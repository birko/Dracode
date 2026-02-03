using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DraCode.Agent;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Agents.SubAgents;
using DraCode.KoboldLair.Agents.Tools;
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
        private readonly ILogger<DragonService> _logger;
        private readonly ConcurrentDictionary<string, DragonSession> _sessions;
        private readonly ConcurrentDictionary<string, WebSocket> _sessionWebSockets;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly ProjectConfigurationService _projectConfigService;
        private readonly ProjectService _projectService;
        private readonly GitService _gitService;
        private readonly string _projectsPath;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(10);
        private readonly int _maxMessageHistory = 100;
        private bool _disposed;

        public DragonService(
            ILogger<DragonService> logger,
            ProviderConfigurationService providerConfigService,
            ProjectConfigurationService projectConfigService,
            ProjectService projectService,
            GitService gitService,
            string? projectsPath = "./projects")
        {
            _logger = logger;
            _sessions = new ConcurrentDictionary<string, DragonSession>();
            _sessionWebSockets = new ConcurrentDictionary<string, WebSocket>();
            _providerConfigService = providerConfigService;
            _projectConfigService = projectConfigService;
            _projectService = projectService;
            _gitService = gitService;
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

                    session.Dragon!.SetMessageCallback(async (type, content) =>
                    {
                        _logger.LogInformation("[Dragon] [{Type}] {Content}", type, content);
                        if (type == "tool_call" || type == "tool_result" || type == "info")
                        {
                            await SendThinkingUpdateAsync(webSocket, currentSessionId, type, content);
                        }
                    });

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

                var buffer = new byte[1024 * 4];
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
                getExternalPaths: (id) => _projectConfigService.GetAllowedExternalPaths(id));

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
                ProjectId = config.ProjectId,
                ProjectName = config.ProjectName ?? fallbackName,
                WyvernEnabled = config.WyvernEnabled,
                WyrmEnabled = config.WyrmEnabled,
                DrakeEnabled = config.DrakeEnabled,
                KoboldEnabled = config.KoboldEnabled,
                MaxParallelWyverns = config.MaxParallelWyverns,
                MaxParallelWyrms = config.MaxParallelWyrms,
                MaxParallelDrakes = config.MaxParallelDrakes,
                MaxParallelKobolds = config.MaxParallelKobolds,
                WyvernProvider = config.WyvernProvider,
                WyrmProvider = config.WyrmProvider,
                DrakeProvider = config.DrakeProvider,
                KoboldProvider = config.KoboldProvider
            };
        }

        private async Task HandleMessageAsync(WebSocket webSocket, DragonSession session, string messageText)
        {
            var sessionId = session.SessionId;

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var message = JsonSerializer.Deserialize<DragonMessage>(messageText, options);
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

            var specFiles = Directory.GetDirectories(_projectsPath)
                .Select(dir => Path.Combine(dir, "specification.md"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTime)
                .ToList();

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
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            var json = JsonSerializer.Serialize(data, options);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task SendTrackedMessageAsync(WebSocket webSocket, DragonSession session, string messageType, object data)
        {
            var messageId = Guid.NewGuid().ToString();
            var trackedData = new Dictionary<string, object> { ["messageId"] = messageId };

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var dataJson = JsonSerializer.Serialize(data, jsonOptions);
            var dataDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson, jsonOptions);
            if (dataDict != null)
            {
                foreach (var kvp in dataDict)
                    trackedData[kvp.Key] = kvp.Value;
            }

            TrackMessage(session, messageType, trackedData, messageId);
            await SendMessageAsync(webSocket, trackedData);
        }

        private void TrackMessage(DragonSession session, string messageType, object data, string? messageId = null)
        {
            messageId ??= Guid.NewGuid().ToString();
            session.MessageHistory.Add(new SessionMessage(messageId, messageType, data, DateTime.UtcNow));
            session.LastMessageId = messageId;

            while (session.MessageHistory.Count > _maxMessageHistory)
                session.MessageHistory.RemoveAt(0);
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

                session.Dragon!.SetMessageCallback(async (type, content) =>
                {
                    if (type == "tool_call" || type == "tool_result" || type == "info")
                        await SendThinkingUpdateAsync(webSocket, sessionId, type, content);
                });

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
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();
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
