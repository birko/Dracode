using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DraCode.Agent;
using DraCode.KoboldLair.Agents;
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
    /// Tracks session state including agent, last activity, and message history
    /// </summary>
    public class DragonSession
    {
        public string SessionId { get; init; } = "";
        public DragonAgent? Agent { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public List<SessionMessage> MessageHistory { get; } = new();
        public string? LastMessageId { get; set; }
    }

    /// <summary>
    /// Service for handling Dragon agent interactions via WebSocket.
    /// Dragon gathers project requirements from users and creates specifications.
    /// </summary>
    public class DragonService : IDisposable
    {
        private readonly ILogger<DragonService> _logger;
        private readonly ConcurrentDictionary<string, DragonSession> _sessions;
        private readonly ConcurrentDictionary<string, WebSocket> _sessionWebSockets;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly ProjectService? _projectService;
        private readonly string _projectsPath;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(10);
        private readonly int _maxMessageHistory = 100;
        private bool _disposed;

        public DragonService(
            ILogger<DragonService> logger,
            ProviderConfigurationService providerConfigService,
            ProjectService? projectService = null,
            string projectsPath = "./projects")
        {
            _logger = logger;
            _sessions = new ConcurrentDictionary<string, DragonSession>();
            _sessionWebSockets = new ConcurrentDictionary<string, WebSocket>();
            _providerConfigService = providerConfigService;
            _projectService = projectService;
            _projectsPath = projectsPath;

            // Start cleanup timer to remove expired sessions every minute
            _cleanupTimer = new Timer(CleanupExpiredSessions, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Cleanup expired sessions that haven't had activity within the timeout period
        /// </summary>
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
                    _logger.LogInformation("Cleaned up expired session: {SessionId} (inactive for {Minutes} minutes)",
                        sessionId, (DateTime.UtcNow - session.LastActivity).TotalMinutes);
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

        /// <summary>
        /// Handles WebSocket connection for Dragon chat
        /// </summary>
        /// <param name="webSocket">The WebSocket connection</param>
        /// <param name="existingSessionId">Optional session ID to resume an existing session</param>
        public async Task HandleWebSocketAsync(WebSocket webSocket, string? existingSessionId = null)
        {
            DragonSession? session = null;
            var isResuming = false;

            // Try to resume existing session if sessionId provided
            if (!string.IsNullOrEmpty(existingSessionId) && _sessions.TryGetValue(existingSessionId, out session))
            {
                // Check if session hasn't expired
                if (DateTime.UtcNow - session.LastActivity <= _sessionTimeout)
                {
                    isResuming = true;
                    session.LastActivity = DateTime.UtcNow;
                    _sessionWebSockets[existingSessionId] = webSocket;
                    _logger.LogInformation("Dragon session resumed: {SessionId}", existingSessionId);
                }
                else
                {
                    // Session expired, will create new one
                    _sessions.TryRemove(existingSessionId, out _);
                    session = null;
                    _logger.LogInformation("Session {SessionId} expired, creating new session", existingSessionId);
                }
            }

            // Create new session if not resuming
            if (session == null)
            {
                var sessionId = Guid.NewGuid().ToString();
                session = new DragonSession { SessionId = sessionId };
                _sessions[sessionId] = session;
                _sessionWebSockets[sessionId] = webSocket;
                _logger.LogInformation("Dragon session started: {SessionId}", sessionId);
            }

            var currentSessionId = session.SessionId;

            try
            {
                DragonAgent dragon;

                if (isResuming && session.Agent != null)
                {
                    // Use existing agent with conversation history
                    dragon = session.Agent;
                    _logger.LogInformation("[Dragon] Resuming existing conversation for session {SessionId}", currentSessionId);

                    // Send session_resumed message with history replay info
                    await SendTrackedMessageAsync(webSocket, session, "session_resumed", new
                    {
                        type = "session_resumed",
                        sessionId = currentSessionId,
                        messageCount = session.MessageHistory.Count,
                        lastMessageId = session.LastMessageId,
                        timestamp = DateTime.UtcNow
                    });

                    // Replay message history for client to catch up
                    foreach (var msg in session.MessageHistory)
                    {
                        // Send with replay flag so client knows these are historical
                        var replayData = new Dictionary<string, object>
                        {
                            ["type"] = msg.Type,
                            ["messageId"] = msg.MessageId,
                            ["isReplay"] = true,
                            ["sessionId"] = currentSessionId,
                            ["timestamp"] = msg.Timestamp
                        };

                        // Merge in original data properties
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
                    // Get provider settings from configuration
                    var (provider, config, options) = _providerConfigService.GetProviderSettingsForAgent("dragon");

                    // Create callback for spec updates that notifies ProjectService
                    Action<string>? onSpecUpdated = _projectService != null
                        ? specPath => _projectService.MarkSpecificationModified(specPath)
                        : null;

                    dragon = CreateDragonAgent(provider, options, config, onSpecUpdated);

                    // Set up message callback for debugging
                    dragon.SetMessageCallback((type, content) =>
                    {
                        _logger.LogInformation("[Dragon Agent] [{Type}] {Content}", type, content);
                    });

                    session.Agent = dragon;

                    // Send welcome message
                    _logger.LogInformation("[Dragon] Starting session...");
                    try
                    {
                        var welcomeResponse = await dragon.StartSessionAsync();
                        _logger.LogInformation("[Dragon] Welcome response: {Response}", welcomeResponse);

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
                            details = "Please check that your API key is valid and the provider service is available.",
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
                            details = "There was a problem starting the requirements agent. Please check provider configuration.",
                            timestamp = DateTime.UtcNow
                        });
                    }
                }

                // Handle incoming messages
                var buffer = new byte[1024 * 4];
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("WebSocket close received for session {SessionId}", currentSessionId);
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            CancellationToken.None);
                        break;
                    }

                    var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogDebug("Dragon received message ({Count} bytes): {Message}", result.Count, messageText);
                    session.LastActivity = DateTime.UtcNow;
                    await HandleMessageAsync(webSocket, session, messageText, dragon);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Dragon WebSocket session {SessionId}", currentSessionId);
            }
            finally
            {
                // Don't remove session on disconnect - keep it for potential reconnection
                // Session will be cleaned up by the cleanup timer after timeout
                _sessionWebSockets.TryRemove(currentSessionId, out _);
                _logger.LogInformation("Dragon WebSocket disconnected (session preserved): {SessionId}", currentSessionId);
            }
        }

        /// <summary>
        /// Handles individual messages from the user
        /// </summary>
        private async Task HandleMessageAsync(
            WebSocket webSocket,
            DragonSession session,
            string messageText,
            DragonAgent dragon)
        {
            var sessionId = session.SessionId;

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var message = JsonSerializer.Deserialize<DragonMessage>(messageText, options);
                if (message == null)
                {
                    _logger.LogWarning("Invalid message format");
                    return;
                }

                // Handle ping/pong for keep-alive (client may send type or action)
                var messageType = message.Type ?? message.Action;
                if (messageType?.Equals("ping", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await SendMessageAsync(webSocket, new { type = "pong" });
                    return;
                }

                // Check for reload command
                if (message.Type == "reload")
                {
                    await ReloadAgentAsync(session, message.Provider);
                    return;
                }

                _logger.LogInformation("Dragon received: {Message}", message.Message);

                // Track user message in history (for context on reconnect)
                TrackMessage(session, "user_message", new { role = "user", content = message.Message });

                // Send typing indicator (not tracked - ephemeral)
                await SendMessageAsync(webSocket, new
                {
                    type = "dragon_typing",
                    sessionId
                });

                // Get Dragon's response
                _logger.LogInformation("[Dragon] Processing user message...");
                var response = await dragon.ContinueSessionAsync(message.Message);
                _logger.LogInformation("[Dragon] Response: {Response}", response);

                // Send response with message ID for deduplication
                await SendTrackedMessageAsync(webSocket, session, "dragon_message", new
                {
                    type = "dragon_message",
                    sessionId,
                    message = response,
                    timestamp = DateTime.UtcNow
                });

                // Check if specification was created - look in consolidated project folders
                var projectsDir = _projectsPath;
                if (Directory.Exists(projectsDir))
                {
                    // Find most recently modified specification.md in any project folder
                    var specFiles = Directory.GetDirectories(projectsDir)
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
                            // Specification was just created
                            var projectFolder = Path.GetDirectoryName(latestSpec)!;
                            var projectName = Path.GetFileName(projectFolder);

                            await SendTrackedMessageAsync(webSocket, session, "specification_created", new
                            {
                                type = "specification_created",
                                sessionId,
                                filename = "specification.md",
                                path = latestSpec,
                                projectFolder = projectFolder,
                                timestamp = DateTime.UtcNow
                            });

                            // Register project with ProjectService if available
                            if (_projectService != null)
                            {
                                try
                                {
                                    var project = _projectService.RegisterProject(projectName, latestSpec);
                                    _logger.LogInformation("✨ Auto-registered project: {ProjectName} (ID: {ProjectId})",
                                        projectName, project.Id);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to auto-register project for spec: {SpecPath}", latestSpec);
                                }
                            }
                        }
                    }
                }
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
                    details = "Please check that your API key is valid and the provider service is available.",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Timeout communicating with LLM provider");
                await SendTrackedMessageAsync(webSocket, session, "error", new
                {
                    type = "error",
                    errorType = "llm_timeout",
                    sessionId,
                    message = "Request to LLM provider timed out",
                    details = "The AI service is taking too long to respond. Please try again.",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error in LLM response");
                await SendTrackedMessageAsync(webSocket, session, "error", new
                {
                    type = "error",
                    errorType = "llm_response",
                    sessionId,
                    message = "Invalid response from LLM provider",
                    details = $"Could not parse the AI response: {ex.Message}",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Dragon message");

                // Check if it's an LLM-related error by examining the message
                var isLlmError = ex.Message.Contains("API", StringComparison.OrdinalIgnoreCase) ||
                                 ex.Message.Contains("provider", StringComparison.OrdinalIgnoreCase) ||
                                 ex.Message.Contains("401") || ex.Message.Contains("403") ||
                                 ex.Message.Contains("429") || ex.Message.Contains("500");

                await SendTrackedMessageAsync(webSocket, session, "error", new
                {
                    type = "error",
                    errorType = isLlmError ? "llm_error" : "general",
                    sessionId,
                    message = isLlmError
                        ? $"LLM Provider Error: {ex.Message}"
                        : "An unexpected error occurred",
                    details = isLlmError
                        ? "There was a problem communicating with the AI service. Check your API key and provider settings."
                        : ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Sends a message through WebSocket
        /// </summary>
        private async Task SendMessageAsync(WebSocket webSocket, object data)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            var json = JsonSerializer.Serialize(data, options);
            _logger.LogDebug("Dragon sending message: {Message}", json);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }

        /// <summary>
        /// Sends a message through WebSocket and tracks it in session history with a unique messageId
        /// </summary>
        private async Task SendTrackedMessageAsync(WebSocket webSocket, DragonSession session, string messageType, object data)
        {
            var messageId = Guid.NewGuid().ToString();

            // Add messageId to the data
            var trackedData = new Dictionary<string, object>
            {
                ["messageId"] = messageId
            };

            // Merge in the original data using reflection or serialization
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            var dataJson = JsonSerializer.Serialize(data, jsonOptions);
            var dataDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson, jsonOptions);
            if (dataDict != null)
            {
                foreach (var kvp in dataDict)
                {
                    trackedData[kvp.Key] = kvp.Value;
                }
            }

            // Track in session history
            TrackMessage(session, messageType, trackedData, messageId);

            // Send the message
            await SendMessageAsync(webSocket, trackedData);
        }

        /// <summary>
        /// Tracks a message in session history for replay on reconnect
        /// </summary>
        private void TrackMessage(DragonSession session, string messageType, object data, string? messageId = null)
        {
            messageId ??= Guid.NewGuid().ToString();
            var timestamp = DateTime.UtcNow;

            session.MessageHistory.Add(new SessionMessage(messageId, messageType, data, timestamp));
            session.LastMessageId = messageId;

            // Trim history if it exceeds max
            while (session.MessageHistory.Count > _maxMessageHistory)
            {
                session.MessageHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Reloads a Dragon agent - clears context and reloads provider settings
        /// </summary>
        private async Task ReloadAgentAsync(DragonSession session, string? providerOverride = null)
        {
            var sessionId = session.SessionId;
            _logger.LogInformation("Reloading Dragon agent for session: {SessionId} with provider: {Provider}",
                sessionId, providerOverride ?? "default");

            if (!_sessionWebSockets.TryGetValue(sessionId, out var webSocket))
            {
                _logger.LogWarning("WebSocket not found for session: {SessionId}", sessionId);
                return;
            }

            try
            {
                // Get fresh provider settings from configuration
                string providerName;
                Dictionary<string, string> config;
                AgentOptions options;

                if (!string.IsNullOrEmpty(providerOverride))
                {
                    // Use the specified provider
                    _providerConfigService.SetProviderForAgent("dragon", providerOverride);
                    (providerName, config, options) = _providerConfigService.GetProviderSettingsForAgent("dragon");
                }
                else
                {
                    // Use configured provider
                    (providerName, config, options) = _providerConfigService.GetProviderSettingsForAgent("dragon");
                }


                // Create callback for spec updates that notifies ProjectService
                Action<string>? onSpecUpdated = _projectService != null
                    ? specPath => _projectService.MarkSpecificationModified(specPath)
                    : null;

                // Create new agent with clean context
                var dragon = CreateDragonAgent(providerName, options, config, onSpecUpdated);

                // Set up message callback for debugging
                dragon.SetMessageCallback((type, content) =>
                {
                    _logger.LogInformation("[Dragon Agent] [{Type}] {Content}", type, content);
                });

                session.Agent = dragon;
                // Clear message history on reload
                session.MessageHistory.Clear();
                session.LastMessageId = null;

                // Send welcome message from new agent
                _logger.LogInformation("[Dragon] Reloading session - starting fresh...");
                var welcomeResponse = await dragon.StartSessionAsync();
                _logger.LogInformation("[Dragon] Reload welcome response: {Response}", welcomeResponse);

                await SendTrackedMessageAsync(webSocket, session, "dragon_reloaded", new
                {
                    type = "dragon_reloaded",
                    sessionId,
                    message = $"Agent reloaded with provider: {providerName}\n\n{welcomeResponse}",
                    timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("Dragon agent reloaded successfully: {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading Dragon agent for session: {SessionId}", sessionId);
                await SendTrackedMessageAsync(webSocket, session, "error", new
                {
                    type = "error",
                    sessionId,
                    message = $"Failed to reload agent: {ex.Message}",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Gets statistics about active Dragon sessions
        /// </summary>
        public DragonStatistics GetStatistics()
        {
            // Count specifications in consolidated project folders
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
        /// Creates a DragonAgent with the specification update callback and project listing
        /// </summary>
        private DragonAgent CreateDragonAgent(
            string provider,
            AgentOptions options,
            Dictionary<string, string> config,
            Action<string>? onSpecificationUpdated)
        {
            var llmProvider = KoboldLairAgentFactory.CreateLlmProvider(provider, config);

            // Create project listing function if ProjectService is available
            Func<List<ProjectInfo>>? getProjects = _projectService != null
                ? () => GetProjectInfoList()
                : null;

            // Create project approval function if ProjectService is available
            Func<string, bool>? approveProject = _projectService != null
                ? (projectName) => _projectService.ApproveProject(projectName)
                : null;

            // Create existing project registration function if ProjectService is available
            Func<string, string, string?>? registerExistingProject = _projectService != null
                ? (name, sourcePath) => _projectService.RegisterExistingProject(name, sourcePath)
                : null;

            // Create project folder callback for consolidated structure
            // This ensures the project folder exists before the specification is saved
            Func<string, string>? getProjectFolder = _projectService != null
                ? (projectName) => _projectService.CreateProjectFolder(projectName)
                : null;

            return new DragonAgent(llmProvider, options, onSpecificationUpdated, getProjects, approveProject, registerExistingProject, getProjectFolder, _projectsPath);
        }

        /// <summary>
        /// Gets project information for the list_projects tool
        /// </summary>
        private List<ProjectInfo> GetProjectInfoList()
        {
            if (_projectService == null)
                return new List<ProjectInfo>();

            var projects = _projectService.GetAllProjects();
            return projects.Select(p => new ProjectInfo
            {
                Id = p.Id,
                Name = p.Name,
                Status = p.Status.ToString(),
                FeatureCount = p.Specification?.Features.Count ?? 0,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();
        }
    }
}
