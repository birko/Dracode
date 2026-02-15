using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DraCode.Agent;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Agents.SubAgents;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Represents a stored message for session replay on reconnect
    /// </summary>
    public record SessionMessage(string MessageId, string Type, object Data, DateTime Timestamp);

    /// <summary>
    /// Tracks session state including agents and message history
    /// </summary>
    public class DragonSession
    {
        internal readonly object _historyLock = new object();

        public string SessionId { get; init; } = "";
        public DragonAgent? Dragon { get; set; }
        public SageAgent? Sage { get; set; }
        public SeekerAgent? Seeker { get; set; }
        public SentinelAgent? Sentinel { get; set; }
        public WardenAgent? Warden { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public List<SessionMessage> MessageHistory { get; } = new();
        public string? LastMessageId { get; set; }
        public string? CurrentProjectFolder { get; set; }
        public ReliableWebSocketSender? ReliableSender { get; set; }

        /// <summary>
        /// Tracks whether the Dragon's conversation context should be reset after the current response.
        /// Set when switching between projects so old project context doesn't leak.
        /// </summary>
        public bool PendingContextReset { get; set; }

        /// <summary>
        /// Tracks whether a streaming response was sent during the current request.
        /// Reset before each request, set to true when assistant_final callback fires.
        /// </summary>
        public bool StreamingResponseSent { get; set; }

        /// <summary>
        /// Saves the message history to a JSON file in the project folder.
        /// Thread-safe: takes snapshot under lock to prevent race conditions.
        /// </summary>
        public async Task SaveHistoryToFileAsync(string projectFolder, ILogger? logger = null)
        {
            try
            {
                if (!Directory.Exists(projectFolder))
                {
                    Directory.CreateDirectory(projectFolder);
                }

                var historyPath = Path.Combine(projectFolder, "dragon-history.json");
                
                // Take snapshot under lock to prevent race conditions
                List<SessionMessage> messagesToSave;
                lock (_historyLock)
                {
                    // Prune to last 100 messages before saving
                    messagesToSave = MessageHistory.Count > 100 
                        ? MessageHistory.Skip(MessageHistory.Count - 100).ToList() 
                        : new List<SessionMessage>(MessageHistory);
                }

                // Serialize and write outside lock
                var json = JsonSerializer.Serialize(messagesToSave, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                await File.WriteAllTextAsync(historyPath, json);
                logger?.LogDebug("Saved Dragon history for session {SessionId} to {Path}", SessionId, historyPath);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to save Dragon history for session {SessionId}", SessionId);
            }
        }

        /// <summary>
        /// Loads message history from a JSON file in the project folder.
        /// </summary>
        public static async Task<List<SessionMessage>?> LoadHistoryFromFileAsync(string projectFolder, ILogger? logger = null)
        {
            try
            {
                var historyPath = Path.Combine(projectFolder, "dragon-history.json");
                if (!File.Exists(historyPath))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(historyPath);
                var messages = JsonSerializer.Deserialize<List<SessionMessage>>(json);
                
                logger?.LogDebug("Loaded {Count} messages from Dragon history at {Path}", messages?.Count ?? 0, historyPath);
                return messages;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to load Dragon history from {Path}", projectFolder);
                return null;
            }
        }
    }

    /// <summary>
    /// Service for handling Dragon agent interactions via WebSocket.
    /// Dragon coordinates the Dragon Council (Sage, Seeker, Sentinel, Warden).
    /// </summary>
    public class DragonService : IDisposable
    {
        // Cached JsonSerializerOptions to avoid reflection overhead on every message
        private static readonly JsonSerializerOptions s_readOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions s_writeOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly ILogger<DragonService> _logger;
        private readonly ConcurrentDictionary<string, DragonSession> _sessions;
        private readonly ConcurrentDictionary<string, WebSocket> _sessionWebSockets;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly ProjectConfigurationService _projectConfigService;
        private readonly ProjectService _projectService;
        private readonly GitService _gitService;
        private readonly KoboldFactory? _koboldFactory;
        private readonly DrakeFactory? _drakeFactory;
        private readonly string _projectsPath;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(10);
        private readonly int _maxMessageHistory = 100;

        // Cache for specification file enumeration to avoid frequent filesystem calls
        private List<string>? _specFilesCache;
        private DateTime _specFilesCacheTime = DateTime.MinValue;
        private readonly TimeSpan _specFilesCacheExpiry = TimeSpan.FromSeconds(30);
        private readonly object _specFilesCacheLock = new();
        private bool _disposed;

        public DragonService(
            ILogger<DragonService> logger,
            ProviderConfigurationService providerConfigService,
            ProjectConfigurationService projectConfigService,
            ProjectService projectService,
            GitService gitService,
            KoboldLairConfiguration config,
            KoboldFactory? koboldFactory = null,
            DrakeFactory? drakeFactory = null)
        {
            _logger = logger;
            _sessions = new ConcurrentDictionary<string, DragonSession>();
            _sessionWebSockets = new ConcurrentDictionary<string, WebSocket>();
            _providerConfigService = providerConfigService;
            _projectConfigService = projectConfigService;
            _projectService = projectService;
            _gitService = gitService;
            _koboldFactory = koboldFactory;
            _drakeFactory = drakeFactory;
            _projectsPath = config.ProjectsPath ?? "./projects";

            _cleanupTimer = new Timer(CleanupExpiredSessions, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        private void CleanupExpiredSessions(object? state)
        {
            var expiredSessions = _sessions
                .Where(kvp => DateTime.UtcNow - kvp.Value.LastActivity > _sessionTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                if (_sessions.TryRemove(sessionId, out var session))
                {
                    _logger.LogInformation("Cleaned up expired session: {SessionId}", sessionId);
                }
                _sessionWebSockets.TryRemove(sessionId, out _);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer.Dispose();
                _disposed = true;
            }
        }

        public async Task HandleWebSocketAsync(WebSocket webSocket, string? existingSessionId = null)
        {
            DragonSession? session = null;
            var isResuming = false;
            string? sessionNotFoundReason = null;

            if (!string.IsNullOrEmpty(existingSessionId))
            {
                if (_sessions.TryGetValue(existingSessionId, out session))
                {
                    if (DateTime.UtcNow - session.LastActivity <= _sessionTimeout)
                    {
                        isResuming = true;
                        session.LastActivity = DateTime.UtcNow;
                        _sessionWebSockets[existingSessionId] = webSocket;
                        _logger.LogInformation("Dragon session resumed: {SessionId}", existingSessionId);
                    }
                    else
                    {
                        _sessions.TryRemove(existingSessionId, out _);
                        session = null;
                        sessionNotFoundReason = "expired";
                    }
                }
                else
                {
                    sessionNotFoundReason = "not_found";
                }
            }

            if (session == null)
            {
                var sessionId = Guid.NewGuid().ToString();
                session = new DragonSession 
                { 
                    SessionId = sessionId,
                    ReliableSender = new ReliableWebSocketSender(webSocket, _logger)
                };
                _sessions[sessionId] = session;
                _sessionWebSockets[sessionId] = webSocket;
                _logger.LogInformation("Dragon session started: {SessionId}", sessionId);
            }
            else if (session.ReliableSender == null || isResuming)
            {
                // Create new reliable sender for resumed session
                session.ReliableSender = new ReliableWebSocketSender(webSocket, _logger);
            }

            var currentSessionId = session.SessionId;

            if (!isResuming && !string.IsNullOrEmpty(existingSessionId) && sessionNotFoundReason != null)
            {
                await SendMessageAsync(webSocket, new
                {
                    type = "session_not_found",
                    requestedSessionId = existingSessionId,
                    newSessionId = currentSessionId,
                    reason = sessionNotFoundReason,
                    timestamp = DateTime.UtcNow
                });
            }

            try
            {
                if (isResuming && session.Dragon != null)
                {
                    _logger.LogInformation("[Dragon] Resuming session {SessionId}", currentSessionId);

                    int messageCount;
                    List<SessionMessage> messagesToReplay;
                    lock (session._historyLock)
                    {
                        messageCount = session.MessageHistory.Count;
                        messagesToReplay = new List<SessionMessage>(session.MessageHistory);
                    }

                    await SendTrackedMessageAsync(webSocket, session, "session_resumed", new
                    {
                        type = "session_resumed",
                        sessionId = currentSessionId,
                        messageCount,
                        timestamp = DateTime.UtcNow
                    });

                    foreach (var msg in messagesToReplay)
                    {
                        var replayData = new Dictionary<string, object>
                        {
                            ["type"] = msg.Type,
                            ["messageId"] = msg.MessageId,
                            ["isReplay"] = true,
                            ["sessionId"] = currentSessionId,
                            ["timestamp"] = msg.Timestamp
                        };

                        // Extract properties from the stored data (handles both JsonNode and dictionary)
                        ExtractMessageProperties(msg.Data, replayData);

                        await SendMessageAsync(webSocket, replayData);
                    }
                }
                else
                {
                    // Create all agents for this session
                    CreateSessionAgents(session);

                    // Set message callback for Dragon (handles final response)
                    Action<string, string> dragonCallback = (type, content) =>
                    {
                        _logger.LogInformation("[Dragon] [{Type}] {Content}", type, content);
                        if (type == "tool_call" || type == "tool_result" || type == "info")
                        {
                            _ = SendThinkingUpdateAsync(webSocket, session, currentSessionId, type, content);
                        }
                        else if (type == "assistant_stream")
                        {
                            // Forward streaming chunks to client in real-time
                            _ = SendStreamingChunkAsync(webSocket, session, currentSessionId, content);
                        }
                        else if (type == "assistant_final")
                        {
                            // Mark that streaming occurred - final message will be sent by HandleMessageAsync
                            // to avoid fire-and-forget race conditions where SendStreamCompleteAsync fails
                            // after the fallback check has already been skipped
                            session.StreamingResponseSent = true;
                        }
                    };

                    // Set message callback for council members (does NOT handle assistant_final)
                    // Council members' final responses are returned to Dragon, not sent directly to client
                    Action<string, string> councilCallback = (type, content) =>
                    {
                        _logger.LogDebug("[Dragon Council] [{Type}] {Content}", type, content);
                        if (type == "tool_call" || type == "tool_result" || type == "info")
                        {
                            _ = SendThinkingUpdateAsync(webSocket, session, currentSessionId, type, content);
                        }
                        // Note: assistant_stream and assistant_final are intentionally not forwarded
                        // Council responses are returned to Dragon which sends the final response
                    };

                    session.Dragon!.SetMessageCallback(dragonCallback);
                    session.Sage!.SetMessageCallback(councilCallback);
                    session.Seeker!.SetMessageCallback(councilCallback);
                    session.Sentinel!.SetMessageCallback(councilCallback);
                    session.Warden!.SetMessageCallback(councilCallback);

                    _logger.LogInformation("[Dragon] Starting session...");
                    try
                    {
                        // Reset streaming flag before processing
                        session.StreamingResponseSent = false;
                        var welcomeResponse = await session.Dragon.StartSessionAsync();
                        _logger.LogInformation("[Dragon] Welcome: {Response}", welcomeResponse);

                        // Always send the final message through the reliable awaited path
                        // The isStreamed flag tells the client whether streaming chunks were sent
                        _logger.LogDebug("[Dragon] Sending welcome (isStreamed={IsStreamed})", session.StreamingResponseSent);
                        await SendTrackedMessageAsync(webSocket, session, "dragon_message", new
                        {
                            type = "dragon_message",
                            sessionId = currentSessionId,
                            message = welcomeResponse,
                            timestamp = DateTime.UtcNow,
                            isStreamed = session.StreamingResponseSent
                        });
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError(ex, "Failed to start Dragon session - LLM connection error");
                        await SendTrackedMessageAsync(webSocket, session, "error", new
                        {
                            type = "error",
                            errorType = "llm_connection",
                            sessionId = currentSessionId,
                            message = $"Failed to connect to LLM provider: {ex.Message}",
                            timestamp = DateTime.UtcNow
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to start Dragon session");
                        await SendTrackedMessageAsync(webSocket, session, "error", new
                        {
                            type = "error",
                            errorType = "startup_error",
                            sessionId = currentSessionId,
                            message = $"Failed to initialize Dragon: {ex.Message}",
                            timestamp = DateTime.UtcNow
                        });
                    }
                }

                var buffer = new byte[1024 * 64]; // 64KB buffer for large messages
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    session.LastActivity = DateTime.UtcNow;
                    
                    // Handle message acknowledgments
                    try
                    {
                        var json = JsonDocument.Parse(messageText);
                        if (json.RootElement.TryGetProperty("type", out var typeElement) && 
                            typeElement.GetString() == "ack" &&
                            json.RootElement.TryGetProperty("messageId", out var msgIdElement))
                        {
                            var ackMessageId = msgIdElement.GetString();
                            if (!string.IsNullOrEmpty(ackMessageId))
                            {
                                session.ReliableSender?.AcknowledgeMessage(ackMessageId);
                                continue;
                            }
                        }
                    }
                    catch { }
                    
                    await HandleMessageAsync(webSocket, session, messageText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Dragon WebSocket session {SessionId}", currentSessionId);
            }
            finally
            {
                // Flush any remaining messages before disconnect
                if (session.ReliableSender != null)
                {
                    try
                    {
                        await session.ReliableSender.FlushAsync(TimeSpan.FromSeconds(2));
                        session.ReliableSender.Dispose();
                        session.ReliableSender = null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error flushing reliable sender on disconnect");
                    }
                }
                
                _sessionWebSockets.TryRemove(currentSessionId, out _);
                _logger.LogInformation("Dragon WebSocket disconnected (session preserved): {SessionId}", currentSessionId);
            }
        }

        /// <summary>
        /// Creates all agents for a session
        /// </summary>
        private void CreateSessionAgents(DragonSession session)
        {
            var (providerName, config, options) = _providerConfigService.GetProviderSettingsForAgent("dragon");
            var llmProvider = KoboldLairAgentFactory.CreateLlmProvider(providerName, config);

            // Enable streaming for Dragon (better UX for interactive chat)
            options.EnableStreaming = true;
            options.StreamingFallbackToSync = true;

            // Shared specification dictionary for Sage
            var specifications = new Dictionary<string, Specification>();

            // Create sub-agents
            session.Sage = new SageAgent(
                llmProvider,
                options,
                specifications,
                onSpecificationUpdated: path => _projectService.MarkSpecificationModified(path),
                approveProject: name => _projectService.ApproveProject(name),
                getProjectFolder: name => _projectService.CreateProjectFolder(name),
                projectsPath: _projectsPath,
                onProjectLoaded: projectFolder => OnProjectLoaded(session, projectFolder));

            session.Seeker = new SeekerAgent(
                llmProvider,
                options,
                registerExistingProject: (name, path) => _projectService.RegisterExistingProject(name, path));

            session.Sentinel = new SentinelAgent(
                llmProvider,
                _gitService,
                options,
                getProjectFolder: name =>
                {
                    var folder = _projectService.CreateProjectFolder(name);
                    return Directory.Exists(folder) ? folder : null;
                },
                projectsPath: _projectsPath);

            session.Warden = new WardenAgent(
                llmProvider,
                options,
                getProjectConfig: GetProjectAgentConfig,
                getAllProjects: () => _projectService.GetAllProjects().Select(p => (p.Id, p.Name)).ToList(),
                setAgentEnabled: (id, type, enabled) =>
                {
                    _projectConfigService.SetAgentEnabled(id, type, enabled);
                    _logger.LogInformation("Agent {Type} {State} for {Project}", type, enabled ? "enabled" : "disabled", id);
                },
                setAgentLimit: (id, type, limit) =>
                {
                    _projectConfigService.SetAgentLimit(id, type, limit);
                    _logger.LogInformation("Agent {Type} limit set to {Limit} for {Project}", type, limit, id);
                },
                addExternalPath: (id, path) =>
                {
                    _projectConfigService.AddAllowedExternalPath(id, path);
                    _logger.LogInformation("External path added for {Project}: {Path}", id, path);
                },
                removeExternalPath: (id, path) =>
                {
                    var removed = _projectConfigService.RemoveAllowedExternalPath(id, path);
                    if (removed)
                        _logger.LogInformation("External path removed for {Project}: {Path}", id, path);
                    return removed;
                },
                getExternalPaths: (id) => _projectConfigService.GetAllowedExternalPaths(id),
                getProjectStatus: GetProjectStatusForRetry,
                retryAnalysis: (projectIdOrName) =>
                {
                    var success = _projectService.RetryAnalysis(projectIdOrName);
                    if (success)
                        _logger.LogInformation("Retry initiated for project: {Project}", projectIdOrName);
                    return success;
                },
                getFailedProjects: GetFailedProjects,
                getRunningAgents: GetRunningAgents,
                getRunningAgentsForProject: GetRunningAgentsForProject,
                getGlobalKoboldStats: () => _koboldFactory?.GetStatistics() ?? new KoboldStatistics(),
                retryFailedTaskTool: _drakeFactory != null ? new RetryFailedTaskTool(_drakeFactory, _projectService) : null,
                setTaskPriorityTool: _drakeFactory != null ? new SetTaskPriorityTool(_drakeFactory, _projectService) : null,
                setExecutionState: (projectId, state) => _projectService.SetExecutionState(projectId, state),
                getProjectsNeedingVerification: GetProjectsNeedingVerification,
                retryVerification: RetryVerification,
                getVerificationStatus: GetVerificationStatus,
                getVerificationReport: GetVerificationReport,
                skipVerification: SkipVerification);

            // Create Dragon coordinator with delegation function
            session.Dragon = new DragonAgent(
                llmProvider,
                options,
                getProjects: GetProjectInfoList,
                delegateToCouncil: (member, task) => DelegateToCouncilAsync(session, member, task));
        }

        /// <summary>
        /// Called when a project specification is loaded. Sets the session's project folder,
        /// loads conversation history, and returns a brief summary for the Dragon to present.
        /// </summary>
        private string? OnProjectLoaded(DragonSession session, string projectFolder)
        {
            if (session.CurrentProjectFolder == projectFolder)
                return null; // Already on this project

            var isProjectSwitch = session.CurrentProjectFolder != null;
            session.CurrentProjectFolder = projectFolder;

            if (isProjectSwitch)
            {
                // Switching between projects - reset Dragon context after response is sent
                session.PendingContextReset = true;

                // Clear old project's message history
                lock (session._historyLock)
                {
                    session.MessageHistory.Clear();
                }

                _logger.LogInformation("Session {SessionId} switching from previous project to: {Folder}", session.SessionId, projectFolder);
            }
            else
            {
                _logger.LogInformation("Session {SessionId} set initial project folder: {Folder}", session.SessionId, projectFolder);
            }

            try
            {
                var history = DragonSession.LoadHistoryFromFileAsync(projectFolder, _logger).GetAwaiter().GetResult();
                if (history == null || history.Count == 0)
                    return null;

                // Load history into session for replay/persistence
                lock (session._historyLock)
                {
                    if (session.MessageHistory.Count == 0)
                    {
                        session.MessageHistory.AddRange(history);
                    }
                }

                // Build a brief summary of the previous conversation
                var userMessages = history
                    .Where(m => m.Type == "user_message" || m.Type == "dragon_message")
                    .ToList();

                var userCount = history.Count(m => m.Type == "user_message");
                var assistantCount = history.Count(m => m.Type == "dragon_message");

                // Extract last few user topics for context
                var recentTopics = history
                    .Where(m => m.Type == "user_message")
                    .TakeLast(3)
                    .Select(m => ExtractMessageContent(m.Data))
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Select(c => c!.Length > 80 ? c[..80] + "..." : c)
                    .ToList();

                var summary = $"📜 **Previous conversation found** ({userCount} user messages, {assistantCount} responses).";
                if (recentTopics.Count > 0)
                {
                    summary += "\nLast topics discussed:\n" + string.Join("\n", recentTopics.Select(t => $"- \"{t}\""));
                }
                summary += "\n\nPlease give the user a brief summary of what was discussed previously and ask how they'd like to continue.";

                _logger.LogInformation("Loaded conversation history for session {SessionId}: {UserCount} user, {AssistantCount} assistant messages",
                    session.SessionId, userCount, assistantCount);

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load conversation history for project folder: {Folder}", projectFolder);
                return null;
            }
        }

        /// <summary>
        /// Extracts the text content from a SessionMessage's Data object.
        /// </summary>
        private static string? ExtractMessageContent(object? data)
        {
            if (data == null) return null;

            try
            {
                if (data is JsonObject jsonObj)
                {
                    if (jsonObj.TryGetPropertyValue("content", out var content))
                        return content?.ToString();
                    if (jsonObj.TryGetPropertyValue("message", out var message))
                        return message?.ToString();
                }

                if (data is JsonElement element && element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("content", out var content))
                        return content.GetString();
                    if (element.TryGetProperty("message", out var message))
                        return message.GetString();
                }

                if (data is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("content", out var content))
                        return content?.ToString();
                    if (dict.TryGetValue("message", out var message))
                        return message?.ToString();
                }
            }
            catch
            {
                // Ignore extraction errors
            }

            return null;
        }

        /// <summary>
        /// Delegates a task to a council member (sub-agent)
        /// </summary>
        private async Task<string> DelegateToCouncilAsync(DragonSession session, string councilMember, string task)
        {
            _logger.LogInformation("[Dragon Council] Delegating to {Member}: {Task}", councilMember, task);

            try
            {
                return councilMember.ToLowerInvariant() switch
                {
                    "sage" => await session.Sage!.ProcessTaskAsync(task),
                    "seeker" => await session.Seeker!.ProcessTaskAsync(task),
                    "sentinel" => await session.Sentinel!.ProcessTaskAsync(task),
                    "warden" => await session.Warden!.ProcessTaskAsync(task),
                    _ => $"Unknown council member: {councilMember}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error delegating to {Member}", councilMember);
                return $"Error from {councilMember}: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets project agent configuration by ID or name
        /// </summary>
        private ProjectAgentConfig? GetProjectAgentConfig(string projectIdOrName)
        {
            var config = _projectConfigService.GetProjectConfig(projectIdOrName);
            if (config != null)
            {
                return MapToProjectAgentConfig(config);
            }

            var projects = _projectService.GetAllProjects();
            var project = projects.FirstOrDefault(p =>
                p.Name.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase));

            if (project != null)
            {
                var projectConfig = _projectConfigService.GetOrCreateProjectConfig(project.Id, project.Name);
                return MapToProjectAgentConfig(projectConfig, project.Name);
            }

            return null;
        }

        private static ProjectAgentConfig MapToProjectAgentConfig(DraCode.KoboldLair.Models.Configuration.ProjectConfig config, string? fallbackName = null)
        {
            return new ProjectAgentConfig
            {
                ProjectId = config.Project.Id,
                ProjectName = config.Project.Name ?? fallbackName,
                WyvernEnabled = config.Agents.Wyvern.Enabled,
                WyrmEnabled = config.Agents.Wyrm.Enabled,
                DrakeEnabled = config.Agents.Drake.Enabled,
                KoboldEnabled = config.Agents.Kobold.Enabled,
                MaxParallelWyverns = config.Agents.Wyvern.MaxParallel,
                MaxParallelWyrms = config.Agents.Wyrm.MaxParallel,
                MaxParallelDrakes = config.Agents.Drake.MaxParallel,
                MaxParallelKobolds = config.Agents.Kobold.MaxParallel,
                WyvernProvider = config.Agents.Wyvern.Provider,
                WyrmProvider = config.Agents.Wyrm.Provider,
                DrakeProvider = config.Agents.Drake.Provider,
                KoboldProvider = config.Agents.Kobold.Provider
            };
        }

        private async Task HandleMessageAsync(WebSocket webSocket, DragonSession session, string messageText)
        {
            var sessionId = session.SessionId;

            try
            {
                var message = JsonSerializer.Deserialize<DragonMessage>(messageText, s_readOptions);
                if (message == null) return;

                var messageType = message.Type ?? message.Action;
                if (messageType?.Equals("ping", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await SendMessageAsync(webSocket, new { type = "pong" });
                    return;
                }

                if (message.Type == "reload")
                {
                    await ReloadAgentAsync(session, message.Provider);
                    return;
                }

                if (message.Type == "clear_context")
                {
                    await ClearContextAsync(session);
                    return;
                }

                _logger.LogInformation("Dragon received: {Message}", message.Message);
                TrackMessage(session, "user_message", new { role = "user", content = message.Message });

                await SendMessageAsync(webSocket, new { type = "dragon_typing", sessionId });

                // Reset streaming flag before processing
                session.StreamingResponseSent = false;

                var response = await session.Dragon!.ContinueSessionAsync(message.Message);
                _logger.LogInformation("[Dragon] Response: {Response}", response);

                // Always send the final message through the reliable awaited path
                // The isStreamed flag tells the client whether streaming chunks were sent
                // (so it can finalize the streaming element instead of creating a new one)
                _logger.LogDebug("[Dragon] Sending response (isStreamed={IsStreamed})", session.StreamingResponseSent);
                await SendTrackedMessageAsync(webSocket, session, "dragon_message", new
                {
                    type = "dragon_message",
                    sessionId,
                    message = response,
                    timestamp = DateTime.UtcNow,
                    isStreamed = session.StreamingResponseSent
                });

                // Reset Dragon context if project was switched
                if (session.PendingContextReset)
                {
                    session.PendingContextReset = false;
                    session.Dragon!.ClearConversationHistory();
                    // Keep only the project switch exchange so Dragon has minimal context
                    session.Dragon!.RestoreContext(new[]
                    {
                        ("user", message.Message!),
                        ("assistant", response)
                    });
                    _logger.LogInformation("[Dragon] Context reset after project switch. Kept last exchange only.");
                }

                // Check for new specifications
                await CheckForNewSpecifications(webSocket, session);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error communicating with LLM provider");
                await SendTrackedMessageAsync(webSocket, session, "error", new
                {
                    type = "error",
                    errorType = "llm_connection",
                    sessionId,
                    message = $"Failed to connect to LLM provider: {ex.Message}",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Dragon message");
                await SendTrackedMessageAsync(webSocket, session, "error", new
                {
                    type = "error",
                    errorType = "general",
                    sessionId,
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        private async Task CheckForNewSpecifications(WebSocket webSocket, DragonSession session)
        {
            if (!Directory.Exists(_projectsPath)) return;

            var specFiles = GetCachedSpecificationFiles();
            var latestSpec = specFiles.FirstOrDefault();
            if (latestSpec != null)
            {
                var specInfo = new FileInfo(latestSpec);
                if ((DateTime.UtcNow - specInfo.LastWriteTime).TotalSeconds < 5)
                {
                    var projectFolder = Path.GetDirectoryName(latestSpec)!;
                    var projectName = Path.GetFileName(projectFolder);

                    // Set current project folder and load history if available
                    if (session.CurrentProjectFolder != projectFolder)
                    {
                        session.CurrentProjectFolder = projectFolder;
                        var loadedHistory = await DragonSession.LoadHistoryFromFileAsync(projectFolder, _logger);
                        if (loadedHistory != null && loadedHistory.Count > 0)
                        {
                            // Only load if session history is empty or very different
                            lock (session._historyLock)
                            {
                                if (session.MessageHistory.Count == 0)
                                {
                                    session.MessageHistory.AddRange(loadedHistory);
                                    _logger.LogInformation("Loaded {Count} messages from history for project {Project}", loadedHistory.Count, projectName);
                                }
                            }
                        }
                    }

                    await SendTrackedMessageAsync(webSocket, session, "specification_created", new
                    {
                        type = "specification_created",
                        sessionId = session.SessionId,
                        filename = "specification.md",
                        path = latestSpec,
                        projectFolder,
                        timestamp = DateTime.UtcNow
                    });

                    try
                    {
                        var project = _projectService.RegisterProject(projectName, latestSpec);
                        _logger.LogInformation("Auto-registered project: {Name} ({Id})", projectName, project.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to auto-register project: {Path}", latestSpec);
                    }
                }
            }
        }

        /// <summary>
        /// Gets specification files with caching to avoid frequent filesystem enumeration.
        /// Cache is refreshed every 30 seconds.
        /// </summary>
        private List<string> GetCachedSpecificationFiles()
        {
            lock (_specFilesCacheLock)
            {
                // Check if cache is still valid
                if (_specFilesCache != null && DateTime.UtcNow - _specFilesCacheTime < _specFilesCacheExpiry)
                {
                    return _specFilesCache;
                }

                // Refresh cache
                _specFilesCache = Directory.GetDirectories(_projectsPath)
                    .Select(dir => Path.Combine(dir, "specification.md"))
                    .Where(File.Exists)
                    .OrderByDescending(File.GetLastWriteTime)
                    .ToList();
                _specFilesCacheTime = DateTime.UtcNow;

                return _specFilesCache;
            }
        }

        /// <summary>
        /// Invalidates the specification files cache (call when a new spec is created)
        /// </summary>
        private void InvalidateSpecFilesCache()
        {
            lock (_specFilesCacheLock)
            {
                _specFilesCache = null;
                _specFilesCacheTime = DateTime.MinValue;
            }
        }

        private async Task SendThinkingUpdateAsync(WebSocket webSocket, DragonSession session, string sessionId, string eventType, string content)
        {
            try
            {
                string? toolName = null;
                string? description = null;

                if (eventType == "tool_call" && content.StartsWith("Tool: "))
                {
                    var lines = content.Split('\n', 2);
                    toolName = lines[0].Substring(6).Trim();
                    description = $"Calling {toolName}...";
                }
                else if (eventType == "tool_result" && content.StartsWith("Result from "))
                {
                    var colonIndex = content.IndexOf(':');
                    if (colonIndex > 12)
                    {
                        toolName = content.Substring(12, colonIndex - 12);
                        description = $"Processing {toolName} result...";
                    }
                }
                else if (eventType == "info")
                {
                    description = content.StartsWith("ITERATION") ? "Thinking..." : content;
                }

                var messageId = Guid.NewGuid().ToString();
                var data = new
                {
                    type = "dragon_thinking",
                    sessionId,
                    eventType,
                    toolName,
                    description = description ?? "Processing...",
                    timestamp = DateTime.UtcNow
                };

                // Use reliable sender with high priority (lower number = higher priority)
                if (session.ReliableSender != null)
                {
                    session.ReliableSender.QueueMessage(messageId, data, priority: 5);
                }
                else
                {
                    await SendMessageAsync(webSocket, data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending thinking update");
            }
        }

        private async Task SendStreamingChunkAsync(WebSocket webSocket, DragonSession session, string sessionId, string chunk)
        {
            try
            {
                var messageId = Guid.NewGuid().ToString();
                var data = new
                {
                    type = "dragon_stream",
                    sessionId,
                    chunk,
                    timestamp = DateTime.UtcNow
                };

                // Use reliable sender with highest priority for streaming
                if (session.ReliableSender != null)
                {
                    session.ReliableSender.QueueMessage(messageId, data, priority: 0);
                }
                else
                {
                    await SendMessageAsync(webSocket, data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending streaming chunk");
            }
        }

        private async Task SendMessageAsync(WebSocket webSocket, object data)
        {
            var json = JsonSerializer.Serialize(data, s_writeOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task SendTrackedMessageAsync(WebSocket webSocket, DragonSession session, string messageType, object data)
        {
            var messageId = Guid.NewGuid().ToString();

            // Use JsonNode for efficient property injection without reflection
            // Serialize to JsonNode, add messageId, then serialize to bytes
            var jsonNode = JsonSerializer.SerializeToNode(data, s_writeOptions);
            if (jsonNode is System.Text.Json.Nodes.JsonObject jsonObject)
            {
                jsonObject["messageId"] = messageId;
            }

            TrackMessage(session, messageType, jsonNode ?? data, messageId);

            // Serialize directly to bytes for WebSocket send
            var json = jsonNode?.ToJsonString(s_writeOptions) ?? JsonSerializer.Serialize(data, s_writeOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private void TrackMessage(DragonSession session, string messageType, object data, string? messageId = null)
        {
            messageId ??= Guid.NewGuid().ToString();
            
            lock (session._historyLock)
            {
                session.MessageHistory.Add(new SessionMessage(messageId, messageType, data, DateTime.UtcNow));
                session.LastMessageId = messageId;

                // Efficient trimming: remove excess items in one operation instead of O(n²) loop
                var excess = session.MessageHistory.Count - _maxMessageHistory;
                if (excess > 0)
                    session.MessageHistory.RemoveRange(0, excess);
            }

            // Fire-and-forget async save if project folder is set
            if (!string.IsNullOrEmpty(session.CurrentProjectFolder))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await session.SaveHistoryToFileAsync(session.CurrentProjectFolder, _logger);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to persist Dragon history for session {SessionId}", session.SessionId);
                    }
                });
            }
        }

        private async Task ReloadAgentAsync(DragonSession session, string? providerOverride = null)
        {
            var sessionId = session.SessionId;
            if (!_sessionWebSockets.TryGetValue(sessionId, out var webSocket)) return;

            try
            {
                if (!string.IsNullOrEmpty(providerOverride))
                    _providerConfigService.SetProviderForAgent("dragon", providerOverride);

                CreateSessionAgents(session);
                
                lock (session._historyLock)
                {
                    session.MessageHistory.Clear();
                }

                // Set message callback for Dragon (handles final response)
                Action<string, string> dragonCallback = (type, content) =>
                {
                    if (type == "tool_call" || type == "tool_result" || type == "info")
                        _ = SendThinkingUpdateAsync(webSocket, session, sessionId, type, content);
                    else if (type == "assistant_stream")
                        _ = SendStreamingChunkAsync(webSocket, session, sessionId, content);
                    else if (type == "assistant_final")
                    {
                        // Mark that streaming occurred - final message sent by awaited path below
                        session.StreamingResponseSent = true;
                    }
                };

                // Set message callback for council members (does NOT handle assistant_final)
                // Council members' final responses are returned to Dragon, not sent directly to client
                Action<string, string> councilCallback = (type, content) =>
                {
                    if (type == "tool_call" || type == "tool_result" || type == "info")
                        _ = SendThinkingUpdateAsync(webSocket, session, sessionId, type, content);
                    // Note: assistant_stream and assistant_final are intentionally not forwarded
                };

                session.Dragon!.SetMessageCallback(dragonCallback);
                session.Sage!.SetMessageCallback(councilCallback);
                session.Seeker!.SetMessageCallback(councilCallback);
                session.Sentinel!.SetMessageCallback(councilCallback);
                session.Warden!.SetMessageCallback(councilCallback);

                // Reset streaming flag before processing
                session.StreamingResponseSent = false;
                var welcomeResponse = await session.Dragon.StartSessionAsync();

                // Always send through the reliable awaited path
                _logger.LogDebug("[Dragon] Sending reload welcome (isStreamed={IsStreamed})", session.StreamingResponseSent);
                await SendTrackedMessageAsync(webSocket, session, "dragon_reloaded", new
                {
                    type = "dragon_reloaded",
                    sessionId,
                    message = $"Dragon Council reloaded.\n\n{welcomeResponse}",
                    timestamp = DateTime.UtcNow,
                    isStreamed = session.StreamingResponseSent
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading Dragon");
                await SendTrackedMessageAsync(webSocket, session, "error", new
                {
                    type = "error",
                    sessionId,
                    message = $"Failed to reload: {ex.Message}",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        private async Task ClearContextAsync(DragonSession session)
        {
            var sessionId = session.SessionId;
            if (!_sessionWebSockets.TryGetValue(sessionId, out var webSocket)) return;

            session.Dragon?.ClearContext();
            
            lock (session._historyLock)
            {
                session.MessageHistory.Clear();
            }

            await SendTrackedMessageAsync(webSocket, session, "context_cleared", new
            {
                type = "context_cleared",
                sessionId,
                message = "Conversation context cleared.",
                timestamp = DateTime.UtcNow
            });
        }

        private List<ProjectInfo> GetProjectInfoList()
        {
            return _projectService.GetAllProjects().Select(p => new ProjectInfo
            {
                Id = p.Id,
                Name = p.Name,
                Status = p.Status.ToString(),
                ExecutionState = p.ExecutionState.ToString(),
                FeatureCount = GetFeatureCountForProject(p),
                CreatedAt = p.Timestamps.CreatedAt,
                UpdatedAt = p.Timestamps.UpdatedAt,
                HasGitRepository = !string.IsNullOrEmpty(p.Paths.Output) &&
                    _gitService.IsRepositoryAsync(p.Paths.Output).GetAwaiter().GetResult() // Sync wrapper for projection
            }).ToList();
        }

        /// <summary>
        /// Gets the feature count for a project by loading from specification.features.json
        /// </summary>
        private int GetFeatureCountForProject(Project project)
        {
            if (string.IsNullOrEmpty(project.Paths.Output))
                return 0;

            try
            {
                var featuresPath = Path.Combine(project.Paths.Output, "specification.features.json");
                if (!File.Exists(featuresPath))
                    return 0;

                var json = File.ReadAllText(featuresPath);
                var features = System.Text.Json.JsonSerializer.Deserialize<List<object>>(json);
                return features?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets project status and error message for retry tool
        /// </summary>
        private (bool Success, string? ErrorMessage, string? Status) GetProjectStatusForRetry(string projectIdOrName)
        {
            var projects = _projectService.GetAllProjects();
            var project = projects.FirstOrDefault(p =>
                p.Name.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase));

            if (project == null)
            {
                return (false, null, null);
            }

            return (true, project.Tracking.ErrorMessage, project.Status.ToString());
        }

        /// <summary>
        /// Gets list of failed projects for retry tool
        /// </summary>
        private List<(string Id, string Name, string Status, string? ErrorMessage)> GetFailedProjects()
        {
            return _projectService.GetAllProjects()
                .Where(p => p.Status == ProjectStatus.Failed)
                .Select(p => (p.Id, p.Name, p.Status.ToString(), p.Tracking.ErrorMessage))
                .ToList();
        }

        /// <summary>
        /// Gets running agent information for all projects
        /// </summary>
        private List<RunningAgentInfo> GetRunningAgents()
        {
            var result = new List<RunningAgentInfo>();
            var projects = _projectService.GetAllProjects();

            foreach (var project in projects)
            {
                var info = BuildRunningAgentInfo(project);
                if (info.ActiveDrakes > 0 || info.ActiveKobolds > 0)
                {
                    result.Add(info);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets running agent information for a specific project
        /// </summary>
        private RunningAgentInfo? GetRunningAgentsForProject(string projectIdOrName)
        {
            var projects = _projectService.GetAllProjects();
            var project = projects.FirstOrDefault(p =>
                p.Name.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase));

            if (project == null)
            {
                return null;
            }

            return BuildRunningAgentInfo(project);
        }

        /// <summary>
        /// Builds running agent info for a project
        /// </summary>
        private RunningAgentInfo BuildRunningAgentInfo(Project project)
        {
            var info = new RunningAgentInfo
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectStatus = project.Status.ToString()
            };

            // Get Kobolds for this project
            if (_koboldFactory != null)
            {
                var projectKobolds = _koboldFactory.GetAllKobolds()
                    .Where(k => k.ProjectId == project.Id)
                    .ToList();

                info.ActiveKobolds = projectKobolds.Count(k =>
                    k.Status == KoboldStatus.Assigned || k.Status == KoboldStatus.Working);
                info.WorkingKobolds = projectKobolds.Count(k => k.Status == KoboldStatus.Working);
                info.AssignedKobolds = projectKobolds.Count(k => k.Status == KoboldStatus.Assigned);

                info.Kobolds = projectKobolds
                    .Where(k => k.Status == KoboldStatus.Assigned || k.Status == KoboldStatus.Working)
                    .Select(k => new KoboldInfo
                    {
                        Id = k.Id.ToString(),
                        AgentType = k.AgentType,
                        Status = k.Status.ToString(),
                        TaskDescription = k.TaskDescription,
                        StartedAt = k.StartedAt,
                        WorkingDuration = k.StartedAt.HasValue ? DateTime.UtcNow - k.StartedAt.Value : null,
                        IsStuck = k.IsStuck
                    })
                    .ToList();
            }

            // Get Drakes for this project
            if (_drakeFactory != null)
            {
                var allDrakes = _drakeFactory.GetAllDrakes();
                var projectDrakes = allDrakes
                    .Where(d => d.ProjectId == project.Id)
                    .ToList();

                info.ActiveDrakes = projectDrakes.Count;

                info.Drakes = projectDrakes.Select(d =>
                {
                    var stats = d.GetStatistics();
                    return new DrakeInfo
                    {
                        Name = d.Name,
                        TaskFile = d.TaskFilePath,
                        TotalTasks = stats.TotalTasks,
                        CompletedTasks = stats.DoneTasks,
                        WorkingTasks = stats.WorkingTasks,
                        PendingTasks = stats.UnassignedTasks
                    };
                }).ToList();
            }

            return info;
        }

        public DragonStatistics GetStatistics()
        {
            var totalSpecs = 0;
            if (Directory.Exists(_projectsPath))
            {
                totalSpecs = Directory.GetDirectories(_projectsPath)
                    .Count(dir => File.Exists(Path.Combine(dir, "specification.md")));
            }

            return new DragonStatistics
            {
                ActiveSessions = _sessions.Count,
                TotalSpecifications = totalSpecs
            };
        }

        /// <summary>
        /// Extracts message properties from stored data (handles JsonNode, JsonObject, and dictionary types)
        /// </summary>
        private static void ExtractMessageProperties(object data, Dictionary<string, object> targetDict)
        {
            var skipKeys = new HashSet<string> { "type", "messageId", "sessionId", "timestamp" };

            if (data is System.Text.Json.Nodes.JsonObject jsonObject)
            {
                foreach (var kvp in jsonObject)
                {
                    if (!skipKeys.Contains(kvp.Key) && kvp.Value != null)
                    {
                        // Convert JsonNode to appropriate .NET type
                        targetDict[kvp.Key] = ConvertJsonNode(kvp.Value);
                    }
                }
            }
            else if (data is System.Text.Json.Nodes.JsonNode jsonNode)
            {
                // Handle case where it's a JsonNode but not JsonObject
                if (jsonNode is System.Text.Json.Nodes.JsonObject obj)
                {
                    foreach (var kvp in obj)
                    {
                        if (!skipKeys.Contains(kvp.Key) && kvp.Value != null)
                        {
                            targetDict[kvp.Key] = ConvertJsonNode(kvp.Value);
                        }
                    }
                }
            }
            else if (data is IDictionary<string, object> dataDict)
            {
                foreach (var kvp in dataDict.Where(k => !skipKeys.Contains(k.Key)))
                {
                    targetDict[kvp.Key] = kvp.Value;
                }
            }
            else if (data is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in jsonElement.EnumerateObject())
                {
                    if (!skipKeys.Contains(prop.Name))
                    {
                        targetDict[prop.Name] = ConvertJsonElement(prop.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Converts a JsonNode to an appropriate .NET type for serialization
        /// </summary>
        private static object ConvertJsonNode(System.Text.Json.Nodes.JsonNode? node)
        {
            if (node == null) return null!;

            if (node is System.Text.Json.Nodes.JsonValue value)
            {
                // Try to get the underlying value
                if (value.TryGetValue<string>(out var str)) return str;
                if (value.TryGetValue<bool>(out var b)) return b;
                if (value.TryGetValue<int>(out var i)) return i;
                if (value.TryGetValue<long>(out var l)) return l;
                if (value.TryGetValue<double>(out var d)) return d;
                if (value.TryGetValue<DateTime>(out var dt)) return dt;
                return value.ToString();
            }

            if (node is System.Text.Json.Nodes.JsonArray arr)
            {
                return arr.Select(n => ConvertJsonNode(n)).ToList();
            }

            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                var dict = new Dictionary<string, object>();
                foreach (var kvp in obj)
                {
                    if (kvp.Value != null)
                    {
                        dict[kvp.Key] = ConvertJsonNode(kvp.Value);
                    }
                }
                return dict;
            }

            return node.ToString();
        }

        /// <summary>
        /// Converts a JsonElement to an appropriate .NET type
        /// </summary>
        private static object ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
                _ => element.ToString()
            };
        }

        /// <summary>
        /// Gets projects needing verification (AwaitingVerification or Failed verification)
        /// </summary>
        private List<(string Id, string Name, string Status, string? VerificationStatus)> GetProjectsNeedingVerification()
        {
            return _projectService.GetAllProjects()
                .Where(p => p.Status == ProjectStatus.AwaitingVerification || p.VerificationStatus == VerificationStatus.Failed)
                .Select(p => (p.Id, p.Name, p.Status.ToString(), p.VerificationStatus.ToString()))
                .ToList();
        }

        /// <summary>
        /// Resets verification state to trigger re-verification
        /// </summary>
        private bool RetryVerification(string projectIdOrName)
        {
            var project = _projectService.GetAllProjects().FirstOrDefault(p =>
                p.Name.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase));

            if (project == null || project.Status != ProjectStatus.AwaitingVerification)
                return false;

            project.VerificationStatus = VerificationStatus.NotStarted;
            project.VerificationStartedAt = null;
            project.VerificationCompletedAt = null;
            project.VerificationReport = null;
            project.VerificationChecks.Clear();
            _projectService.UpdateProject(project);
            _logger.LogInformation("Verification reset for project: {Project}", projectIdOrName);
            return true;
        }

        /// <summary>
        /// Gets verification status summary for a project
        /// </summary>
        private (bool Success, string? VerificationStatus, DateTime? LastVerified, string? Summary) GetVerificationStatus(string projectIdOrName)
        {
            var project = _projectService.GetAllProjects().FirstOrDefault(p =>
                p.Name.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase));

            if (project == null)
                return (false, null, null, null);

            var summary = project.VerificationReport != null && project.VerificationReport.Length > 500 
                ? project.VerificationReport.Substring(0, 500) + "..." 
                : project.VerificationReport;

            return (true, project.VerificationStatus.ToString(), project.VerificationCompletedAt, summary);
        }

        /// <summary>
        /// Gets full verification report for a project
        /// </summary>
        private (bool Success, string? Report) GetVerificationReport(string projectIdOrName)
        {
            var project = _projectService.GetAllProjects().FirstOrDefault(p =>
                p.Name.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase));

            if (project == null)
                return (false, null);

            return (true, project.VerificationReport);
        }

        /// <summary>
        /// Skips verification and marks project as Verified
        /// </summary>
        private bool SkipVerification(string projectIdOrName)
        {
            var project = _projectService.GetAllProjects().FirstOrDefault(p =>
                p.Name.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase));

            if (project == null || project.Status != ProjectStatus.AwaitingVerification)
                return false;

            project.VerificationStatus = VerificationStatus.Skipped;
            project.VerificationCompletedAt = DateTime.UtcNow;
            project.VerificationReport = "Verification skipped by user.";
            _projectService.UpdateProjectStatus(project.Id, ProjectStatus.Verified);
            _logger.LogInformation("Verification skipped for project: {Project}", projectIdOrName);
            return true;
        }
    }
}
