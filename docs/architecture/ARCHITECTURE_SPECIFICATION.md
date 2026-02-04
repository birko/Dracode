# DraCode Architecture Specification

**Version:** 2.4.1
**Last Updated:** February 4, 2026
**Status:** Current Implementation

---

## Table of Contents
1. [System Overview](#1-system-overview)
2. [Architecture Design](#2-architecture-design)
3. [Component Specifications](#3-component-specifications)
4. [Data Flow](#4-data-flow)
5. [Provider Integration](#5-provider-integration)
6. [UI/UX Design](#6-uiux-design)
7. [Security Architecture](#7-security-architecture)
8. [Configuration System](#8-configuration-system)

---

## 1. System Overview

### 1.1 Purpose
DraCode is an AI-powered coding agent CLI that enables autonomous code manipulation through natural language instructions. It provides a secure, sandboxed environment for LLM-driven development tasks.

### 1.2 Key Capabilities
- **Multi-Provider LLM Support**: Seamless integration with 10 LLM providers
- **Multi-Task Execution**: Sequential execution of multiple tasks with fresh agent instances
- **Tool-Based Architecture**: Extensible system for adding new capabilities
- **Interactive CLI**: Modern, colorful interface with Spectre.Console
- **OAuth Integration**: Secure GitHub Copilot authentication
- **Provider Selection**: Interactive menu for choosing AI providers
- **Sandboxed Execution**: All operations restricted to workspace
- **Batch Processing**: Comma-separated tasks or interactive multi-task input

### 1.3 Technology Stack
- **Language**: C# 14.0
- **Framework**: .NET 10.0
- **UI Library**: Spectre.Console 0.54.0
- **Configuration**: Microsoft.Extensions.Configuration
- **HTTP Client**: System.Net.Http
- **JSON Serialization**: System.Text.Json

---

## 2. Architecture Design

### 2.1 Layered Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Presentation Layer                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │ CLI UI       │  │  Provider    │  │  Interactive         │  │
│  │ (Program.cs) │  │  Selection   │  │  Prompts (AskUser)   │  │
│  │ Spectre.     │  │  Menu        │  │  Spectre.Console     │  │
│  │ Console      │  │              │  │                      │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────┐
│                    Business Logic Layer                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │ Agent Core   │  │  Message     │  │  Iteration           │  │
│  │ (Agent.cs)   │  │  Management  │  │  Control             │  │
│  │              │  │  Conversation│  │  Max Iterations      │  │
│  │              │  │  History     │  │  Stop Reason Handler │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────┐
│                    Service Layer                                │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │ Tool System  │  │  LLM         │  │  OAuth Service       │  │
│  │ - ListFiles  │  │  Provider    │  │  GitHub Device Flow  │  │
│  │ - ReadFile   │  │  Abstraction │  │  Token Management    │  │
│  │ - WriteFile  │  │  ILlmProvider│  │  Refresh Logic       │  │
│  │ - SearchCode │  │  Interface   │  │                      │  │
│  │ - RunCommand │  │              │  │                      │  │
│  │ - DisplayText│  │              │  │                      │  │
│  │ - AskUser    │  │              │  │                      │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────┐
│                    Integration Layer                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │ OpenAI       │  │  Claude      │  │  Gemini              │  │
│  │ Provider     │  │  Provider    │  │  Provider            │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │ Azure OpenAI │  │  Ollama      │  │  GitHub Copilot      │  │
│  │ Provider     │  │  Provider    │  │  Provider (OAuth)    │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Component Interaction Diagram

```
User Input
    │
    ▼
┌─────────────────┐
│  Program.cs     │──► Provider Selection Menu
│  Entry Point    │──► Verbose Output Selection
│                 │──► Task Input (Single/Multiple)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Task Loop      │──► For each task:
│  (Sequential)   │    - Create new Agent instance
│                 │    - Execute task
│                 │    - Track progress
│                 │    - Handle errors
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  AgentFactory   │──► Creates Agent with Provider
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│  Agent (Abstract)                       │
│  ┌───────────────────────────────────┐  │
│  │ RunAsync(task, maxIterations)     │  │
│  │ ├─► Send message to LLM           │  │
│  │ ├─► Process response              │  │
│  │ ├─► Execute tools if needed       │  │
│  │ └─► Repeat until done/max reached │  │
│  └───────────────────────────────────┘  │
└────────┬──────────────────────┬─────────┘
         │                      │
         ▼                      ▼
┌──────────────────┐   ┌──────────────────┐
│  ILlmProvider    │   │  Tool System     │
│  - SendMessage   │   │  - Execute()     │
│  - Parse Response│   │  - Validate Path │
└──────────────────┘   └──────────────────┘
```

### 2.3 Multi-Task Execution Flow

DraCode supports executing multiple tasks sequentially, with each task getting a fresh agent instance for context isolation.

```
┌─────────────────────────────────────────────────────────┐
│  Task Queue: ["Task 1", "Task 2", "Task 3"]            │
└──────────────────┬──────────────────────────────────────┘
                   │
     ┌─────────────┴──────────────┐
     │                            │
     ▼                            ▼
┌──────────────┐          ┌──────────────┐
│  For i=1..N  │          │  Progress    │
│  Task Loop   │──────────│  Tracking    │
└──────┬───────┘          └──────────────┘
       │
       ▼
┌──────────────────────────────────┐
│  Create New Agent Instance       │  ◄─── Fresh context
│  AgentFactory.Create(...)        │        for each task
└──────────┬───────────────────────┘
           │
           ▼
┌──────────────────────────────────┐
│  Execute Task                    │
│  await agent.RunAsync(task)      │
└──────────┬───────────────────────┘
           │
           ▼
┌──────────────────────────────────┐
│  Handle Result                   │
│  - Success: ✓ Task N completed   │
│  - Failure: ✗ Task N failed      │
└──────────┬───────────────────────┘
           │
           ▼
     Next Task or Complete
```

**Key Features**:
- **Context Isolation**: Each task gets a new agent with empty conversation history
- **Error Handling**: Failures in one task don't stop subsequent tasks
- **Progress Tracking**: Shows "Task N/Total" for each execution
- **Status Reporting**: Individual success/failure status per task
- **Batch Processing**: Supports comma-separated tasks or interactive input

**Task Input Methods**:
1. Command-line comma-separated: `--task="Task 1,Task 2,Task 3"`
2. Interactive multi-line input: Prompts for tasks until empty line
3. Configuration array: `"Tasks": ["Task 1", "Task 2"]`

---

## 3. Component Specifications

### 3.1 Agent Core (`Agent.cs`)

**Purpose**: Orchestrates the conversation loop between user, LLM, and tools.

**Key Responsibilities**:
- Manage conversation history
- Send messages to LLM provider
- Process LLM responses (text, tool calls, errors)
- Execute tools and collect results
- Handle iteration limits
- Display formatted output with Spectre.Console

**Class Structure**:
```csharp
public abstract class Agent
{
    // Dependencies
    private readonly ILlmProvider _llmProvider;
    private readonly List<Tool> _tools;
    private readonly string _workingDirectory;
    private readonly bool _verbose;
    
    // Abstract members
    protected abstract string SystemPrompt { get; }
    protected virtual List<Tool> CreateTools() { ... }
    
    // Main execution loop
    public async Task<List<Message>> RunAsync(string task, int maxIterations = 10)
    {
        // 1. Initialize conversation with user task
        // 2. For each iteration:
        //    a. Send conversation to LLM
        //    b. Parse response
        //    c. Execute tools if requested
        //    d. Add results to conversation
        //    e. Display output
        // 3. Return conversation history
    }
}
```

**UI Integration**:
- Uses Spectre.Console `Rule` for iteration headers
- Uses Spectre.Console `Panel` for tool calls, results, and messages
- Color-codes stop reasons (yellow=tool_use, green=end_turn, red=error)
- Displays tool execution with rounded borders
- Shows final messages with double borders

### 3.2 LLM Provider System

**Interface**: `ILlmProvider`
```csharp
public interface ILlmProvider
{
    string Name { get; }
    Task<LlmResponse> SendMessageAsync(
        List<Message> messages, 
        List<Tool> tools, 
        string systemPrompt
    );
}
```

**Base Class**: `LlmProviderBase`
- Provides `BuildOpenAiStyleMessages()` for OpenAI-compatible APIs
- Provides `BuildOpenAiStyleTools()` for function calling schema
- Provides `ParseOpenAiStyleResponse()` for response parsing
- Handles ContentBlock to API format conversions

**Provider Implementations**:

| Provider | API Format | Authentication | Special Features |
|----------|-----------|----------------|------------------|
| OpenAI | Function Calling | API Key | Standard OpenAI format |
| Claude | Tools API | API Key | Native ContentBlock format |
| Gemini | Function Declarations | API Key | Custom parts structure |
| Azure OpenAI | Function Calling | API Key + Endpoint | Deployment-based |
| Ollama | Function Calling | None (local) | Local model support |
| GitHub Copilot | Function Calling | OAuth Device Flow | Token refresh logic |
| Z.AI | Function Calling | API Key | GLM models, deep thinking mode |
| vLLM | Function Calling | None (local) | High-performance local inference |
| SGLang | Function Calling | None (local) | Structured generation support |
| LlamaCpp | Function Calling | None (local) | GGUF model support |

**Message Format Conversions**:

**ContentBlock → OpenAI Format**:
```csharp
// Assistant message with tool_use
{
    role: "assistant",
    content: null,
    tool_calls: [{
        id: "call_123",
        type: "function",
        function: {
            name: "read_file",
            arguments: "{\"file_path\":\"test.txt\"}"
        }
    }]
}

// Tool result message
{
    role: "tool",
    tool_call_id: "call_123",
    content: "file contents"
}
```

**ContentBlock → Claude Format**:
```csharp
{
    role: "assistant",
    content: [{
        type: "tool_use",
        id: "toolu_123",
        name: "read_file",
        input: { file_path: "test.txt" }
    }]
}
```

**ContentBlock → Gemini Format**:
```csharp
{
    role: "model",
    parts: [{
        functionCall: {
            name: "read_file",
            args: { file_path: "test.txt" }
        }
    }]
}
```

### 3.3 Tool System

**Base Class**: `Tool`
```csharp
public abstract class Tool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract object? InputSchema { get; }
    public abstract string Execute(
        string workingDirectory, 
        Dictionary<string, object> input
    );
}
```

**Tool Catalog**:

1. **ListFiles**: Directory listing with recursive option
   - Security: Path validation via `PathHelper.IsPathSafe()`
   - Output: Newline-separated file paths

2. **ReadFile**: Read file contents
   - Security: Path validation, workspace restriction
   - Output: Full file contents as string

3. **WriteFile**: Create/modify files
   - Security: Path validation, directory creation option
   - Output: "OK" or error message

4. **SearchCode**: Grep-like code search
   - Features: Regex support, file glob patterns, recursive
   - Output: `path:line: content` format

5. **RunCommand**: Execute shell commands
   - Security: Timeout (default 120s), working directory locked
   - Output: Combined stdout/stderr

6. **DisplayText**: Show formatted text to user
   - UI: Blue rounded panel with optional title
   - Output: "Text displayed successfully"

7. **AskUser**: Interactive user prompts
   - UI: Cyan double-border panel with context
   - Input: Spectre.Console styled prompt
   - Output: User's response

### 3.4 OAuth Service (`GitHubOAuthService`)

**Purpose**: Handle GitHub OAuth device flow for Copilot authentication.

**Flow**:
```
1. Request Device Code
   POST https://github.com/login/device/code
   ↓
2. Display Code to User
   "Enter code: ABC-DEF"
   "Visit: https://github.com/login/device"
   ↓
3. Poll for Token
   POST https://github.com/login/oauth/access_token
   (Every 5 seconds until authorized)
   ↓
4. Store Token
   Save to ~/.dracode/tokens.json
   ↓
5. Refresh When Needed
   Token valid for 8 hours
   Auto-refresh on expiration
```

**Token Storage**:
```json
{
  "access_token": "gho_...",
  "expires_at": "2026-01-20T18:00:00Z",
  "refresh_token": "ghr_...",
  "refresh_token_expires_at": "2026-07-20T10:00:00Z"
}
```

---

## 4. Data Flow

### 4.1 Message Flow Diagram

```
User Task
    │
    ▼
┌─────────────────────────────────────┐
│ Program.cs: Parse Configuration     │
│ - Load appsettings.json              │
│ - Check for --provider argument      │
│ - Show provider selection if needed  │
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│ AgentFactory.Create()                │
│ - Instantiate provider               │
│ - Create CodingAgent                 │
│ - Configure tools                    │
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────┐
│ Agent.RunAsync(task)                                │
│                                                      │
│  ┌────────────────────────────────────────────┐    │
│  │ Iteration 1                                 │    │
│  │ ┌────────────────────────────────────────┐ │    │
│  │ │ 1. Send to LLM                         │ │    │
│  │ │    messages: [{"role":"user","content":│ │    │
│  │ │               "task description"}]      │ │    │
│  │ │    tools: [list_files, read_file, ...] │ │    │
│  │ │    system_prompt: "You are a coding   "│ │    │
│  │ └────────────────────────────────────────┘ │    │
│  │                    │                        │    │
│  │                    ▼                        │    │
│  │ ┌────────────────────────────────────────┐ │    │
│  │ │ 2. LLM Responds                        │ │    │
│  │ │    stop_reason: "tool_use"             │ │    │
│  │ │    content: [{                         │ │    │
│  │ │      type: "tool_use",                 │ │    │
│  │ │      name: "read_file",                │ │    │
│  │ │      input: {file_path: "test.txt"}    │ │    │
│  │ │    }]                                   │ │    │
│  │ └────────────────────────────────────────┘ │    │
│  │                    │                        │    │
│  │                    ▼                        │    │
│  │ ┌────────────────────────────────────────┐ │    │
│  │ │ 3. Execute Tool                        │ │    │
│  │ │    tool = tools.Find("read_file")      │ │    │
│  │ │    result = tool.Execute(wd, input)    │ │    │
│  │ │    result = "File contents: ..."       │ │    │
│  │ └────────────────────────────────────────┘ │    │
│  │                    │                        │    │
│  │                    ▼                        │    │
│  │ ┌────────────────────────────────────────┐ │    │
│  │ │ 4. Add Tool Result to Conversation     │ │    │
│  │ │    messages.Add({                      │ │    │
│  │ │      role: "user",                     │ │    │
│  │ │      content: [{                       │ │    │
│  │ │        type: "tool_result",            │ │    │
│  │ │        tool_use_id: "...",             │ │    │
│  │ │        content: "File contents: ..."   │ │    │
│  │ │      }]                                 │ │    │
│  │ │    })                                   │ │    │
│  │ └────────────────────────────────────────┘ │    │
│  └────────────────────────────────────────────┘    │
│                                                      │
│  [Repeat for Iteration 2, 3, ... until end_turn]   │
│                                                      │
└──────────────────────────────────────────────────────┘
```

### 4.2 Tool Execution Flow

```
LLM requests tool
    │
    ▼
┌────────────────────────────────┐
│ Agent receives tool_use block  │
│ - Extract tool name             │
│ - Extract input parameters      │
└────────┬───────────────────────┘
         │
         ▼
┌────────────────────────────────┐
│ Find matching Tool by name     │
│ var tool = _tools.Find(name)   │
└────────┬───────────────────────┘
         │
         ▼
┌────────────────────────────────┐
│ Tool.Execute(wd, input)        │
│ - Validate inputs               │
│ - Check path safety            │
│ - Perform action               │
│ - Return result string         │
└────────┬───────────────────────┘
         │
         ▼
┌────────────────────────────────┐
│ Agent wraps result             │
│ {                               │
│   type: "tool_result",          │
│   tool_use_id: "...",           │
│   content: result               │
│ }                               │
└────────┬───────────────────────┘
         │
         ▼
┌────────────────────────────────┐
│ Add to conversation             │
│ Send back to LLM               │
└────────────────────────────────┘
```

---

## 5. Provider Integration

### 5.1 Provider Registration

**AgentFactory.cs**:
```csharp
public static Agent Create(
    string type, 
    string workingDirectory, 
    bool verbose, 
    Dictionary<string, string> config)
{
    ILlmProvider provider = type.ToLowerInvariant() switch
    {
        "openai" => new OpenAiProvider(
            apiKey: config["apiKey"],
            model: config["model"]
        ),
        "claude" => new ClaudeProvider(
            apiKey: config["apiKey"],
            model: config["model"]
        ),
        // ... other providers
    };
    
    return new CodingAgent(provider, workingDirectory, verbose);
}
```

### 5.2 Adding New Provider

**Steps**:
1. Create provider class implementing `ILlmProvider`
2. Implement `SendMessageAsync()` with API-specific logic
3. Add message format conversion if not OpenAI-compatible
4. Add case to `AgentFactory.Create()`
5. Add configuration to `appsettings.json`
6. Add icon to provider selection menu in `Program.cs`

**Example Template**:
```csharp
public class NewProvider : LlmProviderBase
{
    public override string Name => "New Provider";
    
    public override async Task<LlmResponse> SendMessageAsync(
        List<Message> messages, 
        List<Tool> tools, 
        string systemPrompt)
    {
        // 1. Convert messages to provider format
        var apiMessages = ConvertMessages(messages, systemPrompt);
        
        // 2. Send HTTP request
        var response = await _httpClient.PostAsync(url, content);
        
        // 3. Parse response
        return ParseResponse(responseJson);
    }
    
    protected override bool IsConfigured() => /* check config */;
}
```

---

## 6. UI/UX Design

### 6.1 Spectre.Console Integration

**Banner Display**:
```csharp
var banner = new FigletText("DraCode")
    .Centered()
    .Color(Color.Cyan1);
AnsiConsole.Write(banner);
```

**Configuration Table**:
```csharp
var table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("[cyan]Setting[/]")
    .AddColumn("[white]Value[/]")
    .AddRow("[cyan]Provider[/]", $"[yellow]{provider}[/]");
```

**Provider Selection Menu**:
```csharp
var prompt = new SelectionPrompt<string>()
    .Title("[bold cyan]Select an AI Provider:[/]")
    .PageSize(10)
    .AddChoices(providers);
```

**Tool Call Panels**:
```csharp
var panel = new Panel(content)
{
    Border = BoxBorder.Rounded,
    BorderStyle = new Style(Color.Yellow),
    Header = new PanelHeader("🔧 Tool Call", Justify.Left)
};
```

### 6.2 Color Scheme

| Element | Color | Purpose |
|---------|-------|---------|
| Banner | Cyan | Brand identity |
| Tool calls | Yellow | Action indication |
| Success results | Green | Positive feedback |
| Error results | Red | Error indication |
| Final messages | Green (double border) | Completion |
| User prompts | Cyan (double border) | Input needed |
| System info | Grey/Dim | Low priority |

---

## 7. Security Architecture

### 7.1 Sandboxing

**Path Validation** (`PathHelper.IsPathSafe()`):
```csharp
public static bool IsPathSafe(string path, string workingDirectory)
{
    var fullPath = Path.GetFullPath(path, workingDirectory);
    var workingPath = Path.GetFullPath(workingDirectory);
    return fullPath.StartsWith(workingPath, 
        StringComparison.OrdinalIgnoreCase);
}
```

**Enforcement Points**:
- `ListFiles.Execute()` - Validates directory path
- `ReadFile.Execute()` - Validates file path
- `WriteFile.Execute()` - Validates file path
- `SearchCode.Execute()` - Validates directory path

**Blocked Operations**:
- `../../../etc/passwd` ❌
- `/tmp/malicious` ❌
- `C:\Windows\System32` ❌
- Absolute paths outside workspace ❌

### 7.2 Command Execution Safety

**RunCommand Security**:
- No shell execution (`UseShellExecute = false`)
- Timeout protection (default 120 seconds)
- Working directory locked to workspace
- Process killed on timeout
- No elevated privileges

### 7.3 Token Security

**OAuth Token Storage**:
- Location: `~/.dracode/tokens.json`
- Permissions: User-only (600 on Unix)
- Not committed to git (in .gitignore)
- Encrypted at rest (OS file system encryption)

**API Key Security**:
- Environment variables preferred
- `appsettings.local.json` gitignored
- Never logged or displayed
- Not sent to LLM

---

## 8. Configuration System

### 8.1 Configuration Hierarchy

```
Priority (highest to lowest):
1. Command-line arguments (--provider=, --task=)
2. Environment variables (OPENAI_API_KEY)
3. appsettings.local.json (gitignored)
4. appsettings.json (defaults)
```

### 8.2 Configuration Schema

```json
{
  "Agent": {
    "Provider": "openai",           // Default provider
    "WorkingDirectory": "./",        // Sandbox directory
    "Verbose": true,                 // Enable detailed output
    "Tasks": [],                     // List of tasks to execute (optional)
    "Providers": {
      "openai": {
        "type": "openai",
        "apiKey": "${OPENAI_API_KEY}",
        "model": "gpt-4o",
        "baseUrl": "https://api.openai.com/v1/chat/completions"
      },
      "claude": {
        "type": "claude",
        "apiKey": "${ANTHROPIC_API_KEY}",
        "model": "claude-3-5-sonnet-latest",
        "baseUrl": "https://api.anthropic.com/v1/messages"
      },
      "gemini": {
        "type": "gemini",
        "apiKey": "${GEMINI_API_KEY}",
        "model": "gemini-2.0-flash-exp",
        "baseUrl": "https://generativelanguage.googleapis.com/v1beta/models/"
      },
      "azureopenai": {
        "type": "azureopenai",
        "endpoint": "${AZURE_OPENAI_ENDPOINT}",
        "apiKey": "${AZURE_OPENAI_API_KEY}",
        "deployment": "gpt-4"
      },
      "ollama": {
        "type": "ollama",
        "model": "llama3.2",
        "baseUrl": "http://localhost:11434"
      },
      "githubcopilot": {
        "type": "githubcopilot",
        "clientId": "${GITHUB_CLIENT_ID}",
        "model": "gpt-4o",
        "baseUrl": "https://api.githubcopilot.com/chat/completions"
      }
    }
  }
}
```

### 8.3 Provider Icons

Used in selection menu:
- 🤖 OpenAI
- 🧠 Claude/Anthropic
- ✨ Gemini/Google
- 🐙 GitHub Copilot
- ☁️ Azure OpenAI
- 🦙 Ollama
- 🐉 Z.AI (Zhipu GLM)
- ⚡ vLLM
- 🔮 SGLang
- 🦙 LlamaCpp
- 🔧 Default/Other

---

## 9. Performance Considerations

### 9.1 Async/Await Pattern
- All I/O operations are asynchronous
- HTTP requests use `HttpClient` with async methods
- File operations use `File.ReadAllTextAsync()` / `File.WriteAllTextAsync()`

### 9.2 HTTP Client Management
- Single `HttpClient` instance per provider (avoid socket exhaustion)
- Configurable timeout for Ollama (5 minutes for local models)
- Reuse connections with keep-alive

### 9.3 Token Refresh Optimization
- Check token expiration before every request
- Only refresh when within 5 minutes of expiration
- Cache tokens in memory and disk

---

## 10. Error Handling

### 10.1 Error Categories

| Category | Handling | UI Feedback |
|----------|----------|-------------|
| LLM API Error | Return error response | Red panel with error message |
| Tool Execution Error | Return error string | Red panel with "Error:" prefix |
| Configuration Error | Exit with error message | Red text on stderr |
| OAuth Error | Retry with backoff | Status messages, wait prompts |
| Timeout Error | Kill process, return timeout message | Yellow warning |

### 10.2 Stop Reasons

| Stop Reason | Meaning | Agent Action |
|------------|---------|--------------|
| `tool_use` | LLM wants to use tools | Execute tools, continue |
| `end_turn` | Task complete | Display response, exit |
| `error` | API error occurred | Display error, exit |
| `NotConfigured` | Provider not configured | Display config error, exit |
| Other | Unexpected state | Display warning, exit |

---

## 11. Future Architecture Considerations

### 11.1 Current Enhancements (Implemented)
- **17 Specialized Agent Types**: Coding (C#, C++, JavaScript, TypeScript, PHP, Python, etc.), Web (HTML, CSS, React, Angular), Media (SVG, Bitmap, Image), and Diagramming
- **KoboldLair Multi-Agent System**: Autonomous hierarchical system (Dragon → Wyvern → Drake → Kobold)
- **Git Integration**: GitService, GitStatusTool, GitMergeTool for version control
- **Per-Agent-Type Provider Configuration**: Different LLM providers for different Kobold agent types

### 11.2 Planned Enhancements
- **Streaming responses**: Real-time token streaming from LLMs
- **Persistent memory**: Agent memory across sessions
- **Plugin system**: Dynamic tool loading from assemblies

### 11.3 Scalability
- KoboldLair provides multi-agent concurrent task handling
- Per-project resource limits (maxParallelKobolds)
- Tool system is thread-safe and stateless
- Provider instances are managed per agent type
- Background services run every 60 seconds for automatic processing

---

**End of Architecture Specification**
