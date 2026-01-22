# 🎯 DraCode Complete Project Specification

> **Use this specification to regenerate the entire DraCode project from scratch**

---

## 📋 Project Overview

**DraCode** is a multi-provider AI coding agent system with:
- Real-time WebSocket-based multi-agent orchestration
- Support for 6 LLM providers (OpenAI, Claude, Gemini, Azure OpenAI, Ollama, GitHub Copilot)
- Modern TypeScript web client with zero frontend dependencies
- Tool-based autonomous code manipulation
- Interactive user prompts via ask_user tool
- .NET Aspire orchestration for service discovery

---

## 🏗️ Technology Stack

### Backend
- **.NET 10.0** - All C# projects
- **ASP.NET Core** - Web hosting & WebSocket support
- **.NET Aspire** - Service orchestration
- **System.Text.Json** - JSON serialization
- **HttpClient** - LLM API calls

### Frontend
- **TypeScript** (ES2020 target, ES modules)
- **Pure HTML/CSS** - Zero frontend frameworks
- **WebSocket API** - Real-time communication
- **DOM manipulation** - Vanilla JavaScript

### Infrastructure
- **WebSocket Protocol** - Bidirectional streaming
- **ConcurrentDictionary** - Thread-safe agent storage
- **TaskCompletionSource** - Async prompt handling

---

## 📁 Solution Structure

```
DraCode.sln
├── DraCode/                      # CLI application (optional - not covered in this spec)
├── DraCode.Agent/                # Core agent library
│   ├── Agents/
│   │   ├── Agent.cs              # Abstract base agent with multi-turn conversation
│   │   ├── CodingAgent.cs        # Coding-specific agent with system prompt
│   │   └── AgentFactory.cs       # Factory for creating provider-specific agents
│   ├── LLMs/
│   │   ├── Message.cs            # Message model (role, content)
│   │   ├── ContentBlock.cs       # Content block (text, tool_use)
│   │   ├── LlmResponse.cs        # LLM response (StopReason, Content)
│   │   └── Providers/
│   │       ├── ILlmProvider.cs   # Provider interface
│   │       ├── LlmProviderBase.cs # Base class for OpenAI-style message handling
│   │       ├── OpenAiProvider.cs
│   │       ├── ClaudeProvider.cs
│   │       ├── GeminiProvider.cs
│   │       ├── AzureOpenAiProvider.cs
│   │       ├── OllamaProvider.cs
│   │       └── GitHubCopilotProvider.cs
│   ├── Tools/
│   │   ├── Tool.cs               # Abstract base tool
│   │   ├── ListFilesTool.cs      # Directory listing
│   │   ├── ReadFileTool.cs       # Read file contents
│   │   ├── WriteFileTool.cs      # Create/modify files
│   │   ├── SearchCodeTool.cs     # Grep-like code search
│   │   ├── RunCommandTool.cs     # Execute shell commands
│   │   ├── AskUserTool.cs        # Interactive user prompts
│   │   └── DisplayTextTool.cs    # Formatted output
│   ├── Auth/
│   │   ├── GitHubOAuthService.cs # Device flow OAuth implementation
│   │   └── TokenStorage.cs       # Token persistence (~/.dracode/)
│   └── Helpers/
│       └── PathHelper.cs         # Sandbox path validation
├── DraCode.WebSocket/            # WebSocket API server
│   ├── Program.cs                # WebSocket endpoint setup
│   ├── Services/
│   │   └── AgentConnectionManager.cs  # Multi-agent orchestration
│   ├── Models/
│   │   ├── WebSocketMessage.cs   # Client → Server messages
│   │   ├── WebSocketResponse.cs  # Server → Client messages
│   │   └── AgentConfiguration.cs # Configuration models
│   └── appsettings.json          # Provider configuration
├── DraCode.Web/                  # Web client
│   ├── Program.cs                # ASP.NET Core setup
│   ├── wwwroot/
│   │   ├── index.html            # Main HTML UI
│   │   ├── styles.css            # Pure CSS styling
│   │   └── dist/                 # Compiled TypeScript
│   ├── src/
│   │   ├── types.ts              # TypeScript interfaces
│   │   ├── client.ts             # DraCodeClient class (~800 LOC)
│   │   └── main.ts               # Application entry point
│   ├── tsconfig.json             # TypeScript configuration
│   ├── package.json              # npm scripts
│   └── appsettings.json          # Web server config
├── DraCode.AppHost/              # .NET Aspire orchestration
│   ├── Program.cs                # Service definitions
│   └── DraCode.AppHost.csproj
└── DraCode.ServiceDefaults/      # Shared Aspire config
    ├── Extensions.cs             # Service defaults extension
    └── DraCode.ServiceDefaults.csproj
```

---

## 🧬 Core Architecture

### 1. Agent System (DraCode.Agent)

#### Agent Base Class

```csharp
// DraCode.Agent/Agents/Agent.cs
public abstract class Agent
{
    // Properties
    protected ILlmProvider LlmProvider { get; }
    protected List<Tool> Tools { get; }
    protected List<Message> Conversation { get; }
    public string WorkingDirectory { get; }
    public bool Verbose { get; }
    public Action<string, string>? MessageCallback { get; set; }  // (type, message)

    // Abstract methods
    protected abstract string GetSystemPrompt();

    // Core method - multi-turn conversation loop
    public async Task<string> RunAsync(string task, int maxIterations = 10)
    {
        // 1. Build initial conversation with user task
        Conversation.Add(new Message { Role = "user", Content = task });

        // 2. Iteration loop
        for (int i = 1; i <= maxIterations; i++)
        {
            SendMessage("info", $"ITERATION {i}");

            // 3. Call LLM with conversation + tools
            var response = await LlmProvider.SendMessageAsync(
                Conversation, 
                Tools, 
                GetSystemPrompt()
            );

            // 4. Handle stop reason
            if (response.StopReason == "NotConfigured")
                return "Error: Provider not configured";

            if (response.StopReason == "error")
                return $"Error: {response.Content?.FirstOrDefault()?.Text}";

            // 5. Extract text content
            var textContent = response.Content?
                .Where(b => b.Type == "text")
                .Select(b => b.Text)
                .FirstOrDefault() ?? "";

            // 6. Handle tool calls
            var toolCalls = response.Content?
                .Where(b => b.Type == "tool_use")
                .ToList() ?? new List<ContentBlock>();

            if (toolCalls.Any())
            {
                // Execute tools and add results
                var toolResults = await ExecuteToolsAsync(toolCalls);
                Conversation.Add(new Message { 
                    Role = "assistant", 
                    Content = response.Content 
                });
                Conversation.Add(new Message { 
                    Role = "user", 
                    Content = toolResults 
                });
                continue;  // Next iteration
            }

            // 7. End turn - return final response
            if (response.StopReason == "end_turn")
            {
                Conversation.Add(new Message { 
                    Role = "assistant", 
                    Content = textContent 
                });
                return textContent;
            }
        }

        return "Max iterations reached";
    }

    // Tool execution helper
    private async Task<List<ContentBlock>> ExecuteToolsAsync(List<ContentBlock> toolCalls)
    {
        var results = new List<ContentBlock>();
        foreach (var toolCall in toolCalls)
        {
            var tool = Tools.FirstOrDefault(t => t.Name == toolCall.Name);
            if (tool == null)
            {
                results.Add(new ContentBlock {
                    Type = "tool_result",
                    ToolUseId = toolCall.Id,
                    Content = "Error: Tool not found"
                });
                continue;
            }

            SendMessage("tool_call", $"Tool: {toolCall.Name}({JsonSerializer.Serialize(toolCall.Input)})");
            var result = tool.Execute(WorkingDirectory, toolCall.Input);
            SendMessage("tool_result", $"Result: {result}");

            results.Add(new ContentBlock {
                Type = "tool_result",
                ToolUseId = toolCall.Id,
                Content = result
            });
        }
        return results;
    }

    protected void SendMessage(string type, string message)
    {
        MessageCallback?.Invoke(type, message);
        if (Verbose)
            Console.WriteLine($"[{type}] {message}");
    }
}
```

#### CodingAgent Implementation

```csharp
// DraCode.Agent/Agents/CodingAgent.cs
public class CodingAgent : Agent
{
    protected override string GetSystemPrompt()
    {
        return @"You are an expert coding assistant. You have access to tools for:
- Listing files and directories
- Reading and writing files
- Searching code
- Running shell commands
- Asking the user questions
- Displaying formatted output

Follow these principles:
1. Always explore the workspace first with list_files
2. Read relevant files before making changes
3. Make minimal, surgical changes to existing code
4. Test your changes with run_command
5. Ask the user if you need clarification
6. Provide clear explanations of what you did

When you complete the task, provide a summary of changes made.";
    }
}
```

#### AgentFactory

```csharp
// DraCode.Agent/Agents/AgentFactory.cs
public static class AgentFactory
{
    public static Agent Create(
        string provider,
        string workingDirectory,
        bool verbose,
        Dictionary<string, string> config,
        string agentType = "coding")
    {
        // Create provider
        ILlmProvider llmProvider = provider.ToLower() switch
        {
            "openai" => new OpenAiProvider(config),
            "claude" => new ClaudeProvider(config),
            "gemini" => new GeminiProvider(config),
            "azureopenai" => new AzureOpenAiProvider(config),
            "ollama" => new OllamaProvider(config),
            "githubcopilot" => new GitHubCopilotProvider(config),
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };

        // Create agent
        return agentType.ToLower() switch
        {
            "coding" => new CodingAgent(llmProvider, workingDirectory, verbose),
            _ => throw new ArgumentException($"Unknown agent type: {agentType}")
        };
    }
}
```

---

### 2. Message Models (DraCode.Agent/LLMs/)

```csharp
// Message.cs
public class Message
{
    public string? Role { get; set; }        // "user" or "assistant"
    public object? Content { get; set; }     // string, ContentBlock[], or List<ContentBlock>
}

// ContentBlock.cs
public class ContentBlock
{
    public string? Type { get; set; }        // "text" or "tool_use" or "tool_result"
    public string? Text { get; set; }        // For text blocks
    public string? Id { get; set; }          // For tool_use blocks
    public string? Name { get; set; }        // Tool name
    public Dictionary<string, object>? Input { get; set; }  // Tool parameters
    public string? ToolUseId { get; set; }   // For tool_result blocks
    public string? Content { get; set; }     // Tool result content
}

// LlmResponse.cs
public class LlmResponse
{
    public string? StopReason { get; set; }  // "tool_use", "end_turn", "error", "NotConfigured"
    public List<ContentBlock>? Content { get; set; }
}
```

---

### 3. LLM Provider Interface

```csharp
// DraCode.Agent/LLMs/Providers/ILlmProvider.cs
public interface ILlmProvider
{
    string Name { get; }
    Task<LlmResponse> SendMessageAsync(
        List<Message> messages, 
        List<Tool> tools, 
        string systemPrompt
    );
}

// DraCode.Agent/LLMs/Providers/LlmProviderBase.cs
public abstract class LlmProviderBase : ILlmProvider
{
    protected Dictionary<string, string> Config { get; }
    public abstract string Name { get; }
    
    protected abstract bool IsConfigured();
    public abstract Task<LlmResponse> SendMessageAsync(...);

    // Helper methods for OpenAI-style message conversion
    protected object ConvertToApiMessage(Message message) { /* ... */ }
    protected object ConvertToolsToApiFormat(List<Tool> tools) { /* ... */ }
}
```

#### Example Provider: OpenAI

```csharp
// DraCode.Agent/LLMs/Providers/OpenAiProvider.cs
public class OpenAiProvider : LlmProviderBase
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public override string Name => "OpenAI";

    public OpenAiProvider(Dictionary<string, string> config) : base(config)
    {
        _apiKey = config.GetValueOrDefault("apiKey", "");
        _model = config.GetValueOrDefault("model", "gpt-4o");
        _baseUrl = config.GetValueOrDefault("baseUrl", "https://api.openai.com/v1/chat/completions");
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    protected override bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_apiKey) && 
               !_apiKey.StartsWith("${");  // Not an environment variable placeholder
    }

    public override async Task<LlmResponse> SendMessageAsync(
        List<Message> messages, 
        List<Tool> tools, 
        string systemPrompt)
    {
        if (!IsConfigured())
            return new LlmResponse { StopReason = "NotConfigured" };

        try
        {
            // Build request
            var apiMessages = new List<object> 
            { 
                new { role = "system", content = systemPrompt } 
            };
            apiMessages.AddRange(messages.Select(ConvertToApiMessage));

            var requestBody = new
            {
                model = _model,
                messages = apiMessages,
                tools = ConvertToolsToApiFormat(tools),
                tool_choice = "auto"
            };

            // Send request
            var response = await _httpClient.PostAsJsonAsync(_baseUrl, requestBody);
            response.EnsureSuccessStatusCode();

            // Parse response
            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
            var choice = jsonResponse.GetProperty("choices")[0];
            var message = choice.GetProperty("message");

            // Extract content
            var content = new List<ContentBlock>();
            
            if (message.TryGetProperty("content", out var contentProp))
            {
                content.Add(new ContentBlock { 
                    Type = "text", 
                    Text = contentProp.GetString() 
                });
            }

            // Extract tool calls
            if (message.TryGetProperty("tool_calls", out var toolCalls))
            {
                foreach (var toolCall in toolCalls.EnumerateArray())
                {
                    var function = toolCall.GetProperty("function");
                    content.Add(new ContentBlock
                    {
                        Type = "tool_use",
                        Id = toolCall.GetProperty("id").GetString(),
                        Name = function.GetProperty("name").GetString(),
                        Input = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            function.GetProperty("arguments").GetString()
                        )
                    });
                }
            }

            // Determine stop reason
            var finishReason = choice.GetProperty("finish_reason").GetString();
            var stopReason = finishReason switch
            {
                "tool_calls" => "tool_use",
                "stop" => "end_turn",
                _ => "end_turn"
            };

            return new LlmResponse { StopReason = stopReason, Content = content };
        }
        catch (Exception ex)
        {
            return new LlmResponse { 
                StopReason = "error", 
                Content = new List<ContentBlock> { 
                    new ContentBlock { Type = "text", Text = ex.Message } 
                }
            };
        }
    }
}
```

---

### 4. Tool System

#### Tool Base Class

```csharp
// DraCode.Agent/Tools/Tool.cs
public abstract class Tool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract object? InputSchema { get; }  // JSON Schema for LLM
    
    public Action<string, string>? MessageCallback { get; set; }  // (type, message)
    public Func<string, string, Task<string>>? PromptCallback { get; set; }  // (question, context) -> answer
    
    public abstract string Execute(string workingDirectory, Dictionary<string, object> input);
    
    protected void SendMessage(string type, string message)
    {
        MessageCallback?.Invoke(type, message);
    }
}
```

#### Example Tool: ReadFile

```csharp
// DraCode.Agent/Tools/ReadFileTool.cs
public class ReadFileTool : Tool
{
    public override string Name => "read_file";
    
    public override string Description => 
        "Read the contents of a file. Returns the file content or error message.";
    
    public override object? InputSchema => new
    {
        type = "object",
        properties = new
        {
            file_path = new
            {
                type = "string",
                description = "Path to the file to read (relative to working directory)"
            }
        },
        required = new[] { "file_path" }
    };

    public override string Execute(string workingDirectory, Dictionary<string, object> input)
    {
        try
        {
            if (!input.TryGetValue("file_path", out var filePathObj))
                return "Error: Missing 'file_path' parameter";

            var filePath = filePathObj.ToString();
            var fullPath = Path.Combine(workingDirectory, filePath);

            // Security: validate path is within working directory
            if (!PathHelper.IsPathSafe(fullPath, workingDirectory))
                return "Error: File path is outside working directory";

            if (!File.Exists(fullPath))
                return $"Error: File not found: {filePath}";

            var content = File.ReadAllText(fullPath);
            SendMessage("tool_result", $"Read {content.Length} characters from {filePath}");
            return content;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
```

#### Example Tool: AskUser

```csharp
// DraCode.Agent/Tools/AskUserTool.cs
public class AskUserTool : Tool
{
    public override string Name => "ask_user";
    
    public override string Description => 
        "Ask the user a question and wait for their response. Use this when you need clarification or additional information.";
    
    public override object? InputSchema => new
    {
        type = "object",
        properties = new
        {
            question = new
            {
                type = "string",
                description = "The question to ask the user"
            },
            context = new
            {
                type = "string",
                description = "Optional context to help the user understand the question"
            }
        },
        required = new[] { "question" }
    };

    public override string Execute(string workingDirectory, Dictionary<string, object> input)
    {
        try
        {
            if (!input.TryGetValue("question", out var questionObj))
                return "Error: Missing 'question' parameter";

            var question = questionObj.ToString();
            var context = input.TryGetValue("context", out var ctxObj) 
                ? ctxObj.ToString() 
                : "";

            SendMessage("prompt", $"QUESTION: {question}");
            if (!string.IsNullOrEmpty(context))
                SendMessage("prompt", $"CONTEXT: {context}");

            // Call async prompt callback and wait
            if (PromptCallback == null)
                return "Error: Prompt callback not configured";

            var answer = PromptCallback(question, context).GetAwaiter().GetResult();
            SendMessage("prompt", $"USER RESPONSE: {answer}");
            
            return answer;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
```

---

### 5. WebSocket Server (DraCode.WebSocket)

#### Program.cs

```csharp
// DraCode.WebSocket/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<AgentConnectionManager>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Enable WebSocket
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};
app.UseWebSockets(webSocketOptions);

app.UseCors();

// WebSocket endpoint
app.Map("/ws", async (HttpContext context, AgentConnectionManager manager) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await manager.HandleConnectionAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Health check
app.MapGet("/", () => "DraCode WebSocket Server is running");

app.Run();
```

#### AgentConnectionManager

```csharp
// DraCode.WebSocket/Services/AgentConnectionManager.cs
public class AgentConnectionManager
{
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, AgentConnection> _agents;

    public AgentConnectionManager(IConfiguration configuration)
    {
        _configuration = configuration;
        _agents = new ConcurrentDictionary<string, AgentConnection>();
    }

    public async Task HandleConnectionAsync(WebSocket webSocket)
    {
        var connectionId = Guid.NewGuid().ToString();
        var buffer = new byte[1024 * 4];

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    CancellationToken.None
                );

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await CloseConnectionAsync(webSocket, connectionId);
                    break;
                }

                var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var message = JsonSerializer.Deserialize<WebSocketMessage>(messageJson);

                await HandleCommandAsync(webSocket, connectionId, message);
            }
        }
        catch (Exception ex)
        {
            await SendErrorAsync(webSocket, ex.Message);
        }
    }

    private async Task HandleCommandAsync(
        WebSocket webSocket, 
        string connectionId, 
        WebSocketMessage message)
    {
        switch (message.Command?.ToLower())
        {
            case "list":
                await HandleListProvidersAsync(webSocket);
                break;

            case "connect":
                await HandleConnectAsync(webSocket, connectionId, message);
                break;

            case "disconnect":
                await HandleDisconnectAsync(webSocket, connectionId, message.AgentId);
                break;

            case "reset":
                await HandleResetAsync(webSocket, connectionId, message.AgentId);
                break;

            case "send":
                await HandleSendAsync(webSocket, connectionId, message);
                break;

            case "prompt_response":
                await HandlePromptResponseAsync(connectionId, message);
                break;

            default:
                await SendErrorAsync(webSocket, $"Unknown command: {message.Command}");
                break;
        }
    }

    private async Task HandleListProvidersAsync(WebSocket webSocket)
    {
        var agentConfig = _configuration.GetSection("Agent").Get<AgentConfiguration>();
        var providers = agentConfig?.Providers?.Select(kvp => new Provider
        {
            Name = kvp.Key,
            Type = kvp.Value.Type,
            Model = kvp.Value.Model,
            Deployment = kvp.Value.Deployment,
            Configured = IsProviderConfigured(kvp.Value)
        }).ToList() ?? new List<Provider>();

        await SendResponseAsync(webSocket, new WebSocketResponse
        {
            Status = "success",
            Data = JsonSerializer.Serialize(providers)
        });
    }

    private async Task HandleConnectAsync(
        WebSocket webSocket, 
        string connectionId, 
        WebSocketMessage message)
    {
        try
        {
            var agentId = message.AgentId ?? Guid.NewGuid().ToString();
            var key = $"{connectionId}:{agentId}";

            // Get provider configuration
            var provider = message.Config?.Provider;
            if (string.IsNullOrEmpty(provider))
            {
                await SendErrorAsync(webSocket, "Provider not specified", agentId);
                return;
            }

            // Merge configuration (appsettings + request)
            var mergedConfig = MergeConfiguration(provider, message.Config);

            // Get working directory
            var workingDir = message.Config?.WorkingDirectory 
                ?? _configuration.GetSection("Agent:WorkingDirectory").Value 
                ?? "./";

            // Create agent
            var agent = AgentFactory.Create(
                provider, 
                workingDir, 
                verbose: true, 
                mergedConfig
            );

            // Setup message callback
            agent.MessageCallback = (type, msg) =>
            {
                SendResponseAsync(webSocket, new WebSocketResponse
                {
                    Status = "stream",
                    MessageType = type,
                    Message = msg,
                    AgentId = agentId
                }).Wait();
            };

            // Setup ask_user prompt callback
            var askUserTool = agent.Tools.OfType<AskUserTool>().FirstOrDefault();
            if (askUserTool != null)
            {
                askUserTool.PromptCallback = async (question, context) =>
                {
                    var promptId = Guid.NewGuid().ToString();
                    var tcs = new TaskCompletionSource<string>();

                    var connection = _agents.GetOrAdd(key, k => new AgentConnection
                    {
                        Agent = agent,
                        WebSocket = webSocket,
                        AgentId = agentId,
                        PendingPrompts = new ConcurrentDictionary<string, TaskCompletionSource<string>>()
                    });
                    connection.PendingPrompts[promptId] = tcs;

                    await SendResponseAsync(webSocket, new WebSocketResponse
                    {
                        Status = "prompt",
                        Message = question,
                        Data = context,
                        AgentId = agentId,
                        PromptId = promptId
                    });

                    // Wait up to 5 minutes for response
                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                    if (completedTask == timeoutTask)
                        return "No response received (timeout)";

                    return await tcs.Task;
                };
            }

            // Store agent connection
            _agents[key] = new AgentConnection
            {
                Agent = agent,
                WebSocket = webSocket,
                AgentId = agentId,
                PendingPrompts = new ConcurrentDictionary<string, TaskCompletionSource<string>>()
            };

            await SendResponseAsync(webSocket, new WebSocketResponse
            {
                Status = "connected",
                Message = $"Connected to {provider}",
                AgentId = agentId
            });
        }
        catch (Exception ex)
        {
            await SendErrorAsync(webSocket, ex.Message, message.AgentId);
        }
    }

    private async Task HandleSendAsync(
        WebSocket webSocket, 
        string connectionId, 
        WebSocketMessage message)
    {
        var key = $"{connectionId}:{message.AgentId}";
        
        if (!_agents.TryGetValue(key, out var connection))
        {
            await SendErrorAsync(webSocket, "Agent not found", message.AgentId);
            return;
        }

        try
        {
            await SendResponseAsync(webSocket, new WebSocketResponse
            {
                Status = "processing",
                Message = "Processing task...",
                AgentId = message.AgentId
            });

            var result = await connection.Agent.RunAsync(message.Data ?? "");

            await SendResponseAsync(webSocket, new WebSocketResponse
            {
                Status = "completed",
                Data = result,
                AgentId = message.AgentId
            });
        }
        catch (Exception ex)
        {
            await SendErrorAsync(webSocket, ex.Message, message.AgentId);
        }
    }

    private async Task HandlePromptResponseAsync(
        string connectionId, 
        WebSocketMessage message)
    {
        var key = $"{connectionId}:{message.AgentId}";
        
        if (!_agents.TryGetValue(key, out var connection))
            return;

        if (message.PromptId != null && 
            connection.PendingPrompts.TryRemove(message.PromptId, out var tcs))
        {
            tcs.SetResult(message.Data ?? "");
        }
    }

    private Dictionary<string, string> MergeConfiguration(
        string provider, 
        AgentConfig? requestConfig)
    {
        var merged = new Dictionary<string, string>();
        
        // Load from appsettings
        var agentConfig = _configuration.GetSection("Agent").Get<AgentConfiguration>();
        if (agentConfig?.Providers?.TryGetValue(provider, out var providerConfig) == true)
        {
            merged["apiKey"] = ExpandEnvironmentVariable(providerConfig.ApiKey ?? "");
            merged["model"] = providerConfig.Model ?? "";
            merged["baseUrl"] = providerConfig.BaseUrl ?? "";
            merged["endpoint"] = providerConfig.Endpoint ?? "";
            merged["deployment"] = providerConfig.Deployment ?? "";
            merged["clientId"] = providerConfig.ClientId ?? "";
        }

        // Override with request config
        if (requestConfig?.ApiKey != null) merged["apiKey"] = requestConfig.ApiKey;
        if (requestConfig?.Model != null) merged["model"] = requestConfig.Model;
        
        return merged;
    }

    private string ExpandEnvironmentVariable(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        
        if (value.StartsWith("${") && value.EndsWith("}"))
        {
            var envVar = value.Substring(2, value.Length - 3);
            return Environment.GetEnvironmentVariable(envVar) ?? value;
        }
        
        return value;
    }

    private bool IsProviderConfigured(ProviderConfig config)
    {
        if (config.Type == "ollama") return true;  // No auth needed
        
        var hasApiKey = !string.IsNullOrEmpty(config.ApiKey) && 
                       !config.ApiKey.StartsWith("${");
        var hasClientId = !string.IsNullOrEmpty(config.ClientId) && 
                         !config.ClientId.StartsWith("${");
        
        return hasApiKey || hasClientId;
    }

    private async Task SendResponseAsync(WebSocket webSocket, WebSocketResponse response)
    {
        if (webSocket.State != WebSocketState.Open) return;
        
        var json = JsonSerializer.Serialize(response);
        var buffer = Encoding.UTF8.GetBytes(json);
        await webSocket.SendAsync(
            new ArraySegment<byte>(buffer), 
            WebSocketMessageType.Text, 
            true, 
            CancellationToken.None
        );
    }

    private async Task SendErrorAsync(
        WebSocket webSocket, 
        string error, 
        string? agentId = null)
    {
        await SendResponseAsync(webSocket, new WebSocketResponse
        {
            Status = "error",
            Message = error,
            AgentId = agentId
        });
    }

    private async Task CloseConnectionAsync(WebSocket webSocket, string connectionId)
    {
        // Dispose all agents for this connection
        var keysToRemove = _agents.Keys
            .Where(k => k.StartsWith($"{connectionId}:"))
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_agents.TryRemove(key, out var connection))
            {
                // Agent cleanup if needed
            }
        }

        await webSocket.CloseAsync(
            WebSocketCloseStatus.NormalClosure, 
            "Connection closed", 
            CancellationToken.None
        );
    }
}

// Helper class
public class AgentConnection
{
    public Agent Agent { get; set; }
    public WebSocket WebSocket { get; set; }
    public string AgentId { get; set; }
    public ConcurrentDictionary<string, TaskCompletionSource<string>> PendingPrompts { get; set; }
}
```

---

### 6. Web Client (DraCode.Web)

#### TypeScript Types

```typescript
// DraCode.Web/src/types.ts
export interface WebSocketMessage {
    command: 'list' | 'connect' | 'disconnect' | 'reset' | 'send' | 'prompt_response';
    agentId?: string;
    data?: string;
    config?: AgentConfig;
    promptId?: string;
}

export interface WebSocketResponse {
    Status: 'success' | 'connected' | 'disconnected' | 'processing' | 'completed' | 'error' | 'reset' | 'stream' | 'prompt';
    Message?: string;
    Data?: string;
    Error?: string;
    AgentId?: string;
    MessageType?: string;  // info, tool_call, tool_result, prompt, etc.
    PromptId?: string;
}

export interface AgentConfig {
    provider: string;
    apiKey?: string;
    model?: string;
    workingDirectory?: string;
    verbose?: string;
}

export interface Provider {
    name: string;
    type: string;
    model?: string;
    configured: boolean;
    deployment?: string;
}

export interface Agent {
    provider: string;
    name: string;
    tabElement: HTMLButtonElement;
    contentElement: HTMLDivElement;
}

export type LogLevel = 'success' | 'error' | 'info' | 'warning';
```

#### DraCodeClient Implementation

```typescript
// DraCode.Web/src/client.ts
export class DraCodeClient {
    private ws: WebSocket | null = null;
    private agents: Map<string, Agent> = new Map();
    private activeAgentId: string | null = null;
    private availableProviders: Provider[] = [];
    private providerFilter: 'configured' | 'all' | 'notConfigured' = 'configured';
    
    // DOM elements (60+ references, use getElement helper)
    private elements: {
        status: HTMLElement;
        wsUrl: HTMLInputElement;
        connectBtn: HTMLButtonElement;
        // ... 50+ more elements
    };

    constructor() {
        this.initializeElements();
        this.setupEventListeners();
    }

    private initializeElements() {
        this.elements = {
            status: this.getElement('status'),
            wsUrl: this.getElement('wsUrl') as HTMLInputElement,
            // ... all elements
        };
    }

    private getElement(id: string): HTMLElement {
        const el = document.getElementById(id);
        if (!el) throw new Error(`Element with id '${id}' not found`);
        return el;
    }

    private setupEventListeners() {
        // Connection buttons
        this.elements.connectBtn.addEventListener('click', () => this.connectToServer());
        this.elements.disconnectBtn.addEventListener('click', () => this.disconnectFromServer());
        this.elements.listProvidersBtn.addEventListener('click', () => this.listProviders());

        // Provider filter
        const filterRadios = document.querySelectorAll('input[name="providerFilter"]');
        filterRadios.forEach(radio => {
            radio.addEventListener('change', (e) => {
                this.providerFilter = (e.target as HTMLInputElement).value as any;
                this.displayProviders(this.availableProviders);
            });
        });

        // Manual provider connect
        this.elements.manualConnectBtn.addEventListener('click', () => {
            const provider = (this.elements.manualProvider as HTMLInputElement).value;
            const apiKey = (this.elements.manualApiKey as HTMLInputElement).value;
            const model = (this.elements.manualModel as HTMLInputElement).value;
            this.connectManualProvider(provider, apiKey, model);
        });
    }

    public connectToServer(): void {
        const url = this.elements.wsUrl.value;
        
        try {
            this.ws = new WebSocket(url);
            
            this.ws.onopen = () => {
                this.setStatus('connected', '✓ Connected');
                this.listProviders();  // Auto-load providers
            };
            
            this.ws.onmessage = (event) => {
                this.handleMessage(event);
            };
            
            this.ws.onerror = (error) => {
                this.setStatus('error', '✗ Connection error');
                this.logToActive('error', 'WebSocket error occurred');
            };
            
            this.ws.onclose = () => {
                this.setStatus('disconnected', '○ Disconnected');
                this.ws = null;
            };
        } catch (error) {
            this.setStatus('error', '✗ Connection failed');
        }
    }

    public disconnectFromServer(): void {
        if (this.ws) {
            this.ws.close();
            this.ws = null;
        }
        this.setStatus('disconnected', '○ Disconnected');
    }

    public async listProviders(): Promise<void> {
        if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
            await this.showAlert('Connection Error', 'Not connected to server');
            return;
        }

        const message: WebSocketMessage = { command: 'list' };
        this.ws.send(JSON.stringify(message));
    }

    private handleMessage(event: MessageEvent): void {
        const response: WebSocketResponse = JSON.parse(event.data);

        // Route to specific agent if AgentId present
        if (response.AgentId) {
            this.handleAgentMessage(response);
            return;
        }

        // Handle global messages
        switch (response.Status) {
            case 'success':
                if (response.Data) {
                    const providers: Provider[] = JSON.parse(response.Data);
                    this.availableProviders = providers;
                    this.displayProviders(providers);
                }
                break;
            
            case 'error':
                this.logToActive('error', response.Message || 'Error occurred');
                break;
        }
    }

    private handleAgentMessage(response: WebSocketResponse): void {
        const agentId = response.AgentId!;

        switch (response.Status) {
            case 'connected':
                this.logToAgent(agentId, 'success', response.Message || 'Connected');
                // Refresh provider counts
                this.listProviders();
                break;

            case 'disconnected':
                this.logToAgent(agentId, 'info', 'Disconnected');
                break;

            case 'processing':
                this.logToAgent(agentId, 'info', response.Message || 'Processing...');
                break;

            case 'stream':
                const type = response.MessageType || 'info';
                this.addLog(agentId, type as LogLevel, response.Message || '');
                break;

            case 'completed':
                this.logToAgent(agentId, 'success', '✓ Task completed');
                if (response.Data) {
                    this.logToAgent(agentId, 'info', response.Data);
                }
                break;

            case 'prompt':
                this.handlePrompt(agentId, response.PromptId!, response.Message!, response.Data);
                break;

            case 'error':
                this.logToAgent(agentId, 'error', response.Message || 'Error');
                break;
        }
    }

    private displayProviders(providers: Provider[]): void {
        this.elements.providersGrid.innerHTML = '';
        
        // Filter providers
        const filtered = providers.filter(p => {
            if (this.providerFilter === 'configured') return p.configured;
            if (this.providerFilter === 'notConfigured') return !p.configured;
            return true;  // 'all'
        });

        if (filtered.length === 0) {
            this.elements.providersGrid.innerHTML = '<div class="empty-state">No providers found</div>';
            return;
        }

        // Count active connections per provider
        const connectionCounts = new Map<string, number>();
        this.agents.forEach(agent => {
            const count = connectionCounts.get(agent.provider) || 0;
            connectionCounts.set(agent.provider, count + 1);
        });

        // Create cards
        filtered.forEach(provider => {
            const card = document.createElement('div');
            card.className = 'provider-card';
            
            const connections = connectionCounts.get(provider.name) || 0;
            if (connections > 0) {
                card.classList.add('connected');
            }

            const connectionStatus = connections > 0 
                ? `${connections} connection${connections !== 1 ? 's' : ''}`
                : 'Not connected';

            card.innerHTML = `
                <div class="provider-name">${this.escapeHtml(provider.name)}</div>
                <div class="provider-model">${this.escapeHtml(provider.model || 'Default model')}</div>
                <div class="provider-status ${provider.configured ? 'available' : 'unavailable'}">
                    ${provider.configured ? 'Configured' : 'Not configured'}
                </div>
                <div class="provider-connections">${connectionStatus}</div>
            `;

            card.addEventListener('click', () => {
                const agentId = `agent-${provider.name}-${Date.now()}`;
                this.connectToProvider(provider.name, agentId);
            });

            this.elements.providersGrid.appendChild(card);
        });

        this.elements.providersSection.style.display = 'block';
    }

    public async connectToProvider(providerName: string, agentId: string): Promise<void> {
        if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
            await this.showAlert('Connection Error', 'Not connected to server');
            return;
        }

        // Count existing connections
        const existingConnections = Array.from(this.agents.values())
            .filter(a => a.provider === providerName).length;
        
        const defaultDisplayName = existingConnections > 0 
            ? `${providerName} #${existingConnections + 1}`
            : providerName;

        // Show connection modal
        this.showConnectionModal(defaultDisplayName, (tabName, workingDir) => {
            const config: AgentConfig = { provider: providerName };
            if (workingDir && workingDir.trim()) {
                config.workingDirectory = workingDir.trim();
            }

            const message: WebSocketMessage = {
                command: 'connect',
                config: config,
                agentId
            };

            this.ws!.send(JSON.stringify(message));
            this.createAgentTab(agentId, tabName, providerName);
            this.logToAgent(agentId, 'info', `Connecting to ${providerName}...`);
        });
    }

    private showConnectionModal(
        defaultTabName: string, 
        onConnect: (tabName: string, workingDir: string) => void
    ): void {
        this.elements.connectionTabName.value = defaultTabName;
        this.elements.connectionWorkingDir.value = '';

        let workingDirManuallyEdited = false;

        const handleTabNameInput = () => {
            if (!workingDirManuallyEdited) {
                const tabName = this.elements.connectionTabName.value;
                this.elements.connectionWorkingDir.value = this.sanitizeDirectoryName(tabName);
            }
        };

        const handleWorkingDirInput = () => {
            workingDirManuallyEdited = true;
        };

        const handleConnect = async () => {
            const tabName = this.elements.connectionTabName.value.trim();
            const workingDir = this.elements.connectionWorkingDir.value.trim();

            if (!tabName) {
                await this.showAlert('Validation Error', 'Tab name is required');
                return;
            }

            this.elements.connectionModal.classList.remove('active');
            cleanup();
            onConnect(tabName, workingDir);
        };

        const handleCancel = () => {
            this.elements.connectionModal.classList.remove('active');
            cleanup();
        };

        const cleanup = () => {
            this.elements.connectionModalConnect.removeEventListener('click', handleConnect);
            this.elements.connectionModalCancel.removeEventListener('click', handleCancel);
            this.elements.connectionTabName.removeEventListener('input', handleTabNameInput);
            this.elements.connectionWorkingDir.removeEventListener('input', handleWorkingDirInput);
        };

        this.elements.connectionModalConnect.addEventListener('click', handleConnect);
        this.elements.connectionModalCancel.addEventListener('click', handleCancel);
        this.elements.connectionTabName.addEventListener('input', handleTabNameInput);
        this.elements.connectionWorkingDir.addEventListener('input', handleWorkingDirInput);

        this.elements.connectionModal.classList.add('active');
        handleTabNameInput();  // Initialize
        setTimeout(() => this.elements.connectionTabName.focus(), 100);
    }

    private sanitizeDirectoryName(name: string): string {
        if (!name) return '';

        // Remove diacritics
        const normalized = name.normalize('NFD').replace(/[\u0300-\u036f]/g, '');

        // Sanitize
        let sanitized = normalized
            .toLowerCase()
            .replace(/[^a-z0-9_-]+/g, '-')
            .replace(/^-+|-+$/g, '')
            .replace(/-+/g, '-');

        return sanitized || 'workspace';
    }

    private createAgentTab(agentId: string, name: string, provider: string): void {
        // Create tab button
        const tab = document.createElement('button');
        tab.className = 'tab';
        tab.type = 'button';
        tab.innerHTML = `
            ${this.escapeHtml(name)}
            <span class="tab-close" data-agent-id="${agentId}">×</span>
        `;
        tab.dataset.agentId = agentId;

        // Create content area
        const content = document.createElement('div');
        content.className = 'tab-content';
        content.dataset.agentId = agentId;
        content.innerHTML = `
            <div class="task-section">
                <h2>Task</h2>
                <textarea id="taskInput-${agentId}" placeholder="Enter your coding task..."></textarea>
                <button id="sendBtn-${agentId}" type="button">Send Task</button>
            </div>
            <div class="task-section">
                <h2>Activity Log</h2>
                <div class="log" id="log-${agentId}"></div>
            </div>
        `;

        // Store agent
        this.agents.set(agentId, {
            provider,
            name,
            tabElement: tab,
            contentElement: content
        });

        // Add to DOM
        this.elements.tabs.appendChild(tab);
        this.elements.tabContents.appendChild(content);

        // Setup event listeners
        tab.addEventListener('click', (e) => {
            if (!(e.target as HTMLElement).classList.contains('tab-close')) {
                this.switchToAgent(agentId);
            }
        });

        const closeBtn = tab.querySelector('.tab-close')!;
        closeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.closeAgent(agentId);
        });

        const sendBtn = content.querySelector(`#sendBtn-${agentId}`)!;
        sendBtn.addEventListener('click', () => this.sendTask(agentId));

        // Add keyboard shortcut for textarea
        const taskInput = content.querySelector(`#taskInput-${agentId}`) as HTMLTextAreaElement;
        if (taskInput) {
            taskInput.addEventListener('keydown', (e) => {
                // Enter without Shift sends the task
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    this.sendTask(agentId);
                }
                // Shift+Enter adds new line (default textarea behavior)
            });
        }

        // Show tabs section
        this.elements.agentTabs.style.display = 'flex';

        // Switch to new tab
        this.switchToAgent(agentId);
    }

    private switchToAgent(agentId: string): void {
        // Deactivate all
        this.agents.forEach((agent) => {
            agent.tabElement.classList.remove('active');
            agent.contentElement.classList.remove('active');
        });

        // Activate target
        const agent = this.agents.get(agentId);
        if (agent) {
            agent.tabElement.classList.add('active');
            agent.contentElement.classList.add('active');
            this.activeAgentId = agentId;
        }
    }

    private async closeAgent(agentId: string): Promise<void> {
        const confirmed = await this.showConfirm(
            'Close Agent',
            'Are you sure you want to close this agent?'
        );

        if (!confirmed) return;

        // Send disconnect
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.send(JSON.stringify({
                command: 'disconnect',
                agentId
            }));
        }

        // Remove from DOM
        const agent = this.agents.get(agentId);
        if (agent) {
            agent.tabElement.remove();
            agent.contentElement.remove();
            this.agents.delete(agentId);
        }

        // Switch to another tab or hide section
        if (this.agents.size > 0) {
            const firstAgent = this.agents.keys().next().value;
            this.switchToAgent(firstAgent);
        } else {
            this.elements.agentTabs.style.display = 'none';
            this.activeAgentId = null;
        }

        // Refresh provider grid
        this.listProviders();
    }

    private sendTask(agentId: string): void {
        if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
            this.logToAgent(agentId, 'error', 'Not connected to server');
            return;
        }

        const taskInput = document.getElementById(`taskInput-${agentId}`) as HTMLTextAreaElement;
        const task = taskInput.value.trim();

        if (!task) {
            this.logToAgent(agentId, 'error', 'Please enter a task');
            return;
        }

        const message: WebSocketMessage = {
            command: 'send',
            agentId,
            data: task
        };

        this.ws.send(JSON.stringify(message));
        this.logToAgent(agentId, 'info', `> ${task}`);
        taskInput.value = '';
    }

    private handlePrompt(
        agentId: string, 
        promptId: string, 
        question: string, 
        context?: string
    ): void {
        this.showPrompt(question, context).then(answer => {
            if (answer && this.ws && this.ws.readyState === WebSocket.OPEN) {
                this.ws.send(JSON.stringify({
                    command: 'prompt_response',
                    agentId,
                    promptId,
                    data: answer
                }));
            }
        });
    }

    // Modal helpers (showAlert, showConfirm, showPrompt) - similar to previous implementation

    private addLog(agentId: string, level: LogLevel, message: string): void {
        const log = document.getElementById(`log-${agentId}`);
        if (!log) return;

        const entry = document.createElement('div');
        entry.className = `log-entry ${level}`;
        
        const time = new Date().toLocaleTimeString();
        entry.innerHTML = `<span class="log-time">${time}</span>${this.escapeHtml(message)}`;
        
        log.appendChild(entry);
        log.scrollTop = log.scrollHeight;
    }

    private logToAgent(agentId: string, level: LogLevel, message: string): void {
        this.addLog(agentId, level, message);
    }

    private escapeHtml(text: string): string {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}
```

#### HTML Structure

```html
<!-- DraCode.Web/wwwroot/index.html -->
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>DraCode Multi-Provider WebSocket Client</title>
    <link rel="stylesheet" href="styles.css">
</head>
<body>
    <header>
        <h1>🤖 DraCode Multi-Provider WebSocket Client</h1>
        <div id="status" class="status disconnected">○ Disconnected</div>
    </header>

    <main>
        <!-- Connection Section -->
        <section class="connection-section">
            <h2>WebSocket Connection</h2>
            <div class="form-group">
                <label for="wsUrl">WebSocket URL</label>
                <input type="text" id="wsUrl" value="ws://localhost:5000/ws" />
            </div>
            <div class="button-group">
                <button id="connectBtn" type="button">Connect</button>
                <button id="disconnectBtn" type="button" class="secondary">Disconnect</button>
                <button id="listProvidersBtn" type="button">List Providers</button>
            </div>
        </section>

        <!-- Providers Section -->
        <section id="providersSection" class="providers-section">
            <h2>Available Providers</h2>
            
            <!-- Provider Filter -->
            <div class="provider-filter">
                <label>Show:</label>
                <label class="filter-option">
                    <input type="radio" name="providerFilter" value="configured" checked />
                    <span>Configured</span>
                </label>
                <label class="filter-option">
                    <input type="radio" name="providerFilter" value="all" />
                    <span>All</span>
                </label>
                <label class="filter-option">
                    <input type="radio" name="providerFilter" value="notConfigured" />
                    <span>Not Configured</span>
                </label>
            </div>

            <div id="providersGrid" class="providers-grid"></div>
        </section>

        <!-- Agent Tabs -->
        <div id="agentTabs">
            <div class="tabs" id="tabs"></div>
            <div id="tabContents"></div>
        </div>
    </main>

    <!-- Connection Modal -->
    <div id="connectionModal" class="modal">
        <div class="modal-content">
            <h3>Configure Connection</h3>
            <div class="form-group">
                <label for="connectionTabName">Connection Name</label>
                <input type="text" id="connectionTabName" placeholder="Enter connection name" />
            </div>
            <div class="form-group">
                <label for="connectionWorkingDir">Working Directory</label>
                <input type="text" id="connectionWorkingDir" placeholder="Auto-generated from name" />
            </div>
            <div class="button-group">
                <button id="connectionModalConnect" type="button">Connect</button>
                <button id="connectionModalCancel" type="button" class="secondary">Cancel</button>
            </div>
        </div>
    </div>

    <!-- General Modal -->
    <div id="generalModal" class="modal">
        <div class="modal-content">
            <h3 id="generalModalTitle"></h3>
            <p id="generalModalMessage"></p>
            <input type="text" id="generalModalInput" style="display:none;" />
            <div class="button-group">
                <button id="generalModalConfirm" type="button">OK</button>
                <button id="generalModalCancel" type="button" class="secondary">Cancel</button>
            </div>
        </div>
    </div>

    <script type="module" src="dist/main.js"></script>
</body>
</html>
```

---

## 🎨 CSS Styling Specification

### Design System

```css
/* DraCode.Web/wwwroot/styles.css */

/* === CSS Variables === */
:root {
    /* Colors */
    --primary-color: #667eea;
    --primary-light: rgba(102, 126, 234, 0.1);
    --secondary-color: #6c757d;
    --success-color: #10b981;
    --error-color: #ef4444;
    --warning-color: #f59e0b;
    --info-color: #3b82f6;
    
    /* Backgrounds */
    --bg-primary: #ffffff;
    --bg-secondary: #f8fafc;
    --bg-tertiary: #e2e8f0;
    
    /* Text */
    --text-primary: #1e293b;
    --text-secondary: #64748b;
    
    /* Borders */
    --border-color: #cbd5e1;
    
    /* Spacing (8px base grid) */
    --spacing-xs: 0.25rem;
    --spacing-sm: 0.5rem;
    --spacing-md: 0.75rem;
    --spacing-lg: 1rem;
    --spacing-xl: 1.5rem;
    
    /* Typography */
    --font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    --font-size-sm: 0.813rem;
    --font-size-base: 0.938rem;
    --font-size-lg: 1.125rem;
    
    /* Border Radius */
    --border-radius-sm: 4px;
    --border-radius-md: 8px;
    --border-radius-lg: 12px;
    --border-radius-xl: 16px;
    
    /* Shadows */
    --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.05);
    --shadow-md: 0 4px 6px rgba(0, 0, 0, 0.1);
    --shadow-lg: 0 10px 15px rgba(0, 0, 0, 0.1);
    --shadow-xl: 0 20px 25px rgba(0, 0, 0, 0.15);
    
    /* Transitions */
    --transition-fast: 150ms cubic-bezier(0.4, 0, 0.2, 1);
    --transition-base: 200ms cubic-bezier(0.4, 0, 0.2, 1);
    
    /* Gradients */
    --gradient-primary: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    --gradient-card: linear-gradient(145deg, #ffffff 0%, #f8fafc 100%);
}

/* === Animations === */
@keyframes fadeIn {
    from { opacity: 0; transform: translateY(-10px); }
    to { opacity: 1; transform: translateY(0); }
}

@keyframes pulse {
    0%, 100% { transform: scale(1); }
    50% { transform: scale(1.1); }
}

/* === Hover Effects (Subtle) === */
/* Provider cards: border color change only (no transform to avoid disturbance during updates) */
.provider-card:hover {
    box-shadow: var(--shadow-lg);
    border-color: var(--primary-color);
}

/* Buttons: minimal lift */
button:hover:not(:disabled) {
    transform: translateY(-1px);
    box-shadow: var(--shadow-md);
}

/* Tabs: background only (no transform) */
.tab:hover:not(.active) {
    background: rgba(102, 126, 234, 0.08);
    color: var(--primary-color);
}

/* Log entries: subtle background */
.log-entry:hover {
    background: rgba(255, 255, 255, 0.03);
}

/* === Base Styles === */
* { box-sizing: border-box; margin: 0; padding: 0; }

body {
    font-family: var(--font-family);
    font-size: var(--font-size-base);
    color: var(--text-primary);
    background: var(--bg-secondary);
    line-height: 1.5;
}

/* === Layout Components === */
header {
    background: var(--gradient-primary);
    color: white;
    padding: var(--spacing-lg);
    display: flex;
    justify-content: space-between;
    align-items: center;
    box-shadow: var(--shadow-md);
}

/* === Provider Icons (CSS ::before) === */
.provider-name::before {
    content: '🔌';
    font-size: 1.2em;
}

.provider-card.connected .provider-name::before {
    content: '✅';
    animation: pulse 2s ease-in-out infinite;
}

.provider-status.available::before {
    content: '✓ ';
    font-weight: 700;
}

.provider-status.unavailable::before {
    content: '⚠ ';
}

.status { /* Status indicator styles */ }
.button { /* Button styles */ }
.provider-card { /* Provider card styles */ }
.tab { /* Tab styles */ }
.log { /* Terminal-style log */ }
.modal { /* Modal overlay and content */ }

/* === Responsive Design === */
@media (max-width: 768px) {
    .providers-grid {
        grid-template-columns: 1fr;
    }
    .tabs {
        overflow-x: auto;
    }
}
```

**Key CSS Features**:
- **Zero dependencies** - Pure CSS
- **CSS custom properties** for theming
- **Flexbox-based** responsive layout
- **Subtle animations** (fadeIn, pulse)
- **Terminal-style log** with dark background
- **Modal system** with backdrop blur
- **Compact mobile-friendly** design
- **Subtle hover effects** on cards/buttons (no disturbing transforms during dynamic updates)
- **CSS ::before pseudo-elements** for icons (🔌/✅ for providers, ✓/⚠ for status)

---

## ⚙️ Configuration Files

### appsettings.json (DraCode.WebSocket)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Agent": {
    "WorkingDirectory": "./workspace",
    "Providers": {
      "openai": {
        "Type": "openai",
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4o",
        "BaseUrl": "https://api.openai.com/v1/chat/completions"
      },
      "claude": {
        "Type": "claude",
        "ApiKey": "${ANTHROPIC_API_KEY}",
        "Model": "claude-3-5-sonnet-latest",
        "BaseUrl": "https://api.anthropic.com/v1/messages"
      },
      "gemini": {
        "Type": "gemini",
        "ApiKey": "${GEMINI_API_KEY}",
        "Model": "gemini-2.0-flash-exp",
        "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/models"
      },
      "azureopenai": {
        "Type": "azureopenai",
        "Endpoint": "${AZURE_OPENAI_ENDPOINT}",
        "ApiKey": "${AZURE_OPENAI_API_KEY}",
        "Deployment": "gpt-4",
        "Model": "gpt-4"
      },
      "ollama": {
        "Type": "ollama",
        "Model": "llama3.2",
        "BaseUrl": "http://localhost:11434"
      },
      "githubcopilot": {
        "Type": "githubcopilot",
        "ClientId": "Ov23liKJ5Ds3g6d9X2Dp",
        "Model": "gpt-4o"
      }
    }
  }
}
```

### tsconfig.json

```json
{
  "compilerOptions": {
    "target": "ES2020",
    "module": "ES2020",
    "lib": ["ES2020", "DOM"],
    "outDir": "./wwwroot/dist",
    "rootDir": "./src",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true,
    "moduleResolution": "node"
  },
  "include": ["src/**/*"],
  "exclude": ["node_modules"]
}
```

### package.json

```json
{
  "name": "dracode-web-client",
  "version": "1.0.0",
  "scripts": {
    "build": "tsc",
    "watch": "tsc --watch"
  },
  "devDependencies": {
    "typescript": "^5.0.0"
  }
}
```

---

## 🚀 Startup & Orchestration

### .NET Aspire AppHost

```csharp
// DraCode.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var websocket = builder.AddProject<Projects.DraCode_WebSocket>("dracode-websocket")
    .WithHttpEndpoint(port: 5000, name: "websocket");

var web = builder.AddProject<Projects.DraCode_Web>("dracode-web")
    .WithHttpEndpoint(port: 5001, name: "web")
    .WithReference(websocket);

builder.Build().Run();
```

**Starts**:
- WebSocket API: `ws://localhost:5000/ws`
- Web Client: `http://localhost:5001`
- Aspire Dashboard: `http://localhost:18888`

---

## 🎯 Key Implementation Guidelines

### 1. **Agent Multi-Turn Conversation**
- Agent.RunAsync() is the core loop (max 10 iterations)
- Each iteration: LLM call → parse response → execute tools → repeat
- Stop reasons: `tool_use` (continue), `end_turn` (return), `error`, `NotConfigured`

### 2. **WebSocket Protocol**
- Commands: `list`, `connect`, `disconnect`, `reset`, `send`, `prompt_response`
- Responses: Include `Status`, `Message`, `Data`, `AgentId`, `MessageType`, `PromptId`
- Streaming: Use `Status: "stream"` with `MessageType` for log categorization

### 3. **Multi-Agent System**
- Key format: `{connectionId}:{agentId}`
- Each agent has independent conversation history
- Support multiple agents per connection and provider

### 4. **Interactive Prompts**
- AskUserTool triggers PromptCallback
- Server sends `Status: "prompt"` with unique PromptId
- Client displays modal, user responds
- Client sends `prompt_response` command
- TaskCompletionSource<string> resolves, agent continues

### 5. **Provider Configuration**
- Stored in appsettings.json with `${ENV_VAR}` syntax
- Server-side expansion of environment variables
- Runtime merging: appsettings + request overrides
- Detection: Check if apiKey/clientId is not placeholder

### 6. **Frontend State Management**
- Map-based agent storage (`Map<agentId, Agent>`)
- Dynamic tab creation/removal
- Active agent tracking for input routing
- Provider filter state persists during session

### 7. **Modal System**
- Three functions: `showAlert()`, `showConfirm()`, `showPrompt()`
- Promise-based async/await pattern
- Single modal DOM reused for all purposes
- Event cleanup to prevent memory leaks

### 8. **Security**
- PathHelper validates all file paths
- Operations restricted to working directory
- API keys never exposed to client
- OAuth tokens stored in `~/.dracode/`

### 9. **Error Handling**
- Tools return `"Error: ..."` on failure
- Agent gets one final chance if all tools fail
- WebSocket sends `Status: "error"` on exceptions
- Frontend displays errors in log with red styling

### 10. **Auto-generated Working Directory**
- Sanitize connection name: remove diacritics, lowercase, replace special chars with hyphens
- Auto-update working directory as user types name
- Stop auto-updating if user manually edits working directory
- Fallback to "workspace" if sanitization produces empty string

### 11. **Keyboard Shortcuts**
- Task textarea: Enter sends task, Shift+Enter adds new line
- Improves UX for both single-line and multi-line task input

### 12. **UI Best Practices**
- Use CSS ::before pseudo-elements for icons (avoids HTML duplication)
- Hover effects should be subtle (no transforms on dynamic elements)
- Remove icons from HTML when CSS already provides them via ::before
- Provider cards: 🔌 (disconnected) / ✅ (connected with pulse animation)
- Status indicators: ✓ (configured) / ⚠ (not configured)

---

## 🔥 Quick Start Commands

```bash
# Build all projects
dotnet build

# Run with .NET Aspire
dotnet run --project DraCode.AppHost

# Compile TypeScript (run in DraCode.Web directory)
cd DraCode.Web
npm install
npm run build

# Manual startup (alternative)
# Terminal 1:
dotnet run --project DraCode.WebSocket

# Terminal 2:
dotnet run --project DraCode.Web

# Open browser
open http://localhost:5001
```

---

## 📦 NuGet Packages Required

**All Projects**:
- `Microsoft.NET.Sdk.Web` (SDK, not package)

**DraCode.AppHost**:
- `Aspire.Hosting.AppHost` (latest)

**DraCode.ServiceDefaults**:
- `Microsoft.Extensions.Http.Resilience`
- `Microsoft.Extensions.ServiceDiscovery`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Instrumentation.Runtime`

**DraCode.Agent**:
- `System.Text.Json`

**DraCode.WebSocket**:
- Project reference to `DraCode.Agent`
- Project reference to `DraCode.ServiceDefaults`

**DraCode.Web**:
- Project reference to `DraCode.ServiceDefaults`

---

## ✅ Testing Checklist

After implementation, verify:

1. ✓ WebSocket connection establishes
2. ✓ Provider list loads with correct configured status
3. ✓ Can connect to multiple providers simultaneously
4. ✓ Each agent has independent tab and conversation
5. ✓ Tab switching works correctly
6. ✓ Task execution streams messages (info, tool_call, tool_result)
7. ✓ Ask_user tool displays modal and waits for response
8. ✓ Connection name auto-generates safe working directory
9. ✓ Manual working directory edit stops auto-generation
10. ✓ Provider connection counts update correctly
11. ✓ Close agent removes tab and disconnects from server
12. ✓ Provider filter (configured/all/notConfigured) works
13. ✓ Responsive design works on mobile (viewport < 768px)
14. ✓ Modal dialogs replace all alerts/confirms/prompts
15. ✓ Environment variables expanded server-side

---

## 🎓 Extension Points

**Add New Tool**:
1. Create class inheriting from `Tool`
2. Implement Name, Description, InputSchema, Execute()
3. Add to Agent.CreateTools()

**Add New Provider**:
1. Create class implementing `ILlmProvider` (or inherit `LlmProviderBase`)
2. Implement Name and SendMessageAsync()
3. Add case in AgentFactory.Create()
4. Add configuration to appsettings.json

**Add New Agent Type**:
1. Create class inheriting from `Agent`
2. Override GetSystemPrompt()
3. Add case in AgentFactory.Create()

---

## 📄 License

MIT License

---

**END OF SPECIFICATION**

Use this document to regenerate the entire DraCode project from scratch with full multi-provider AI agent capabilities, real-time WebSocket communication, and modern TypeScript web client!
