using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.KoboldLair.Server.Agents;
using DraCode.KoboldLair.Server.Agents.Dragon;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Service for handling Dragon agent interactions via WebSocket.
    /// Dragon gathers project requirements from users and creates specifications.
    /// </summary>
    public class DragonService
    {
        private readonly ILogger<DragonService> _logger;
        private readonly Dictionary<string, DragonAgent> _activeSessions;
        private readonly Dictionary<string, WebSocket> _sessionWebSockets;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly ProjectService? _projectService;

        public DragonService(
            ILogger<DragonService> logger,
            ProviderConfigurationService providerConfigService,
            ProjectService? projectService = null)
        {
            _logger = logger;
            _activeSessions = new Dictionary<string, DragonAgent>();
            _sessionWebSockets = new Dictionary<string, WebSocket>();
            _providerConfigService = providerConfigService;
            _projectService = projectService;
        }

        /// <summary>
        /// Handles WebSocket connection for Dragon chat
        /// </summary>
        public async Task HandleWebSocketAsync(WebSocket webSocket)
        {
            var sessionId = Guid.NewGuid().ToString();
            _logger.LogInformation("Dragon session started: {SessionId}", sessionId);

            try
            {
                // Get provider settings from configuration
                var (provider, config, options) = _providerConfigService.GetProviderSettingsForAgent("dragon");
                var llmProvider = KoboldLairAgentFactory.CreateLlmProvider(provider, config);
                
                var dragon = new DragonAgent(llmProvider, options);
                
                // Set up message callback for debugging
                dragon.SetMessageCallback((type, content) =>
                {
                    _logger.LogInformation("[Dragon Agent] [{Type}] {Content}", type, content);
                });
                
                _activeSessions[sessionId] = dragon;
                _sessionWebSockets[sessionId] = webSocket;

                // Send welcome message
                _logger.LogInformation("[Dragon] Starting session...");
                var welcomeResponse = await dragon.StartSessionAsync();
                _logger.LogInformation("[Dragon] Welcome response: {Response}", welcomeResponse);
                
                await SendMessageAsync(webSocket, new
                {
                    type = "dragon_message",
                    sessionId,
                    message = welcomeResponse,
                    timestamp = DateTime.UtcNow
                });

                // Handle incoming messages
                var buffer = new byte[1024 * 4];
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("WebSocket close received for session {SessionId}", sessionId);
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            CancellationToken.None);
                        break;
                    }

                    var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogDebug("Dragon received message ({Count} bytes): {Message}", result.Count, messageText);
                    await HandleMessageAsync(webSocket, sessionId, messageText, dragon);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Dragon WebSocket session {SessionId}", sessionId);
            }
            finally
            {
                _activeSessions.Remove(sessionId);
                _sessionWebSockets.Remove(sessionId);
                _logger.LogInformation("Dragon session ended: {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Handles individual messages from the user
        /// </summary>
        private async Task HandleMessageAsync(
            WebSocket webSocket,
            string sessionId,
            string messageText,
            DragonAgent dragon)
        {
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

                // Handle ping/pong for keep-alive
                if (message.Type == "ping")
                {
                    await SendMessageAsync(webSocket, new { type = "pong" });
                    return;
                }

                // Check for reload command
                if (message.Type == "reload")
                {
                    await ReloadAgentAsync(sessionId, message.Provider);
                    return;
                }

                _logger.LogInformation("Dragon received: {Message}", message.Message);

                // Send typing indicator
                await SendMessageAsync(webSocket, new
                {
                    type = "dragon_typing",
                    sessionId
                });

                // Get Dragon's response
                _logger.LogInformation("[Dragon] Processing user message...");
                var response = await dragon.ContinueSessionAsync(message.Message);
                _logger.LogInformation("[Dragon] Response: {Response}", response);

                // Send response
                await SendMessageAsync(webSocket, new
                {
                    type = "dragon_message",
                    sessionId,
                    message = response,
                    timestamp = DateTime.UtcNow
                });

                // Check if specification was created
                var specPath = dragon.SpecificationsPath;
                if (Directory.Exists(specPath))
                {
                    var latestSpec = Directory.GetFiles(specPath, "*.md")
                        .OrderByDescending(File.GetLastWriteTime)
                        .FirstOrDefault();

                    if (latestSpec != null)
                    {
                        var specInfo = new FileInfo(latestSpec);
                        if ((DateTime.UtcNow - specInfo.LastWriteTime).TotalSeconds < 5)
                        {
                            // Specification was just created
                            await SendMessageAsync(webSocket, new
                            {
                                type = "specification_created",
                                sessionId,
                                filename = Path.GetFileName(latestSpec),
                                path = latestSpec,
                                timestamp = DateTime.UtcNow
                            });

                            // Register project with ProjectService if available
                            if (_projectService != null)
                            {
                                try
                                {
                                    var projectName = Path.GetFileNameWithoutExtension(latestSpec);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Dragon message");
                await SendMessageAsync(webSocket, new
                {
                    type = "error",
                    sessionId,
                    message = "Sorry, I encountered an error. Please try again.",
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
        /// Reloads a Dragon agent - clears context and reloads provider settings
        /// </summary>
        public async Task ReloadAgentAsync(string sessionId, string? providerOverride = null)
        {
            _logger.LogInformation("Reloading Dragon agent for session: {SessionId} with provider: {Provider}", 
                sessionId, providerOverride ?? "default");

            if (!_activeSessions.TryGetValue(sessionId, out var oldAgent))
            {
                _logger.LogWarning("Session not found for reload: {SessionId}", sessionId);
                return;
            }

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
                
                var llmProvider = KoboldLairAgentFactory.CreateLlmProvider(providerName, config);
                
                // Create new agent with clean context
                var dragon = new DragonAgent(llmProvider, options);
                
                // Set up message callback for debugging
                dragon.SetMessageCallback((type, content) =>
                {
                    _logger.LogInformation("[Dragon Agent] [{Type}] {Content}", type, content);
                });
                
                _activeSessions[sessionId] = dragon;

                // Send welcome message from new agent
                _logger.LogInformation("[Dragon] Reloading session - starting fresh...");
                var welcomeResponse = await dragon.StartSessionAsync();
                _logger.LogInformation("[Dragon] Reload welcome response: {Response}", welcomeResponse);
                
                await SendMessageAsync(webSocket, new
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
                await SendMessageAsync(webSocket, new
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
            return new DragonStatistics
            {
                ActiveSessions = _activeSessions.Count,
                TotalSpecifications = Directory.Exists("./specifications") 
                    ? Directory.GetFiles("./specifications", "*.md").Length 
                    : 0
            };
        }
    }

    /// <summary>
    /// Message format from Dragon frontend
    /// </summary>
    public class DragonMessage
    {
        public string? Type { get; set; }
        public string Message { get; set; } = "";
        public string? SessionId { get; set; }
        public string? Provider { get; set; }
    }

    /// <summary>
    /// Statistics about Dragon service
    /// </summary>
    public class DragonStatistics
    {
        public int ActiveSessions { get; set; }
        public int TotalSpecifications { get; set; }

        public override string ToString()
        {
            return $"Dragon Stats: {ActiveSessions} active sessions, {TotalSpecifications} specifications created";
        }
    }
}
