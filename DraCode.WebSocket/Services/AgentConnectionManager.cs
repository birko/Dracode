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
                    var messageBuilder = new StringBuilder();
                    
                    do
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await DisconnectAllAgentsForConnection(connectionId, webSocket);
                            return;
                        }

                        messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    var message = messageBuilder.ToString();
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
                _logger.LogInformation("Processing message from connection {ConnectionId}: {Message}", 
                    connectionId, message.Length > 200 ? message.Substring(0, 200) + "..." : message);

                var request = JsonSerializer.Deserialize<WebSocketMessage>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null)
                {
                    _logger.LogWarning("Failed to deserialize message");
                    await SendResponseAsync(webSocket, new WebSocketResponse
                    {
                        Status = "error",
                        Error = "Invalid message format"
                    });
                    return;
                }

                _logger.LogInformation("Command: {Command}, AgentId: {AgentId}, PromptId: {PromptId}", 
                    request.Command, request.AgentId, request.PromptId);

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

            await SendResponseAsync(webSocket, new WebSocketResponse
            {
                Status = "processing",
                Message = "Agent is processing your request...",
                AgentId = request.AgentId
            });

            // Capture variables for the background task
            var agentId = request.AgentId;
            var taskData = request.Data;
            var connectionSocket = connection.WebSocket;

            // Run the agent task in the background so WebSocket can continue processing messages (e.g., prompt_response)
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("🚀 Starting agent task for {AgentId}", agentId);
                    
                    // Run the agent with the task
                    var conversation = await connection.Agent.RunAsync(taskData);

                    _logger.LogInformation("✅ Agent task completed for {AgentId}", agentId);
                    
                    // Extract the final response
                    var finalResponse = ExtractFinalResponse(conversation);
                    
                    _logger.LogInformation("📝 Final response extracted - Length: {Length} characters", finalResponse?.Length ?? 0);

                    _logger.LogInformation("📤 Sending completed response to client for {AgentId}", agentId);
                    await SendResponseAsync(connectionSocket, new WebSocketResponse
                    {
                        Status = "completed",
                        Message = "Task completed",
                        Data = finalResponse,
                        AgentId = agentId
                    });
                    _logger.LogInformation("✅ Completed response sent for {AgentId}", agentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error executing agent task for {AgentId} in connection {ConnectionId}",
                        agentId, connectionId);
                    
                    _logger.LogInformation("📤 Sending error response to client for {AgentId}", agentId);
                    await SendResponseAsync(connectionSocket, new WebSocketResponse
                    {
                        Status = "error",
                        Error = $"Agent execution failed: {ex.Message}",
                        AgentId = agentId
                    });
                }
            });
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
            try
            {
                if (webSocket.State != WebSocketState.Open)
                {
                    _logger.LogWarning("⚠️ Cannot send response - WebSocket state is {State}", webSocket.State);
                    return;
                }

                var json = JsonSerializer.Serialize(response);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                _logger.LogInformation("📤 Sending response - Status: {Status}, AgentId: {AgentId}, MessageType: {MessageType}", 
                    response.Status, response.AgentId, response.MessageType);
                
                // WebSocket send operations must be synchronized
                // Find the connection for this websocket to get its semaphore
                var connection = _agents.Values.FirstOrDefault(c => c.WebSocket == webSocket);
                if (connection != null)
                {
                    await connection.WebSocketSemaphore.WaitAsync();
                    try
                    {
                        if (webSocket.State == WebSocketState.Open)
                        {
                            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                            _logger.LogInformation("✅ Response sent successfully via semaphore");
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ WebSocket closed while waiting for semaphore");
                        }
                    }
                    finally
                    {
                        connection.WebSocketSemaphore.Release();
                    }
                }
                else
                {
                    // Fallback for messages not associated with an agent connection
                    _logger.LogInformation("📤 Using fallback send (no connection found)");
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    _logger.LogInformation("✅ Fallback response sent successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending WebSocket response - Status: {Status}, AgentId: {AgentId}", 
                    response.Status, response.AgentId);
            }
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
                _logger.LogInformation("Setting up prompt callback for agent {AgentId}", connection.AgentId);
                askUserTool.PromptCallback = async (question, context) =>
                {
                    try
                    {
                        var promptId = Guid.NewGuid().ToString();
                        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                        connection.PendingPrompts[promptId] = tcs;

                        _logger.LogInformation("✅ PROMPT ADDED TO DICTIONARY - PromptId: {PromptId}, AgentId: {AgentId}, Question: {Question}, Dictionary Count: {Count}",
                            promptId, connection.AgentId, question, connection.PendingPrompts.Count);

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

                        _logger.LogInformation("Prompt sent to client. Waiting for response with timeout...");

                        // Wait for user response (with timeout)
                        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
                        _logger.LogInformation("⏰ Timeout task created for 5 minutes. Starting wait...");
                        
                        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);
                        
                        _logger.LogInformation("⏰ Task completed! Completed task: {CompletedTask}, IsTimeout: {IsTimeout}, TCS Status: {TCSStatus}",
                            completedTask == timeoutTask ? "TIMEOUT" : "RESPONSE", 
                            completedTask == timeoutTask,
                            tcs.Task.Status);

                        if (completedTask == timeoutTask)
                        {
                            _logger.LogWarning("⏱️ PROMPT TIMEOUT - Removing from dictionary - PromptId: {PromptId}, AgentId: {AgentId}", promptId, connection.AgentId);
                            connection.PendingPrompts.TryRemove(promptId, out _);
                            return "User did not respond within timeout period";
                        }

                        var response = await tcs.Task.ConfigureAwait(false);
                        _logger.LogInformation("✅ PROMPT COMPLETED - Removing from dictionary - PromptId: {PromptId}, Response: {Response}", 
                            promptId, response);
                        connection.PendingPrompts.TryRemove(promptId, out _);
                        return response;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Exception in PromptCallback");
                        return "Error: " + ex.Message;
                    }
                };
            }
            else
            {
                _logger.LogWarning("AskUser tool not found for agent {AgentId}", connection.AgentId);
            }
        }

        private async Task HandlePromptResponseAsync(string connectionId, System.Net.WebSockets.WebSocket webSocket, WebSocketMessage request)
        {
            _logger.LogInformation("HandlePromptResponseAsync called - ConnectionId: {ConnectionId}, AgentId: {AgentId}, PromptId: {PromptId}, Data: {Data}",
                connectionId, request.AgentId, request.PromptId, request.Data);

            if (string.IsNullOrEmpty(request.AgentId) || string.IsNullOrEmpty(request.PromptId))
            {
                _logger.LogWarning("Missing AgentId or PromptId in prompt_response");
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = "AgentId and PromptId are required for prompt_response command"
                });
                return;
            }

            var agentKey = GetAgentKey(connectionId, request.AgentId);
            _logger.LogInformation("Looking for agent with key: {AgentKey}", agentKey);

            if (!_agents.TryGetValue(agentKey, out var connection))
            {
                _logger.LogWarning("No agent found with key: {AgentKey}. Available keys: {Keys}",
                    agentKey, string.Join(", ", _agents.Keys));
                await SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "error",
                    Error = $"No agent found with ID: {request.AgentId}",
                    AgentId = request.AgentId
                });
                return;
            }

            _logger.LogInformation("Found agent. Pending prompts count: {Count}", connection.PendingPrompts.Count);
            
            if (connection.PendingPrompts.TryGetValue(request.PromptId, out var tcs))
            {
                _logger.LogInformation("Found pending prompt with ID: {PromptId}. Setting result.", request.PromptId);
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
                _logger.LogWarning("No pending prompt found with ID: {PromptId}. Available IDs: {Ids}",
                    request.PromptId, string.Join(", ", connection.PendingPrompts.Keys));
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
        public SemaphoreSlim WebSocketSemaphore { get; } = new SemaphoreSlim(1, 1);

        public AgentConnection(DraCode.Agent.Agents.Agent agent, System.Net.WebSockets.WebSocket webSocket, string agentId)
        {
            Agent = agent;
            WebSocket = webSocket;
            AgentId = agentId;
        }
    }
}
