using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.KoboldTown.Agents;

namespace DraCode.KoboldTown.Services
{
    /// <summary>
    /// Service for handling Dragon agent interactions via WebSocket.
    /// Dragon gathers project requirements from users and creates specifications.
    /// </summary>
    public class DragonService
    {
        private readonly ILogger<DragonService> _logger;
        private readonly Dictionary<string, DragonAgent> _activeSessions;
        private readonly string _defaultProvider;
        private readonly Dictionary<string, string> _defaultConfig;
        private readonly ProjectService? _projectService;

        public DragonService(
            ILogger<DragonService> logger,
            string defaultProvider = "openai",
            Dictionary<string, string>? defaultConfig = null,
            ProjectService? projectService = null)
        {
            _logger = logger;
            _activeSessions = new Dictionary<string, DragonAgent>();
            _defaultProvider = defaultProvider;
            _defaultConfig = defaultConfig ?? new Dictionary<string, string>();
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
                // Create Dragon agent for this session
                var provider = CreateProvider();
                var options = new AgentOptions
                {
                    WorkingDirectory = "./workspace",
                    Verbose = false
                };

                var dragon = new DragonAgent(provider, options);
                _activeSessions[sessionId] = dragon;

                // Send welcome message
                var welcomeResponse = await dragon.StartSessionAsync();
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
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            CancellationToken.None);
                        break;
                    }

                    var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
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
                var message = JsonSerializer.Deserialize<DragonMessage>(messageText);
                if (message == null)
                {
                    _logger.LogWarning("Invalid message format");
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
                var response = await dragon.ContinueSessionAsync(message.Message);

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
            var json = JsonSerializer.Serialize(data);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }

        /// <summary>
        /// Creates an LLM provider for Dragon
        /// </summary>
        private ILlmProvider CreateProvider()
        {
            return _defaultProvider.ToLower() switch
            {
                "openai" => new OpenAiProvider(
                    _defaultConfig.GetValueOrDefault("apiKey", Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? ""),
                    _defaultConfig.GetValueOrDefault("model", "gpt-4o")
                ),
                "anthropic" => new ClaudeProvider(
                    _defaultConfig.GetValueOrDefault("apiKey", Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? ""),
                    _defaultConfig.GetValueOrDefault("model", "claude-sonnet-4.5")
                ),
                "azure" => new AzureOpenAiProvider(
                    _defaultConfig.GetValueOrDefault("endpoint", Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? ""),
                    _defaultConfig.GetValueOrDefault("apiKey", Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? ""),
                    _defaultConfig.GetValueOrDefault("deployment", "gpt-4")
                ),
                _ => throw new ArgumentException($"Unknown provider: {_defaultProvider}")
            };
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
        public string Message { get; set; } = "";
        public string? SessionId { get; set; }
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
