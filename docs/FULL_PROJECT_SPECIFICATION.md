# 🎯 DraCode Complete Project Specification

> **Use this specification to regenerate the entire DraCode project from scratch**

---

## 📋 Project Overview

**DraCode** is a multi-provider AI coding agent system with:
- Real-time WebSocket-based multi-agent orchestration
- Support for 10 LLM providers with streaming support (OpenAI, Claude, Gemini, Azure OpenAI, Ollama, GitHub Copilot, Z.AI, vLLM, SGLang, LlamaCpp)
- 23 specialized agent types organized hierarchically (v2.5.0): Base classes (Agent, OrchestratorAgent, CodingAgent, MediaAgent), Coding agents (Debug, Documentation, Refactor, Test), Specialized language agents (C#, C++, JavaScript/TypeScript, PHP, Python, etc.), Media agents (Image, SVG, Bitmap), and Diagramming agent
- Modern TypeScript web client with zero frontend dependencies
- Tool-based autonomous code manipulation (7 built-in + 12 Dragon-specific tools + 1 Planner tool)
- Interactive user prompts via ask_user tool
- .NET Aspire orchestration for service discovery
- Optional token-based authentication with IP address binding
- KoboldLair multi-agent autonomous coding system (Dragon, Wyrm, Wyvern, Drake, Kobold Planner, Kobold)
- Two-phase analysis workflow (Wyrm pre-analysis → Wyvern detailed tasks) (v2.6.0)
- Shared planning context for cross-agent coordination and learning (v2.6.0)
- Git integration (GitService, GitStatusTool, GitMergeTool)
- Per-project and per-agent-type provider configuration for Kobolds

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
│   │       ├── OpenAiCompatibleProviderBase.cs # Base for OpenAI-compatible APIs
│   │       ├── OpenAiProvider.cs
│   │       ├── ClaudeProvider.cs
│   │       ├── GeminiProvider.cs
│   │       ├── AzureOpenAiProvider.cs
│   │       ├── OllamaProvider.cs
│   │       ├── GitHubCopilotProvider.cs
│   │       ├── ZAiProvider.cs    # Z.AI (Zhipu) GLM models
│   │       ├── VllmProvider.cs   # vLLM local inference
│   │       ├── SglangProvider.cs # SGLang inference
│   │       └── LlamaCppProvider.cs # llama.cpp server
│   ├── Tools/                    # 7 built-in tools
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
├── DraCode.KoboldLair/           # Multi-agent core library
│   ├── Agents/                   # Dragon, Wyvern, Wyrm agents
│   │   └── Tools/                # 8 Dragon-specific tools
│   │       ├── GitStatusTool.cs  # View branch status
│   │       ├── GitMergeTool.cs   # Merge feature branches
│   │       ├── SpecificationManagementTool.cs
│   │       ├── FeatureManagementTool.cs
│   │       ├── ProjectApprovalTool.cs
│   │       ├── ListProjectsTool.cs
│   │       ├── AddExistingProjectTool.cs
│   │       └── SelectAgentTool.cs
│   ├── Factories/                # KoboldFactory, DrakeFactory, WyvernFactory
│   ├── Orchestrators/            # Drake, Wyvern, WyrmRunner
│   ├── Models/                   # Agents/, Configuration/, Projects/, Tasks/
│   └── Services/                 # GitService, ProjectService, ProviderConfigurationService
├── DraCode.KoboldLair.Server/    # KoboldLair WebSocket server
│   └── Services/                 # DragonService, DrakeMonitoringService, WyvernProcessingService
├── DraCode.KoboldLair.Client/    # KoboldLair Web UI
│   └── wwwroot/                  # Status Monitor, Dragon Chat, Hierarchy View
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

## 🚀 Startup & Orchestration

### .NET Aspire AppHost

```csharp
// DraCode.AppHost/AppHost.cs
var builder = DistributedApplication.CreateBuilder(args);

var koboldlairServer = builder.AddProject<Projects.DraCode_KoboldLair_Server>("dracode-koboldlair-server")
    .WithExplicitStart();

var koboldlairClient = builder.AddProject<Projects.DraCode_KoboldLair_Client>("dracode-koboldlair-client")
    .WithReference(koboldlairServer)
    .WithExplicitStart();

builder.Build().Run();
```

**Starts** (on demand from the dashboard):
- KoboldLair Server: WebSocket API (`/dragon`, `/wyvern` endpoints)
- KoboldLair Client: Web UI
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
dotnet build ./DraCode.slnx

# Run with .NET Aspire (starts the dashboard; start services from there)
dotnet run --project DraCode.AppHost

# Compile TypeScript (KoboldLair web client)
cd DraCode.KoboldLair.Client
npm install
npm run build

# Manual startup (alternative)
# Terminal 1:
dotnet run --project DraCode.KoboldLair.Server

# Terminal 2:
dotnet run --project DraCode.KoboldLair.Client
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
