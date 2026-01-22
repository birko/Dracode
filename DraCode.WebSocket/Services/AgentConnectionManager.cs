using DraCode.Agent;
using DraCode.Agent.Agents;
using DraCode.WebSocket.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DraCode.WebSocket.Services
{
    public class AgentConnectionManager
    {
        // Changed to support multiple agents per connection: key = "connectionId:agentId"
        private readonly ConcurrentDictionary<string, AgentConnection> _agents = new();
        private readonly ILogger<AgentConnectionManager> _logger;
        private readonly AgentConfiguration _config;

        public AgentConnectionManager(ILogger<AgentConnectionManager> logger, IOptions<AgentConfiguration> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        public async Task HandleWebSocketAsync(System.Net.WebSockets.WebSocket webSocket, string connectionId)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult? result = null;

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await DisconnectAllAgentsForConnection(connectionId, webSocket);
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessageAsync(connectionId, message, webSocket);
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocket error for connection {ConnectionId}", connectionId);
            }
            finally
            {
                await DisconnectAllAgentsForConnection(connectionId, webSocket);
            }
        }

        private string GetAgentKey(string connectionId, string agentId) => $"{connectionId}:{agentId}";

        private async Task ProcessMessageAsync(string connectionId, string message, System.Net.WebSockets.WebSocket webSocket)
        {
            try
            {
                var request = JsonSerializer.Deserialize<WebSocketMessage>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null)
                {
                    await SendResponseAsync(webSocket, new WebSocketResponse
                    {
                        Status = "error",
                        Error = "Invalid message format"
                    });
                    return;
                }

                switch (request.Command?.ToLowerInvariant())
                {
                    case "list":
                        await HandleListAsync(webSocket, request);
                        break;

                    case "connect":
                        await HandleConnectAsync(connectionId, webSocket, request);
                        break;

                    case "disconnect":
                        await HandleDisconnectAsync(connectionId, webSocket, request);
                        break;

                    case "reset":
                        await HandleResetAsync(connectionId, webSocket, request);
                        break;

                    case "send":
                        await HandleSendAsync(connectionId, webSocket, request);
                        break;

                    case "prompt_response":
                        await HandlePromptResponseAsync(connectionId, webSocket, request);
                        break;

                    default:
                        await SendResponseAsync(webSocket, new WebSocketResponse
                        {
                            Status = "error",
                            Error = $"Unknown command: {request.Command}"
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message for connection {ConnectionId}", connectionId);
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = ex.Message
                });
            }
        }

        private async Task HandleListAsync(System.Net.WebSockets.WebSocket webSocket, WebSocketMessage request)
        {
            try
            {
                var providers = new List<object>();

                foreach (var provider in _config.Providers)
                {
                    var providerInfo = new Dictionary<string, object>
                    {
                        ["name"] = provider.Key,
                        ["type"] = provider.Value.Type ?? provider.Key,
                        ["configured"] = IsProviderConfigured(provider.Value)
                    };

                    // Add model info if available
                    if (!string.IsNullOrEmpty(provider.Value.Model))
                    {
                        providerInfo["model"] = provider.Value.Model;
                    }

                    // Add endpoint info for Azure
                    if (provider.Value.Type == "azureopenai" && !string.IsNullOrEmpty(provider.Value.Deployment))
                    {
                        providerInfo["deployment"] = provider.Value.Deployment;
                    }

                    providers.Add(providerInfo);
                }

                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "success",
                    Message = $"Found {providers.Count} configured provider(s)",
                    Data = JsonSerializer.Serialize(providers, new JsonSerializerOptions { WriteIndented = true }),
                    AgentId = request.AgentId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing providers");
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = $"Failed to list providers: {ex.Message}"
                });
            }
        }

        private bool IsProviderConfigured(ProviderConfig config)
        {
            // Check if provider has required configuration
            if (config.Type == "ollama")
            {
                return true; // Ollama doesn't require API key
            }

            // Check for API key or token
            var hasApiKey = !string.IsNullOrEmpty(config.ApiKey) &&
                           !config.ApiKey.StartsWith("${") &&
                           !config.ApiKey.EndsWith("}");

            var hasClientId = !string.IsNullOrEmpty(config.ClientId) &&
                             !config.ClientId.StartsWith("${") &&
                             !config.ClientId.EndsWith("}");

            return hasApiKey || hasClientId;
        }

        private async Task HandleConnectAsync(string connectionId, System.Net.WebSockets.WebSocket webSocket, WebSocketMessage request)
        {
            try
            {
                // Require agentId for multi-agent support
                if (string.IsNullOrEmpty(request.AgentId))
                {
                    await SendResponseAsync(webSocket, new WebSocketResponse
                    {
                        Status = "error",
                        Error = "AgentId is required for connect command"
                    });
                    return;
                }

                var agentKey = GetAgentKey(connectionId, request.AgentId);

                // Check if agent already exists
                if (_agents.ContainsKey(agentKey))
                {
                    await SendResponseAsync(webSocket, new WebSocketResponse
                    {
                        Status = "error",
                        Error = $"Agent with ID '{request.AgentId}' already connected",
                        AgentId = request.AgentId
                    });
                    return;
                }

                var requestConfig = request.Config ?? new Dictionary<string, string>();
                var provider = requestConfig.GetValueOrDefault("provider", "openai");
                var type = "openai";
                // Merge configuration from appsettings with request config
                var mergedConfig = new Dictionary<string, string>();

                // Start with appsettings configuration if provider exists
                if (_config.Providers.TryGetValue(provider, out var providerConfig))
                {
                    type = !string.IsNullOrEmpty(providerConfig.Type) ? providerConfig.Type : "openai";
                    if (!string.IsNullOrEmpty(providerConfig.ApiKey))
                        mergedConfig["apiKey"] = ExpandEnvironmentVariable(providerConfig.ApiKey);
                    if (!string.IsNullOrEmpty(providerConfig.Model))
                        mergedConfig["model"] = providerConfig.Model;
                    if (!string.IsNullOrEmpty(providerConfig.BaseUrl))
                        mergedConfig["baseUrl"] = providerConfig.BaseUrl;
                    if (!string.IsNullOrEmpty(providerConfig.Endpoint))
                        mergedConfig["endpoint"] = ExpandEnvironmentVariable(providerConfig.Endpoint);
                    if (!string.IsNullOrEmpty(providerConfig.Deployment))
                        mergedConfig["deployment"] = providerConfig.Deployment;
                    if (!string.IsNullOrEmpty(providerConfig.ClientId))
                        mergedConfig["clientId"] = ExpandEnvironmentVariable(providerConfig.ClientId);
                }

                // Override with request configuration
                foreach (var kvp in requestConfig)
                {
                    mergedConfig[kvp.Key] = kvp.Value;
                }

                var workingDirectory = mergedConfig.GetValueOrDefault("workingDirectory", _config.WorkingDirectory);
                var verbose = bool.Parse(mergedConfig.GetValueOrDefault("verbose", "false"));

                var agent = AgentFactory.Create(type, workingDirectory, verbose, mergedConfig);

                var connection = new AgentConnection(agent, webSocket, request.AgentId);
                _agents[agentKey] = connection;

                // Set up message streaming callback
                SetupAgentCallbacks(connection);

                _logger.LogInformation("Agent created: {AgentId} for connection {ConnectionId} with provider {Provider}",
                    request.AgentId, connectionId, provider);

                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "connected",
                    Message = $"Agent initialized with provider: {provider}",
                    AgentId = request.AgentId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating agent {AgentId} for connection {ConnectionId}",
                    request.AgentId, connectionId);
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = $"Failed to create agent: {ex.Message}",
                    AgentId = request.AgentId
                });
            }
        }

        private string ExpandEnvironmentVariable(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Check if value is in format ${ENV_VAR}
            if (value.StartsWith("${") && value.EndsWith("}"))
            {
                var envVar = value.Substring(2, value.Length - 3);
                return Environment.GetEnvironmentVariable(envVar) ?? value;
            }

            return value;
        }

        private async Task HandleDisconnectAsync(string connectionId, System.Net.WebSockets.WebSocket webSocket, WebSocketMessage request)
        {
            if (string.IsNullOrEmpty(request.AgentId))
            {
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = "AgentId is required for disconnect command"
                });
                return;
            }

            var agentKey = GetAgentKey(connectionId, request.AgentId);

            if (_agents.TryRemove(agentKey, out var connection))
            {
                _logger.LogInformation("Agent {AgentId} disconnected from connection {ConnectionId}",
                    request.AgentId, connectionId);

                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "disconnected",
                    Message = "Agent disposed successfully",
                    AgentId = request.AgentId
                });
            }
            else
            {
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = $"No agent found with ID: {request.AgentId}",
                    AgentId = request.AgentId
                });
            }
        }

        private async Task HandleResetAsync(string connectionId, System.Net.WebSockets.WebSocket webSocket, WebSocketMessage request)
        {
            if (string.IsNullOrEmpty(request.AgentId))
            {
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = "AgentId is required for reset command"
                });
                return;
            }

            var agentKey = GetAgentKey(connectionId, request.AgentId);

            if (_agents.TryGetValue(agentKey, out var existingConnection))
            {
                try
                {
                    var requestConfig = request.Config ?? new Dictionary<string, string>();
                    var provider = requestConfig.GetValueOrDefault("provider", "openai");

                    // Merge configuration from appsettings with request config
                    var mergedConfig = new Dictionary<string, string>();

                    // Start with appsettings configuration if provider exists
                    if (_config.Providers.TryGetValue(provider, out var providerConfig))
                    {
                        if (!string.IsNullOrEmpty(providerConfig.ApiKey))
                            mergedConfig["apiKey"] = ExpandEnvironmentVariable(providerConfig.ApiKey);
                        if (!string.IsNullOrEmpty(providerConfig.Model))
                            mergedConfig["model"] = providerConfig.Model;
                        if (!string.IsNullOrEmpty(providerConfig.BaseUrl))
                            mergedConfig["baseUrl"] = providerConfig.BaseUrl;
                        if (!string.IsNullOrEmpty(providerConfig.Endpoint))
                            mergedConfig["endpoint"] = ExpandEnvironmentVariable(providerConfig.Endpoint);
                        if (!string.IsNullOrEmpty(providerConfig.Deployment))
                            mergedConfig["deployment"] = providerConfig.Deployment;
                        if (!string.IsNullOrEmpty(providerConfig.ClientId))
                            mergedConfig["clientId"] = ExpandEnvironmentVariable(providerConfig.ClientId);
                    }

                    // Override with request configuration
                    foreach (var kvp in requestConfig)
                    {
                        mergedConfig[kvp.Key] = kvp.Value;
                    }

                    var workingDirectory = mergedConfig.GetValueOrDefault("workingDirectory", _config.WorkingDirectory);
                    var verbose = bool.Parse(mergedConfig.GetValueOrDefault("verbose", "false"));

                    var newAgent = AgentFactory.Create(provider, workingDirectory, verbose, mergedConfig);
                    existingConnection.Agent = newAgent;

                    // Set up message streaming callback for new agent
                    SetupAgentCallbacks(existingConnection);

                    _logger.LogInformation("Agent {AgentId} reset for connection {ConnectionId}",
                        request.AgentId, connectionId);

                    await SendResponseAsync(webSocket, new WebSocketResponse
                    {
                        Status = "reset",
                        Message = "Agent reinitialized successfully",
                        AgentId = request.AgentId
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resetting agent {AgentId} for connection {ConnectionId}",
                        request.AgentId, connectionId);
                    await SendResponseAsync(webSocket, new WebSocketResponse
                    {
                        Status = "error",
                        Error = $"Failed to reset agent: {ex.Message}",
                        AgentId = request.AgentId
                    });
                }
            }
            else
            {
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = $"No agent found with ID: {request.AgentId}",
                    AgentId = request.AgentId
                });
            }
        }

        private async Task HandleSendAsync(string connectionId, System.Net.WebSockets.WebSocket webSocket, WebSocketMessage request)
        {
            if (string.IsNullOrEmpty(request.AgentId))
            {
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = "AgentId is required for send command"
                });
                return;
            }

            var agentKey = GetAgentKey(connectionId, request.AgentId);

            if (!_agents.TryGetValue(agentKey, out var connection))
            {
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = $"No agent found with ID: {request.AgentId}",
                    AgentId = request.AgentId
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(request.Data))
            {
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = "No data provided to send to agent",
                    AgentId = request.AgentId
                });
                return;
            }

            try
            {
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "processing",
                    Message = "Agent is processing your request...",
                    AgentId = request.AgentId
                });

                // Run the agent with the task
                var conversation = await connection.Agent.RunAsync(request.Data);

                // Extract the final response
                var finalResponse = ExtractFinalResponse(conversation);

                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "completed",
                    Message = "Task completed",
                    Data = finalResponse,
                    AgentId = request.AgentId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing agent task for {AgentId} in connection {ConnectionId}",
                    request.AgentId, connectionId);
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = $"Agent execution failed: {ex.Message}",
                    AgentId = request.AgentId
                });
            }
        }

        private string ExtractFinalResponse(List<Message> conversation)
        {
            var lastMessage = conversation.LastOrDefault(m => m.Role == "assistant");
            if (lastMessage?.Content == null)
                return "No response from agent";

            var textBlocks = new List<string>();

            // Content can be a list of ContentBlock objects
            if (lastMessage.Content is IEnumerable<ContentBlock> blocks)
            {
                foreach (var block in blocks)
                {
                    if (block.Type == "text")
                    {
                        textBlocks.Add(block.Text ?? "");
                    }
                }
            }
            else if (lastMessage.Content is string text)
            {
                textBlocks.Add(text);
            }

            return string.Join("\n", textBlocks);
        }

        private async Task SendResponseAsync(System.Net.WebSockets.WebSocket webSocket, WebSocketResponse response)
        {
            if (webSocket.State != WebSocketState.Open)
                return;

            var json = JsonSerializer.Serialize(response);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private void SetupAgentCallbacks(AgentConnection connection)
        {
            // Set up message streaming callback
            connection.Agent.SetMessageCallback((messageType, content) =>
            {
                // Send streaming message to client
                var response = new WebSocketResponse
                {
                    Status = "stream",
                    MessageType = messageType,
                    Message = content,
                    AgentId = connection.AgentId
                };

                // Send asynchronously without blocking
                _ = SendResponseAsync(connection.WebSocket, response);
            });

            // Set up prompt callback for AskUser tool
            var askUserTool = connection.Agent.Tools.OfType<DraCode.Agent.Tools.AskUser>().FirstOrDefault();
            if (askUserTool != null)
            {
                askUserTool.PromptCallback = async (question, context) =>
                {
                    var promptId = Guid.NewGuid().ToString();
                    var tcs = new TaskCompletionSource<string>();
                    connection.PendingPrompts[promptId] = tcs;

                    // Send prompt to client
                    await SendResponseAsync(connection.WebSocket, new WebSocketResponse
                    {
                        Status = "prompt",
                        MessageType = "prompt",
                        Message = question,
                        Data = context,
                        PromptId = promptId,
                        AgentId = connection.AgentId
                    });

                    // Wait for user response (with timeout)
                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        connection.PendingPrompts.TryRemove(promptId, out _);
                        return "User did not respond within timeout period";
                    }

                    connection.PendingPrompts.TryRemove(promptId, out _);
                    return await tcs.Task;
                };
            }
        }

        private async Task HandlePromptResponseAsync(string connectionId, System.Net.WebSockets.WebSocket webSocket, WebSocketMessage request)
        {
            if (string.IsNullOrEmpty(request.AgentId) || string.IsNullOrEmpty(request.PromptId))
            {
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = "AgentId and PromptId are required for prompt_response command"
                });
                return;
            }

            var agentKey = GetAgentKey(connectionId, request.AgentId);

            if (!_agents.TryGetValue(agentKey, out var connection))
            {
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = $"No agent found with ID: {request.AgentId}",
                    AgentId = request.AgentId
                });
                return;
            }

            if (connection.PendingPrompts.TryGetValue(request.PromptId, out var tcs))
            {
                tcs.SetResult(request.Data ?? "");

                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "success",
                    Message = "Prompt response received",
                    AgentId = request.AgentId
                });
            }
            else
            {
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = $"No pending prompt found with ID: {request.PromptId}",
                    AgentId = request.AgentId
                });
            }
        }

        private async Task DisconnectAllAgentsForConnection(string connectionId, System.Net.WebSockets.WebSocket webSocket)
        {
            var agentsToRemove = _agents.Keys.Where(k => k.StartsWith($"{connectionId}:")).ToList();

            foreach (var agentKey in agentsToRemove)
            {
                if (_agents.TryRemove(agentKey, out var connection))
                {
                    _logger.LogInformation("Agent {AgentId} removed for connection {ConnectionId}",
                        connection.AgentId, connectionId);
                }
            }

            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            }
        }
    }

    public class AgentConnection
    {
        public DraCode.Agent.Agents.Agent Agent { get; set; }
        public System.Net.WebSockets.WebSocket WebSocket { get; }
        public string AgentId { get; }
        public ConcurrentDictionary<string, TaskCompletionSource<string>> PendingPrompts { get; } = new();

        public AgentConnection(DraCode.Agent.Agents.Agent agent, System.Net.WebSockets.WebSocket webSocket, string agentId)
        {
            Agent = agent;
            WebSocket = webSocket;
            AgentId = agentId;
        }
    }
}
