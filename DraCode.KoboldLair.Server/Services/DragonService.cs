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
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Represents a stored message for session replay on reconnect
    /// </summary>
    public record SessionMessage(string MessageId, string Type, Dictionary<string, object> Data, DateTime Timestamp);

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
        public WebSocketSender? Sender { get; set; }

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
        /// Tracks whether the welcome message has been sent to the client.
        /// Welcome is only sent after client sends client_ready to prevent race conditions.
        /// </summary>
        public bool WelcomeSent { get; set; }

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
        private readonly IProjectRepository _projectRepository;
        private readonly GitService _gitService;
        private readonly KoboldFactory? _koboldFactory;
        private readonly DrakeFactory? _drakeFactory;
        private readonly KoboldPlanService? _planService;
        private readonly string _projectsPath;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(10);
        private readonly int _maxMessageHistory = 100;

        // Request queue for non-blocking Dragon execution
        private readonly DragonRequestQueue _requestQueue;
        private readonly ProjectNotificationService? _notificationService;
        private readonly DraCode.KoboldLair.Data.Repositories.Sql.SqlHistoryRepository? _historyRepository;
        private readonly DraCode.KoboldLair.Services.EventSourcing.SpecificationEventService? _specEventService;

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
            IProjectRepository projectRepository,
            GitService gitService,
            KoboldLairConfiguration config,
            KoboldFactory? koboldFactory = null,
            DrakeFactory? drakeFactory = null,
            KoboldPlanService? planService = null,
            int maxConcurrentDragonRequests = 5,
            ProjectNotificationService? notificationService = null,
            DraCode.KoboldLair.Data.Repositories.Sql.SqlHistoryRepository? historyRepository = null,
            DraCode.KoboldLair.Services.EventSourcing.SpecificationEventService? specEventService = null)
        {
            _logger = logger;
            _sessions = new ConcurrentDictionary<string, DragonSession>();
            _sessionWebSockets = new ConcurrentDictionary<string, WebSocket>();
            _providerConfigService = providerConfigService;
            _projectConfigService = projectConfigService;
            _projectService = projectService;
            _projectRepository = projectRepository;
            _gitService = gitService;
            _koboldFactory = koboldFactory;
            _drakeFactory = drakeFactory;
            _planService = planService;
            _notificationService = notificationService;
            _historyRepository = historyRepository;
            _specEventService = specEventService;
            _projectsPath = config.ProjectsPath ?? "./projects";

            // Initialize the request queue for non-blocking execution
            _requestQueue = new DragonRequestQueue(logger, maxConcurrentDragonRequests);

            // Subscribe to real-time notification events for push to active sessions
            if (_notificationService != null)
            {
                _notificationService.OnNotification += OnProjectNotificationReceived;
            }

            _cleanupTimer = new Timer(CleanupExpiredSessions, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Gets statistics about the Dragon service and request queue
        /// </summary>
        public DragonServiceStatistics GetStatistics()
        {
            return new DragonServiceStatistics
            {
                ActiveSessions = _sessions.Count,
                QueueStatistics = _requestQueue.GetStatistics()
            };
        }

        /// <summary>
        /// Statistics for the Dragon service
        /// </summary>
        public class DragonServiceStatistics
        {
            public int ActiveSessions { get; init; }
            public QueueStatistics QueueStatistics { get; init; } = new();
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

        /// <summary>
        /// Handles real-time notification events by pushing to all active Dragon sessions
        /// viewing the affected project.
        /// </summary>
        private void OnProjectNotificationReceived(string projectName, ProjectNotification notification)
        {
            // Find sessions that are viewing this project and push the notification
            foreach (var kvp in _sessions)
            {
                var session = kvp.Value;
                if (string.IsNullOrEmpty(session.CurrentProjectFolder))
                    continue;

                // Match by project folder name
                var folderName = Path.GetFileName(session.CurrentProjectFolder);
                var sanitizedProjectName = projectName.ToLowerInvariant().Replace(" ", "-");
                if (!string.Equals(folderName, sanitizedProjectName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(folderName, projectName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Get the WebSocket for this session
                if (!_sessionWebSockets.TryGetValue(kvp.Key, out var webSocket))
                    continue;

                if (webSocket.State != System.Net.WebSockets.WebSocketState.Open)
                    continue;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendTrackedMessageAsync(webSocket, session, "project_notification", new
                        {
                            type = "project_notification",
                            notificationType = notification.Type,
                            sessionId = session.SessionId,
                            message = notification.Message,
                            metadata = notification.Metadata,
                            timestamp = notification.CreatedAt
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to push notification to session {SessionId}", session.SessionId);
                    }
                });
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_notificationService != null)
                {
                    _notificationService.OnNotification -= OnProjectNotificationReceived;
                }
                _cleanupTimer.Dispose();
                _requestQueue?.Dispose();
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
                    Sender = new WebSocketSender(webSocket, _logger)
                };
                _sessions[sessionId] = session;
                _sessionWebSockets[sessionId] = webSocket;
                _logger.LogInformation("Dragon session started: {SessionId}", sessionId);
            }
            else if (session.Sender == null || isResuming)
            {
                // Create new sender for resumed session
                session.Sender = new WebSocketSender(webSocket, _logger);
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
                }, session.Sender);
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
                        var replayData = new Dictionary<string, object>(msg.Data)
                        {
                            ["type"] = msg.Type,
                            ["messageId"] = msg.MessageId,
                            ["isReplay"] = true,
                            ["sessionId"] = currentSessionId,
                            ["timestamp"] = msg.Timestamp
                        };

                        await SendMessageAsync(webSocket, replayData, session.Sender);
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
                        if (type == "tool_call" || type == "tool_result" || type == "info" || type == "debug" || type == "warning")
                        {
                            _ = SendThinkingUpdateAsync(webSocket, session, currentSessionId, type, content, agentSource: "Dragon");
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

                    // Create named callbacks for each council member so the client knows who's working
                    Action<string, string> MakeCouncilCallback(string memberName) => (type, content) =>
                    {
                        _logger.LogDebug("[{Member}] [{Type}] {Content}", memberName, type, content);
                        if (type == "tool_call" || type == "tool_result" || type == "info" || type == "debug" || type == "warning")
                        {
                            _ = SendThinkingUpdateAsync(webSocket, session, currentSessionId, type, content, agentSource: memberName);
                        }
                        else if (type == "assistant_final")
                        {
                            // Forward council member final responses so client knows they're done
                            _ = SendThinkingUpdateAsync(webSocket, session, currentSessionId, type, content, agentSource: memberName);
                        }
                        // Note: assistant_stream is intentionally not forwarded for council members
                    };

                    session.Dragon!.SetMessageCallback(dragonCallback);
                    session.Sage!.SetMessageCallback(MakeCouncilCallback("Sage"));
                    session.Seeker!.SetMessageCallback(MakeCouncilCallback("Seeker"));
                    session.Sentinel!.SetMessageCallback(MakeCouncilCallback("Sentinel"));
                    session.Warden!.SetMessageCallback(MakeCouncilCallback("Warden"));

                    // Welcome will be sent after client sends client_ready message
                    // This prevents race condition where server sends messages before client is ready
                    _logger.LogInformation("[Dragon] Session initialized, waiting for client_ready...");
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
                session.Sender?.Dispose();
                session.Sender = null;

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
            // Enable verbose mode so iteration/tool events fire to the thinking indicator
            options.Verbose = true;

            // Shared specification dictionary for Sage
            var specifications = new Dictionary<string, Specification>();

            // Create sub-agents
            session.Sage = new SageAgent(
                llmProvider,
                options,
                specifications,
                onSpecificationUpdated: path => _projectService.MarkSpecificationModified(path),
                approveProject: name => _projectService.ApproveProject(name),
                getProjectFolder: name => ResolveProjectFolder(session, name),
                projectsPath: _projectsPath,
                onProjectLoaded: projectFolder => OnProjectLoadedAsync(session, projectFolder),
                getActiveProjectName: () => GetActiveProjectName(session),
                eventService: _specEventService);

            session.Seeker = new SeekerAgent(
                llmProvider,
                options,
                registerExistingProject: (name, path) =>
                {
                    var projectId = _projectService.RegisterExistingProject(name, path);
                    if (projectId != null)
                    {
                        var project = _projectService.GetProject(projectId);
                        if (project != null && !string.IsNullOrEmpty(project.Paths.Output))
                            session.CurrentProjectFolder = project.Paths.Output;
                    }
                    return projectId;
                });

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
                addExternalPath: async (id, path) =>
                {
                    // Normalize path to prevent traversal attacks
                    var normalizedPath = Path.GetFullPath(path);
                    if (!Path.IsPathRooted(normalizedPath))
                    {
                        _logger.LogWarning("Rejected non-absolute external path for {Project}: {Path}", id, path);
                        return;
                    }

                    await _projectRepository.AddAllowedExternalPathAsync(id, normalizedPath);
                    _logger.LogInformation("External path added for {Project}: {Path}", id, normalizedPath);

                    // Update Dragon's context if this is the current session's project
                    RefreshDragonContextForProject(session, id);
                },
                removeExternalPath: async (id, path) =>
                {
                    var removed = await _projectRepository.RemoveAllowedExternalPathAsync(id, path);
                    if (removed)
                    {
                        _logger.LogInformation("External path removed for {Project}: {Path}", id, path);

                        // Update Dragon's context if this is the current session's project
                        RefreshDragonContextForProject(session, id);
                    }
                    return removed;
                },
                getExternalPaths: (id) => _projectRepository.GetAllowedExternalPaths(id),
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
                skipVerification: SkipVerification,
                viewTaskDetailsTool: _drakeFactory != null ? new ViewTaskDetailsTool(_drakeFactory, _projectService, _planService) : null,
                projectProgressTool: _drakeFactory != null ? new ProjectProgressTool(_drakeFactory, _projectService) : null,
                viewWorkspaceTool: new ViewWorkspaceTool(_projectService),
                deleteProjectTool: new DeleteProjectTool(
                    getProject: id => _projectService.GetProject(id),
                    deleteProject: (id, deleteFiles) => DeleteProjectFromRegistry(id, deleteFiles),
                    getAllProjects: () => _projectService.GetAllProjects().Select(p => (p.Id, p.Name)).ToList()),
                notificationsTool: _notificationService != null ? new NotificationsTool(
                    getPendingNotifications: projectName =>
                    {
                        var notifications = _notificationService.GetPendingNotifications(projectName);
                        return notifications.Select(n => new NotificationInfo
                        {
                            Id = n.Id,
                            Type = n.Type,
                            Message = n.Message,
                            Metadata = n.Metadata,
                            CreatedAt = n.CreatedAt
                        }).ToList();
                    },
                    markAsRead: (projectName, ids) => _notificationService.MarkAsRead(projectName, ids),
                    getAllPendingCounts: () =>
                    {
                        var projects = _projectService.GetAllProjects();
                        return projects
                            .Select(p => (p.Name, Count: _notificationService.GetPendingNotifications(p.Name).Count))
                            .Where(x => x.Count > 0)
                            .ToList();
                    }
                ) : null,
                userSettingsTool: new UserSettingsTool(
                    getUserSettings: () => _providerConfigService.GetUserSettings(),
                    setProviderForAgent: (agentType, provider, model) => _providerConfigService.SetProviderForAgent(agentType, provider, model),
                    setProviderForKoboldAgentType: (agentType, provider, model) => _providerConfigService.SetProviderForKoboldAgentType(agentType, provider, model),
                    getAvailableProviders: () => _providerConfigService.GetAvailableProviders().Select(p => p.Name).ToList()
                ),
                viewAnalysisTool: new ViewAnalysisTool(
                    getProjectFolder: projectNameOrId =>
                    {
                        var project = _projectService.FindProjectByName(projectNameOrId)
                            ?? _projectService.GetProject(projectNameOrId);
                        return project?.Paths.Output;
                    },
                    getAllProjects: () => _projectService.GetAllProjects().Select(p => (p.Id, p.Name)).ToList()
                ),
                resetProject: async (name, keepHistory) => await _projectService.ResetProjectAsync(name, keepHistory));

            // Create Dragon coordinator with delegation function
            session.Dragon = new DragonAgent(
                llmProvider,
                options,
                getProjects: GetProjectInfoListAsync,
                delegateToCouncil: (member, task) => DelegateToCouncilAsync(session, member, task));
        }

        /// <summary>
        /// Resolves the project folder for a given name, preferring the current session's project
        /// or an existing project with a matching name before creating a new folder.
        /// </summary>
        private string ResolveProjectFolder(DragonSession session, string name)
        {
            // If session already has a project folder set (e.g., from import), use it
            if (!string.IsNullOrEmpty(session.CurrentProjectFolder) && Directory.Exists(session.CurrentProjectFolder))
            {
                _logger.LogInformation("Using session's current project folder: {Folder}", session.CurrentProjectFolder);
                return session.CurrentProjectFolder;
            }

            // Check if an existing project matches this name (fuzzy)
            var existing = _projectService.FindProjectByName(name);
            if (existing != null && !string.IsNullOrEmpty(existing.Paths.Output) && Directory.Exists(existing.Paths.Output))
            {
                _logger.LogInformation("Found existing project '{Name}' for folder resolution, using: {Folder}", existing.Name, existing.Paths.Output);
                session.CurrentProjectFolder = existing.Paths.Output;
                return existing.Paths.Output;
            }

            // No existing project found - create a new folder (original behavior)
            return _projectService.CreateProjectFolder(name);
        }

        /// <summary>
        /// Gets the active project name from the session's current project folder.
        /// </summary>
        private string? GetActiveProjectName(DragonSession session)
        {
            if (string.IsNullOrEmpty(session.CurrentProjectFolder))
                return null;

            // Try to find the project by its output folder
            var allProjects = _projectService.GetAllProjects();
            var project = allProjects.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Paths.Output) &&
                string.Equals(p.Paths.Output, session.CurrentProjectFolder, StringComparison.OrdinalIgnoreCase));

            return project?.Name;
        }

        /// <summary>
        /// Called when a project specification is loaded. Sets the session's project folder,
        /// loads conversation history, and returns a brief summary for the Dragon to present.
        /// </summary>
        private async Task<string?> OnProjectLoadedAsync(DragonSession session, string projectFolder)
        {
            if (session.CurrentProjectFolder == projectFolder)
                return null; // Already on this project

            var isProjectSwitch = session.CurrentProjectFolder != null;
            session.CurrentProjectFolder = projectFolder;

            // Update Dragon's context with project-specific paths
            if (session.Dragon != null)
            {
                // Find the project to get its allowed external paths
                var allProjects = _projectService.GetAllProjects();

                // Normalize the project folder for comparison (handle relative vs absolute paths)
                var normalizedProjectFolder = Path.GetFullPath(projectFolder);

                var currentProject = allProjects.FirstOrDefault(p =>
                {
                    if (string.IsNullOrEmpty(p.Paths.Output))
                        return false;

                    // Get the full path of the project's output directory
                    var projectOutputPath = Path.IsPathRooted(p.Paths.Output)
                        ? p.Paths.Output
                        : Path.Combine(_projectsPath, p.Paths.Output);
                    projectOutputPath = Path.GetFullPath(projectOutputPath);

                    // Compare normalized paths
                    return string.Equals(projectOutputPath, normalizedProjectFolder, StringComparison.OrdinalIgnoreCase);
                });

                List<string>? allowedPaths = null;
                if (currentProject != null)
                {
                    // Get allowed external paths directly from the project entity
                    allowedPaths = currentProject.Security.AllowedExternalPaths.ToList();
                }

                _logger.LogInformation("Dragon loading project: {ProjectName}, ExternalPaths: {Count}",
                    currentProject?.Name ?? "unknown", allowedPaths?.Count ?? 0);

                // Update Dragon's working directory and allowed external paths
                session.Dragon.UpdateProjectContext(projectFolder, allowedPaths);
                _logger.LogDebug("Updated Dragon context: WorkingDir={Folder}, ExternalPaths={Count}",
                    projectFolder, allowedPaths?.Count ?? 0);
            }

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
                var history = await DragonSession.LoadHistoryFromFileAsync(projectFolder, _logger);
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
        private static string? ExtractMessageContent(Dictionary<string, object>? data)
        {
            if (data == null) return null;

            if (data.TryGetValue("content", out var content))
                return content?.ToString();
            if (data.TryGetValue("message", out var message))
                return message?.ToString();

            return null;
        }

        /// <summary>
        /// Refreshes Dragon's context when a project's external paths change.
        /// Only updates if the modified project is the current session's project.
        /// </summary>
        private void RefreshDragonContextForProject(DragonSession session, string projectIdOrName)
        {
            if (session.Dragon == null || string.IsNullOrEmpty(session.CurrentProjectFolder))
                return;

            // Check if the modified project is the current session's project
            var allProjects = _projectService.GetAllProjects();
            var currentProject = allProjects.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Paths.Output) &&
                string.Equals(p.Paths.Output, session.CurrentProjectFolder, StringComparison.OrdinalIgnoreCase));

            if (currentProject == null)
                return;

            // Check if this update is for the current project (by ID or name)
            var isCurrentProject = string.Equals(currentProject.Id, projectIdOrName, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(currentProject.Name, projectIdOrName, StringComparison.OrdinalIgnoreCase);

            if (!isCurrentProject)
                return;

            // Get updated external paths directly from the project entity
            var allowedPaths = currentProject.Security.AllowedExternalPaths.ToList();

            // Update Dragon's context
            session.Dragon.UpdateProjectContext(session.CurrentProjectFolder, allowedPaths);
            _logger.LogDebug("Refreshed Dragon context for {Project}: {Count} external paths",
                currentProject.Name, allowedPaths?.Count ?? 0);
        }

        /// <summary>
        /// Delegates a task to a council member (sub-agent) with detailed latency tracking
        /// </summary>
        private async Task<string> DelegateToCouncilAsync(DragonSession session, string councilMember, string task)
        {
            var delegationStart = DateTime.UtcNow;
            var memberName = char.ToUpper(councilMember[0]) + councilMember.Substring(1).ToLowerInvariant();

            _logger.LogInformation("[Dragon Council] DELEGATION START {Member} | Task: {TaskPreview}",
                memberName, task.Length > 100 ? task.Substring(0, 100) + "..." : task);

            try
            {
                // For Seeker, prepend current project context including allowed external paths
                var enhancedTask = task;
                if (councilMember.ToLowerInvariant() == "seeker" && !string.IsNullOrEmpty(session.CurrentProjectFolder))
                {
                    // Find the current project to get its allowed external paths
                    var allProjects = _projectService.GetAllProjects();
                    var currentProject = allProjects.FirstOrDefault(p =>
                        !string.IsNullOrEmpty(p.Paths.Output) &&
                        string.Equals(p.Paths.Output, session.CurrentProjectFolder, StringComparison.OrdinalIgnoreCase));

                    if (currentProject != null)
                    {
                        // Get allowed external paths directly from the project entity
                        var allowedPaths = currentProject.Security.AllowedExternalPaths.ToList();

                        if (allowedPaths.Count > 0)
                        {
                            var pathsList = string.Join("\n  - ", allowedPaths);
                            enhancedTask = $"**Current Project Context:**\n" +
                                $"Project: {currentProject.Name}\n" +
                                $"Workspace: {session.CurrentProjectFolder}\n" +
                                $"**Allowed External Paths** (you can scan these directories):\n" +
                                $"  - {pathsList}\n\n" +
                                $"**User Request:**\n{task}";
                            _logger.LogDebug("[Dragon Council] Enhanced Seeker task with project context for {Project}", currentProject.Name);
                        }
                    }
                }

                string result;
                var agentStart = DateTime.UtcNow;

                result = councilMember.ToLowerInvariant() switch
                {
                    "sage" => await session.Sage!.ProcessTaskAsync(enhancedTask),
                    "seeker" => await session.Seeker!.ProcessTaskAsync(enhancedTask),
                    "sentinel" => await session.Sentinel!.ProcessTaskAsync(enhancedTask),
                    "warden" => await session.Warden!.ProcessTaskAsync(enhancedTask),
                    _ => $"Unknown council member: {councilMember}"
                };

                var agentDuration = DateTime.UtcNow - agentStart;
                var totalDuration = DateTime.UtcNow - delegationStart;

                _logger.LogInformation("[Dragon Council] DELEGATION COMPLETE {Member} | Agent: {AgentMs}ms | Total: {TotalMs}ms",
                    memberName, agentDuration.TotalMilliseconds.ToString("F0"), totalDuration.TotalMilliseconds.ToString("F0"));

                return result;
            }
            catch (Exception ex)
            {
                var totalDuration = DateTime.UtcNow - delegationStart;
                _logger.LogError(ex, "[Dragon Council] DELEGATION FAILED {Member} | Duration: {Duration}ms",
                    memberName, totalDuration.TotalMilliseconds.ToString("F0"));
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
                    await SendMessageAsync(webSocket, new { type = "pong" }, session.Sender);
                    return;
                }

                // Handle client_ready signal - send welcome message only after client is prepared
                if (message.Type == "client_ready" && !session.WelcomeSent && session.Dragon != null)
                {
                    _logger.LogInformation("[Dragon] Client ready, sending welcome message");
                    try
                    {
                        session.StreamingResponseSent = false;
                        var welcomeResponse = await session.Dragon.StartSessionAsync();
                        _logger.LogInformation("[Dragon] Welcome: {Response}", welcomeResponse);

                        _logger.LogDebug("[Dragon] Sending welcome (isStreamed={IsStreamed})", session.StreamingResponseSent);
                        await SendTrackedMessageAsync(webSocket, session, "dragon_message", new
                        {
                            type = "dragon_message",
                            sessionId,
                            message = welcomeResponse,
                            timestamp = DateTime.UtcNow,
                            isStreamed = session.StreamingResponseSent
                        });
                        session.WelcomeSent = true;

                        // Send any pending notifications (e.g., feature branches ready for merge)
                        await SendPendingNotificationsAsync(webSocket, session);
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError(ex, "Failed to start Dragon session - LLM connection error");
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
                        _logger.LogError(ex, "Failed to start Dragon session");
                        await SendTrackedMessageAsync(webSocket, session, "error", new
                        {
                            type = "error",
                            errorType = "startup_error",
                            sessionId,
                            message = $"Failed to initialize Dragon: {ex.Message}",
                            timestamp = DateTime.UtcNow
                        });
                    }
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
                TrackMessage(session, "user_message", new Dictionary<string, object> { ["role"] = "user", ["content"] = message.Message! });

                // Validate session is properly initialized before processing
                if (session.Sender == null || session.Dragon == null)
                {
                    _logger.LogWarning("Dragon session not properly initialized (Sender={Sender}, Dragon={Dragon}), cannot process message",
                        session.Sender != null, session.Dragon != null);
                    await SendMessageAsync(webSocket, new
                    {
                        type = "error",
                        error = "session_not_ready",
                        message = "Session is not ready. Please try reconnecting.",
                        timestamp = DateTime.UtcNow
                    }, session.Sender);
                    return;
                }

                await SendMessageAsync(webSocket, new { type = "dragon_typing", sessionId }, session.Sender);

                // Create a request for non-blocking processing
                var requestId = Guid.NewGuid().ToString();
                var cts = new CancellationTokenSource();

                var dragonRequest = new DraCode.KoboldLair.Server.Models.Dragon.DragonRequest
                {
                    RequestId = requestId,
                    SessionId = sessionId,
                    Message = message.Message!,
                    WebSocket = webSocket,
                    Sender = session.Sender,
                    CancellationTokenSource = cts,
                    QueuedAt = DateTime.UtcNow,
                    Dragon = session.Dragon
                };

                // Set up status callback to send progress updates via WebSocket
                dragonRequest.StatusCallback = async (statusType, statusMessage) =>
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        await SendMessageAsync(webSocket, new
                        {
                            type = "dragon_status",
                            requestId,
                            sessionId,
                            statusType,
                            message = statusMessage,
                            timestamp = DateTime.UtcNow
                        }, session.Sender);
                    }
                };

                // Set up post-response callback to check for new specifications
                dragonRequest.OnResponseCallback = async () =>
                {
                    try
                    {
                        await CheckForNewSpecifications(webSocket, session);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in CheckForNewSpecifications callback");
                    }
                };

                // Enqueue for async processing (non-blocking)
                await _requestQueue.EnqueueAsync(dragonRequest);

                _logger.LogDebug("Dragon request enqueued: {RequestId}", requestId);
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

                    // Check git status for the project
                    var gitInstalled = await _gitService.IsGitInstalledAsync();
                    var gitInitialized = gitInstalled && await _gitService.IsRepositoryAsync(projectFolder);
                    var gitStatus = gitInitialized ? "initialized" : (gitInstalled ? "not_initialized" : "not_installed");

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
                        timestamp = DateTime.UtcNow,
                        gitStatus,
                        gitInstalled,
                        gitInitialized
                    });

                    try
                    {
                        // Check if this folder already belongs to an existing project (e.g., imported project)
                        var existingProject = _projectService.GetAllProjects()
                            .FirstOrDefault(p => !string.IsNullOrEmpty(p.Paths.Output) &&
                                string.Equals(Path.GetFullPath(p.Paths.Output), Path.GetFullPath(projectFolder), StringComparison.OrdinalIgnoreCase));

                        if (existingProject != null)
                        {
                            // Link specification to existing project instead of creating a new one
                            _projectService.UpdateSpecificationPath(existingProject.Id, latestSpec);
                            _logger.LogInformation("Linked specification to existing project: {Name} ({Id})", existingProject.Name, existingProject.Id);
                        }
                        else
                        {
                            var project = _projectService.RegisterProject(projectName, latestSpec);
                            _logger.LogInformation("Auto-registered project: {Name} ({Id})", projectName, project.Id);
                        }

                        // Invalidate cache so we don't re-process this same spec file
                        InvalidateSpecFilesCache();
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

        private async Task SendThinkingUpdateAsync(WebSocket webSocket, DragonSession session, string sessionId, string eventType, string content, string? agentSource = null)
        {
            try
            {
                string? toolName = null;
                string? description = null;
                string? category = null; // Groups: "tool", "thinking", "delegation", "reading", "writing"

                if (eventType == "tool_call" && content.StartsWith("Tool: "))
                {
                    var lines = content.Split('\n', 2);
                    toolName = lines[0].Substring(6).Trim();
                    description = GetToolDescription(toolName, lines.Length > 1 ? lines[1] : null);
                    category = "tool";
                }
                else if (eventType == "tool_result" && content.StartsWith("Result from "))
                {
                    var colonIndex = content.IndexOf(':');
                    if (colonIndex > 12)
                    {
                        toolName = content.Substring(12, colonIndex - 12);
                        description = GetToolResultDescription(toolName, content.Substring(colonIndex + 1).TrimStart('\n'));
                        category = "tool";
                    }
                }
                else if (eventType == "info")
                {
                    if (content.StartsWith("ITERATION"))
                    {
                        // Extract iteration number for the client
                        var iterNum = content.Replace("ITERATION ", "").Replace(" (streaming)", "").Trim();
                        description = int.TryParse(iterNum, out var n) && n > 1
                            ? $"Thinking (round {n})..."
                            : "Thinking...";
                        category = "thinking";
                    }
                    else if (content.StartsWith("Stop reason:"))
                    {
                        description = null; // Don't surface internal stop reasons
                    }
                    else
                    {
                        description = content;
                        category = "thinking";
                    }
                }
                else if (eventType == "debug")
                {
                    // Surface LLM connection latency to help users understand waits
                    if (content.Contains("LLM connected in"))
                    {
                        description = "Waiting for AI response...";
                        category = "thinking";
                    }
                    else if (content.Contains("First token received"))
                    {
                        description = "AI is responding...";
                        category = "thinking";
                    }
                    else
                    {
                        return; // Don't surface other debug messages
                    }
                }
                else if (eventType == "warning")
                {
                    description = content;
                    category = "warning";
                }

                if (description == null) return;

                await SendMessageAsync(webSocket, new
                {
                    type = "dragon_thinking",
                    sessionId,
                    eventType,
                    toolName,
                    description,
                    category = category ?? "thinking",
                    agent = agentSource, // Which agent is active (Dragon, Sage, Seeker, etc.)
                    timestamp = DateTime.UtcNow
                }, session.Sender);
            }
            catch (WebSocketException)
            {
                // Client disconnected during processing - not an error
                _logger.LogDebug("Client disconnected during thinking update send");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending thinking update");
            }
        }

        /// <summary>
        /// Returns a human-readable description for a tool being called.
        /// </summary>
        private static string GetToolDescription(string toolName, string? inputLine)
        {
            return toolName switch
            {
                "list_projects" => "Loading projects...",
                "delegate_to_council" => GetDelegationDescription(inputLine),
                "read_file" => GetFileOperationDescription("Reading", inputLine),
                "list_files" => GetFileOperationDescription("Browsing", inputLine),
                "manage_specification" => "Working on specification...",
                "manage_features" => "Managing features...",
                "approve_specification" => "Approving project for processing...",
                "add_existing_project" => "Importing project...",
                "git_status" => "Checking git status...",
                "git_merge" => "Merging branches...",
                "manage_external_paths" => "Configuring paths...",
                "retry_analysis" => "Retrying analysis...",
                "agent_status" => "Checking agent status...",
                "retry_failed_task" => "Retrying failed task...",
                "set_task_priority" => "Updating task priority...",
                "pause_project" => "Pausing project...",
                "resume_project" => "Resuming project...",
                "suspend_project" => "Suspending project...",
                "cancel_project" => "Cancelling project...",
                "view_specification_history" => "Loading specification history...",
                "select_agent" => "Selecting agent type...",
                _ => $"Running {toolName}..."
            };
        }

        private static string GetDelegationDescription(string? inputLine)
        {
            if (string.IsNullOrEmpty(inputLine)) return "Consulting the Council...";

            // Try to extract council_member from Input JSON
            try
            {
                if (inputLine.StartsWith("Input: "))
                {
                    var json = inputLine.Substring(7);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("council_member", out var member))
                    {
                        return member.GetString()?.ToLowerInvariant() switch
                        {
                            "sage" => "Consulting Sage (specifications)...",
                            "seeker" => "Dispatching Seeker (scanning)...",
                            "sentinel" => "Alerting Sentinel (git)...",
                            "warden" => "Summoning Warden (configuration)...",
                            _ => "Consulting the Council..."
                        };
                    }
                }
            }
            catch { /* Parse failed, use default */ }

            return "Consulting the Council...";
        }

        private static string GetFileOperationDescription(string verb, string? inputLine)
        {
            if (string.IsNullOrEmpty(inputLine)) return $"{verb} files...";

            try
            {
                if (inputLine.StartsWith("Input: "))
                {
                    var json = inputLine.Substring(7);
                    using var doc = JsonDocument.Parse(json);

                    // read_file has "path", list_files has "directory"
                    if (doc.RootElement.TryGetProperty("path", out var path))
                    {
                        var filename = Path.GetFileName(path.GetString() ?? "");
                        return string.IsNullOrEmpty(filename) ? $"{verb} files..." : $"{verb} {filename}...";
                    }
                    if (doc.RootElement.TryGetProperty("directory", out var dir))
                    {
                        var dirname = Path.GetFileName((dir.GetString() ?? "").TrimEnd('/', '\\'));
                        return string.IsNullOrEmpty(dirname) ? $"{verb} directory..." : $"{verb} {dirname}/...";
                    }
                }
            }
            catch { /* Parse failed, use default */ }

            return $"{verb} files...";
        }

        /// <summary>
        /// Returns a human-readable summary of a tool result.
        /// </summary>
        private static string GetToolResultDescription(string toolName, string resultPreview)
        {
            return toolName switch
            {
                "list_projects" => "Projects loaded",
                "delegate_to_council" => "Council member responded",
                "read_file" => "File read complete",
                "list_files" => "Directory listing complete",
                "manage_specification" => "Specification updated",
                "manage_features" => "Features updated",
                "approve_specification" => "Project approved",
                "git_status" => "Git status retrieved",
                "git_merge" => "Merge complete",
                "agent_status" => "Agent status retrieved",
                _ => $"{toolName} complete"
            };
        }

        private async Task SendStreamingChunkAsync(WebSocket webSocket, DragonSession session, string sessionId, string chunk)
        {
            try
            {
                await SendMessageAsync(webSocket, new
                {
                    type = "dragon_stream",
                    sessionId,
                    chunk,
                    timestamp = DateTime.UtcNow
                }, session.Sender);
            }
            catch (WebSocketException)
            {
                // Client disconnected during streaming - not an error
                _logger.LogDebug("Client disconnected during streaming chunk send");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending streaming chunk");
            }
        }

        private async Task SendMessageAsync(WebSocket webSocket, object data, WebSocketSender? sender = null)
        {
            if (webSocket.State != WebSocketState.Open) return;

            var json = JsonSerializer.Serialize(data, s_writeOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            // Route through sender's semaphore to prevent concurrent WebSocket writes
            if (sender != null)
            {
                await sender.SendAsync(bytes);
            }
            else
            {
                try
                {
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (WebSocketException)
                {
                    // WebSocket closed between state check and send - expected during disconnect
                }
            }
        }

        /// <summary>
        /// Sends any pending project notifications to the client.
        /// Called after welcome message and project switches.
        /// </summary>
        private async Task SendPendingNotificationsAsync(WebSocket webSocket, DragonSession session)
        {
            if (_notificationService == null || string.IsNullOrEmpty(session.CurrentProjectFolder))
                return;

            try
            {
                // Get project name from active project
                var projectName = _projectService.GetAllProjects()
                    .FirstOrDefault(p =>
                    {
                        var folder = Path.Combine(_projectsPath, p.Name.ToLowerInvariant().Replace(" ", "-"));
                        return string.Equals(folder, session.CurrentProjectFolder, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(p.Name, Path.GetFileName(session.CurrentProjectFolder), StringComparison.OrdinalIgnoreCase);
                    })?.Name;

                if (string.IsNullOrEmpty(projectName))
                    return;

                var notifications = _notificationService.GetPendingNotifications(projectName);
                if (notifications.Count == 0)
                    return;

                foreach (var notification in notifications)
                {
                    await SendTrackedMessageAsync(webSocket, session, "project_notification", new
                    {
                        type = "project_notification",
                        notificationType = notification.Type,
                        sessionId = session.SessionId,
                        message = notification.Message,
                        metadata = notification.Metadata,
                        timestamp = notification.CreatedAt
                    });
                }

                // Mark as read after sending
                _notificationService.MarkAsRead(projectName);
                _logger.LogInformation("Sent {Count} pending notifications for project {Project}", notifications.Count, projectName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending pending notifications");
            }
        }

        private async Task SendTrackedMessageAsync(WebSocket webSocket, DragonSession session, string messageType, object data)
        {
            var messageId = Guid.NewGuid().ToString();

            // Serialize to JSON, parse back as dictionary for uniform storage
            var json = JsonSerializer.Serialize(data, s_writeOptions);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, s_readOptions)
                       ?? new Dictionary<string, object>();
            dict["messageId"] = messageId;

            TrackMessage(session, messageType, dict, messageId);

            // Re-serialize with messageId included
            var finalJson = JsonSerializer.Serialize(dict, s_writeOptions);
            var bytes = Encoding.UTF8.GetBytes(finalJson);

            // Route through sender's semaphore to prevent concurrent WebSocket writes
            if (session.Sender != null)
            {
                await session.Sender.SendAsync(bytes);
            }
            else if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (WebSocketException)
                {
                    // WebSocket closed between state check and send - expected during disconnect
                }
            }
        }

        private void TrackMessage(DragonSession session, string messageType, Dictionary<string, object> data, string? messageId = null)
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

            // Persist history — prefer SQL (serialized, no race) with file as fallback
            if (!string.IsNullOrEmpty(session.CurrentProjectFolder))
            {
                if (_historyRepository != null)
                {
                    // SQL write is serialized via semaphore — safe for concurrent messages
                    List<SessionMessage> snapshot;
                    lock (session._historyLock)
                    {
                        snapshot = new List<SessionMessage>(session.MessageHistory);
                    }
                    _ = _historyRepository.SaveHistoryAsync(session.CurrentProjectFolder, snapshot);
                }
                else
                {
                    // Fallback: fire-and-forget file save (legacy, has race condition)
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
                    if (type == "tool_call" || type == "tool_result" || type == "info" || type == "debug" || type == "warning")
                        _ = SendThinkingUpdateAsync(webSocket, session, sessionId, type, content, agentSource: "Dragon");
                    else if (type == "assistant_stream")
                        _ = SendStreamingChunkAsync(webSocket, session, sessionId, content);
                    else if (type == "assistant_final")
                    {
                        // Mark that streaming occurred - final message sent by awaited path below
                        session.StreamingResponseSent = true;
                    }
                };

                // Create named callbacks for each council member
                Action<string, string> MakeReloadCouncilCallback(string memberName) => (type, content) =>
                {
                    if (type == "tool_call" || type == "tool_result" || type == "info" || type == "debug" || type == "warning")
                        _ = SendThinkingUpdateAsync(webSocket, session, sessionId, type, content, agentSource: memberName);
                    else if (type == "assistant_final")
                        // Forward council member final responses so client knows they're done
                        _ = SendThinkingUpdateAsync(webSocket, session, sessionId, type, content, agentSource: memberName);
                };

                session.Dragon!.SetMessageCallback(dragonCallback);
                session.Sage!.SetMessageCallback(MakeReloadCouncilCallback("Sage"));
                session.Seeker!.SetMessageCallback(MakeReloadCouncilCallback("Seeker"));
                session.Sentinel!.SetMessageCallback(MakeReloadCouncilCallback("Sentinel"));
                session.Warden!.SetMessageCallback(MakeReloadCouncilCallback("Warden"));

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

        private async Task<List<ProjectInfo>> GetProjectInfoListAsync()
        {
            var projects = _projectService.GetAllProjects();
            var result = new List<ProjectInfo>();
            foreach (var p in projects)
            {
                var hasGit = !string.IsNullOrEmpty(p.Paths.Output) &&
                    await _gitService.IsRepositoryAsync(p.Paths.Output);
                result.Add(new ProjectInfo
                {
                    Id = p.Id,
                    Name = p.Name,
                    Status = p.Status.ToString(),
                    ExecutionState = p.ExecutionState.ToString(),
                    FeatureCount = GetFeatureCountForProject(p),
                    PendingFeatureCount = GetPendingFeatureCountForProject(p),
                    CreatedAt = p.Timestamps.CreatedAt,
                    UpdatedAt = p.Timestamps.UpdatedAt,
                    HasGitRepository = hasGit,
                    AllowedExternalPaths = p.Security.AllowedExternalPaths.ToList()
                });
            }
            return result;
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
        /// Gets the count of pending features (status: Draft or legacy New) for a project.
        /// These are features that have been added but not yet marked Ready for Wyvern processing.
        /// </summary>
        private int GetPendingFeatureCountForProject(Project project)
        {
            if (string.IsNullOrEmpty(project.Paths.Output))
                return 0;

            try
            {
                var featuresPath = Path.Combine(project.Paths.Output, "specification.features.json");
                if (!File.Exists(featuresPath))
                    return 0;

                var json = File.ReadAllText(featuresPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                // Handle both wrapped format (with "features" property) and legacy format (direct array)
                var featuresElement = doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("features", out var featsProp)
                    ? featsProp
                    : doc.RootElement;

                if (featuresElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                    return 0;

                int pendingCount = 0;
                foreach (var feature in featuresElement.EnumerateArray())
                {
                    if (feature.TryGetProperty("Status", out var statusProp) &&
                        statusProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var status = statusProp.GetString();
                        // Count Draft and legacy New as pending
                        if (status == "Draft" || status == "New")
                        {
                            pendingCount++;
                        }
                    }
                }

                return pendingCount;
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
        /// <summary>
        /// Deletes a project from the registry. Optionally removes project files from disk.
        /// </summary>
        private bool DeleteProjectFromRegistry(string projectId, bool deleteFiles)
        {
            try
            {
                var project = _projectService.GetProject(projectId);
                if (project == null) return false;

                if (deleteFiles)
                {
                    // Delete the project folder from disk
                    var folder = _projectService.CreateProjectFolder(project.Name);
                    if (Directory.Exists(folder))
                    {
                        try
                        {
                            Directory.Delete(folder, recursive: true);
                            _logger.LogInformation("Deleted project files: {Folder}", folder);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete project files for {Project}", project.Name);
                        }
                    }
                }

                _projectRepository.Delete(projectId);
                _logger.LogInformation("Project {Name} ({Id}) deleted from registry (files: {DeleteFiles})", project.Name, projectId, deleteFiles);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {Id}", projectId);
                return false;
            }
        }

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
            _projectService.UpdateProjectStatus(project.Id, ProjectStatus.Completed);
            _logger.LogInformation("Verification skipped for project: {Project} - marked as Completed", projectIdOrName);
            return true;
        }
    }
}
