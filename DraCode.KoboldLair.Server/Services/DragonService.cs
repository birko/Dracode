using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DraCode.Agent;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Agents.SubAgents;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
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
        public string SessionId { get; init; } = "";
        public DragonAgent? Dragon { get; set; }
        public SageAgent? Sage { get; set; }
        public SeekerAgent? Seeker { get; set; }
        public SentinelAgent? Sentinel { get; set; }
        public WardenAgent? Warden { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public List<SessionMessage> MessageHistory { get; } = new();
        public string? LastMessageId { get; set; }
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
            string? projectsPath = "./projects",
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
            _projectsPath = projectsPath ?? "./projects";

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
                session = new DragonSession { SessionId = sessionId };
                _sessions[sessionId] = session;
                _sessionWebSockets[sessionId] = webSocket;
                _logger.LogInformation("Dragon session started: {SessionId}", sessionId);
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

                    await SendTrackedMessageAsync(webSocket, session, "session_resumed", new
                    {
                        type = "session_resumed",
                        sessionId = currentSessionId,
                        messageCount = session.MessageHistory.Count,
                        timestamp = DateTime.UtcNow
                    });

                    foreach (var msg in session.MessageHistory)
                    {
                        var replayData = new Dictionary<string, object>
                        {
                            ["type"] = msg.Type,
                            ["messageId"] = msg.MessageId,
                            ["isReplay"] = true,
                            ["sessionId"] = currentSessionId,
                            ["timestamp"] = msg.Timestamp
                        };

                        if (msg.Data is IDictionary<string, object> dataDict)
                        {
                            foreach (var kvp in dataDict.Where(k => k.Key != "type" && k.Key != "messageId" && k.Key != "sessionId" && k.Key != "timestamp"))
                            {
                                replayData[kvp.Key] = kvp.Value;
                            }
                        }

                        await SendMessageAsync(webSocket, replayData);
                    }
                }
                else
                {
                    // Create all agents for this session
                    CreateSessionAgents(session);

                    // Set message callbacks for all agents to forward thinking updates
                    Action<string, string> messageCallback = (type, content) =>
                    {
                        _logger.LogInformation("[Dragon Council] [{Type}] {Content}", type, content);
                        if (type == "tool_call" || type == "tool_result" || type == "info")
                        {
                            _ = SendThinkingUpdateAsync(webSocket, currentSessionId, type, content);
                        }
                    };

                    session.Dragon!.SetMessageCallback(messageCallback);
                    session.Sage!.SetMessageCallback(messageCallback);
                    session.Seeker!.SetMessageCallback(messageCallback);
                    session.Sentinel!.SetMessageCallback(messageCallback);
                    session.Warden!.SetMessageCallback(messageCallback);

                    _logger.LogInformation("[Dragon] Starting session...");
                    try
                    {
                        var welcomeResponse = await session.Dragon.StartSessionAsync();
                        _logger.LogInformation("[Dragon] Welcome: {Response}", welcomeResponse);

                        await SendTrackedMessageAsync(webSocket, session, "dragon_message", new
                        {
                            type = "dragon_message",
                            sessionId = currentSessionId,
                            message = welcomeResponse,
                            timestamp = DateTime.UtcNow
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
                    await HandleMessageAsync(webSocket, session, messageText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Dragon WebSocket session {SessionId}", currentSessionId);
            }
            finally
            {
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
                projectsPath: _projectsPath);

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
                retryFailedTaskTool: _drakeFactory != null ? new RetryFailedTaskTool(_drakeFactory, _projectService) : null);

            // Create Dragon coordinator with delegation function
            session.Dragon = new DragonAgent(
                llmProvider,
                options,
                getProjects: GetProjectInfoList,
                delegateToCouncil: (member, task) => DelegateToCouncilAsync(session, member, task));
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

                var response = await session.Dragon!.ContinueSessionAsync(message.Message);
                _logger.LogInformation("[Dragon] Response: {Response}", response);

                await SendTrackedMessageAsync(webSocket, session, "dragon_message", new
                {
                    type = "dragon_message",
                    sessionId,
                    message = response,
                    timestamp = DateTime.UtcNow
                });

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

        private async Task SendThinkingUpdateAsync(WebSocket webSocket, string sessionId, string eventType, string content)
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

                await SendMessageAsync(webSocket, new
                {
                    type = "dragon_thinking",
                    sessionId,
                    eventType,
                    toolName,
                    description = description ?? "Processing...",
                    timestamp = DateTime.UtcNow
                });
            }
            catch { }
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
            session.MessageHistory.Add(new SessionMessage(messageId, messageType, data, DateTime.UtcNow));
            session.LastMessageId = messageId;

            // Efficient trimming: remove excess items in one operation instead of O(n²) loop
            var excess = session.MessageHistory.Count - _maxMessageHistory;
            if (excess > 0)
                session.MessageHistory.RemoveRange(0, excess);
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
                session.MessageHistory.Clear();

                // Set message callbacks for all agents to forward thinking updates
                Action<string, string> messageCallback = (type, content) =>
                {
                    if (type == "tool_call" || type == "tool_result" || type == "info")
                        _ = SendThinkingUpdateAsync(webSocket, sessionId, type, content);
                };

                session.Dragon!.SetMessageCallback(messageCallback);
                session.Sage!.SetMessageCallback(messageCallback);
                session.Seeker!.SetMessageCallback(messageCallback);
                session.Sentinel!.SetMessageCallback(messageCallback);
                session.Warden!.SetMessageCallback(messageCallback);

                var welcomeResponse = await session.Dragon.StartSessionAsync();

                await SendTrackedMessageAsync(webSocket, session, "dragon_reloaded", new
                {
                    type = "dragon_reloaded",
                    sessionId,
                    message = $"Dragon Council reloaded.\n\n{welcomeResponse}",
                    timestamp = DateTime.UtcNow
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
            session.MessageHistory.Clear();

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
                FeatureCount = p.Specification?.Features.Count ?? 0,
                CreatedAt = p.Timestamps.CreatedAt,
                UpdatedAt = p.Timestamps.UpdatedAt,
                HasGitRepository = !string.IsNullOrEmpty(p.Paths.Output) &&
                    _gitService.IsRepositoryAsync(p.Paths.Output).GetAwaiter().GetResult()
            }).ToList();
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
    }
}
