# DraCode Technical Specification Document

**Version:** 2.5.1
**Last Updated:** February 6, 2026
**Document Type:** Comprehensive Technical Specification

---

## 1. PROJECT OVERVIEW

### 1.1 Project Type
**DraCode** is an AI-powered coding agent CLI application that leverages Large Language Models (LLMs) to autonomously perform coding tasks within a sandboxed workspace.

### 1.2 Technology Stack
- **Language:** C# 14.0
- **Framework:** .NET 10.0
- **Project Type:** Console Application (.NET SDK)
- **Architecture:** Modular, Provider-based Architecture with Tool System
- **Build System:** MSBuild (via .NET CLI)

### 1.3 Core Dependencies
```xml
<PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
```
- System.Text.Json (built-in)
- System.Net.Http (built-in)
- System.Diagnostics (built-in)

---

## 2. ARCHITECTURE

### 2.1 High-Level Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                        DraCode CLI                          │
│                      (Entry Point)                          │
└──────────────────┬──────────────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────────────┐
│                      Agent Core                             │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────┐  │
│  │ Conversation │  │   Tools      │  │  LLM Provider   │  │
│  │   Manager    │  │   System     │  │    Interface    │  │
│  └──────────────┘  └──────────────┘  └─────────────────┘  │
└──────────────────┬──────────────────┬────────────┬─────────┘
                   │                  │            │
         ┌─────────▼──────┐  ┌────────▼─────┐  ┌──▼──────────────┐
         │  Tool Executor │  │ File System  │  │  LLM Providers  │
         │  - ListFiles   │  │   Safety     │  │  - OpenAI       │
         │  - ReadFile    │  │   PathHelper │  │  - Claude       │
         │  - WriteFile   │  └──────────────┘  │  - Gemini       │
         │  - SearchCode  │                    │  - Azure OpenAI │
         │  - RunCommand  │                    │  - Ollama       │
         └────────────────┘                    │  - GitHubCopilot│
                                               └─────────────────┘
```

### 2.2 Design Patterns
1. **Factory Pattern** - `AgentFactory` creates agents with specific LLM providers
2. **Strategy Pattern** - Interchangeable LLM providers via `ILlmProvider` interface
3. **Template Method** - `LlmProviderBase` provides common functionality for OpenAI-compatible APIs
4. **Command Pattern** - Tools encapsulate actions as executable objects
5. **Dependency Injection** - Configuration-based provider selection

### 2.3 Key Architectural Principles
- **Sandboxing:** All file operations are restricted to the working directory
- **Provider Abstraction:** LLM providers are interchangeable without code changes
- **Tool-based Extensibility:** New capabilities can be added as Tool implementations
- **Stateless Tools:** Tools don't maintain state between executions
- **Async-First:** All I/O operations use async/await pattern

---

## 3. MAIN FEATURES AND FUNCTIONALITY

### 3.1 Core Features

#### 3.1.1 Multi-Provider LLM Support
Supports 10 LLM providers with unified interface:
- **OpenAI** (GPT-4, GPT-4o, GPT-3.5-turbo)
- **Claude** (Anthropic - Claude 3.5 Sonnet, Haiku)
- **Gemini** (Google - Gemini 2.0 Flash)
- **Azure OpenAI** (Enterprise deployment)
- **Ollama** (Local models - Llama, Mistral, etc.)
- **GitHub Copilot** (with OAuth device flow)
- **Z.AI** (Zhipu GLM models - glm-4.5, glm-4.6, glm-4.7)
- **vLLM** (Local inference via OpenAI-compatible API)
- **SGLang** (Local inference via OpenAI-compatible API)
- **LlamaCpp** (GGUF models via OpenAI-compatible API)

#### 3.1.2 Multi-Task Execution System
- **Sequential Execution:** Execute multiple tasks in order
- **Context Isolation:** Each task gets a fresh agent instance
- **Progress Tracking:** Visual progress indicators (Task N/Total)
- **Error Handling:** Failures don't stop subsequent tasks
- **Batch Processing:** Comma-separated or interactive multi-line input
- **Configuration Support:** Define tasks in JSON array

#### 3.1.3 Autonomous Agent System
- **Conversational Loop:** Multi-turn conversation with LLM
- **Tool Calling:** LLM can invoke tools to perform actions
- **Iterative Execution:** Max 10 iterations per task (configurable)
- **Self-directed:** Agent decides which tools to use and when

#### 3.1.4 Tool System
Seven built-in tools for code manipulation:

1. **list_files** - Directory listing with recursive option
2. **read_file** - Read file contents
3. **write_file** - Create/overwrite files with directory creation
4. **search_code** - Regex/text search with line numbers
5. **run_command** - Execute shell commands with timeout
6. **ask_user** - Interactive prompts during execution
7. **display_text** - Formatted text output with panels

#### 3.1.5 GitHub Copilot OAuth Integration
- **Device Flow Authentication:** Browser-based OAuth flow
- **Token Management:** Automatic refresh and persistence
- **Token Storage:** Secure file-based storage in user directory
- **Expiration Handling:** Auto-refresh before expiration

#### 3.1.6 Interactive CLI UI
- **Provider Selection Menu:** Visual menu for choosing LLM providers
- **Verbose Mode Selection:** Toggle between detailed/quiet output
- **Multi-Task Input:** Interactive prompt for multiple tasks
- **Formatted Output:** Spectre.Console panels, rules, and colors
- **Progress Indicators:** Visual feedback for task execution

#### 3.1.7 Configuration System
- **JSON Configuration:** appsettings.json (defaults) + appsettings.local.json (overrides)
- **Environment Variables:** Override config values via environment
- **CLI Arguments:** Runtime provider and task selection
- **File-based Tasks:** Load task from file path

---

## 4. DATA MODELS AND STRUCTURES

### 4.1 Core Domain Models

#### 4.1.1 Message
```csharp
public class Message
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }  // "user" | "assistant"
    
    [JsonPropertyName("content")]
    public object? Content { get; set; }  // string | List<ContentBlock> | List<object>
}
```

#### 4.1.2 ContentBlock
```csharp
public class ContentBlock
{
    public string? Type { get; set; }  // "text" | "tool_use"
    public string? Text { get; set; }  // For type="text"
    
    // For type="tool_use"
    public string? Id { get; set; }
    public string? Name { get; set; }
    public Dictionary<string, object>? Input { get; set; }
}
```

#### 4.1.3 LlmResponse
```csharp
public class LlmResponse
{
    public string? StopReason { get; set; }  // "end_turn" | "tool_use" | "NotConfigured" | "error"
    public List<ContentBlock>? Content { get; set; }
}
```

#### 4.1.4 Tool (Abstract)
```csharp
public abstract class Tool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract object? InputSchema { get; }  // JSON Schema object
    
    public abstract string Execute(string workingDirectory, Dictionary<string, object> input);
}
```

#### 4.1.5 TokenInfo (OAuth)
```csharp
public class TokenInfo
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string TokenType { get; set; } = "Bearer";
}
```

### 4.2 Configuration Models

#### 4.2.1 Agent Configuration Structure
```json
{
  "Agent": {
    "Provider": "string",           // Default provider name
    "WorkingDirectory": "string",   // Default workspace path
    "Verbose": boolean,             // Enable detailed logging
    "Tasks": ["string"],            // Array of tasks to execute sequentially
    "Providers": {
      "providername": {
        "type": "string",           // Provider type identifier
        "apiKey": "string",         // API key (optional)
        "model": "string",          // Model name
        "baseUrl": "string",        // API endpoint
        // Provider-specific fields
      }
    }
  }
}
```

---

## 5. USER FLOWS AND KEY INTERACTIONS

### 5.1 Primary User Flow

```
1. User invokes CLI
   ↓
2. Program.cs loads configuration
   - Check for appsettings.local.json
   - Fall back to appsettings.json
   - Apply environment variable overrides
   ↓
3. Parse command-line arguments
   - Extract --provider flag
   - Extract --task flag or prompt user
   ↓
4. AgentFactory creates agent with selected provider
   ↓
5. Agent.RunAsync() begins execution loop
   ↓
6. LOOP (max 10 iterations):
   a. Send conversation to LLM with tools and system prompt
   b. Parse LLM response
   c. IF stop_reason = "tool_use":
      - Execute each tool call
      - Append tool results to conversation
      - Continue loop
   d. IF stop_reason = "end_turn":
      - Display final response
      - Break loop
   e. ELSE:
      - Handle error/unexpected stop
      - Break loop
   ↓
7. Return conversation history
```

### 5.2 GitHub Copilot OAuth Flow

```
1. User selects githubcopilot provider
   ↓
2. GitHubCopilotProvider checks for existing token
   ↓
3. IF no token OR expired:
   a. GitHubOAuthService.AuthenticateAsync()
   b. Request device code from GitHub
   c. Display URL and user code to user
   d. Poll GitHub for authorization (max 10 minutes)
   e. Save token to ~/.dracode/github_token.json
   ↓
4. Provider uses token in Authorization header
   ↓
5. IF 401 response:
   - Attempt token refresh
   - Retry request
   ↓
6. Continue with normal LLM interaction
```

### 5.3 Tool Execution Flow

```
1. LLM responds with tool_use content block
   ↓
2. Agent extracts tool name and parameters
   ↓
3. Lookup tool in registered tools list
   ↓
4. Tool.Execute(workingDirectory, input)
   ↓
5. Tool performs safety checks:
   - PathHelper.IsPathSafe() for file operations
   - Timeout validation for commands
   ↓
6. Tool executes action:
   - File I/O
   - Process execution
   - Directory traversal
   ↓
7. Tool returns result string
   ↓
8. Result added to conversation as tool_result
   ↓
9. Conversation sent back to LLM
```

---

## 6. DEPENDENCIES, INTEGRATIONS, AND EXTERNAL SERVICES

### 6.1 External LLM APIs

#### 6.1.1 OpenAI API
- **Endpoint:** `https://api.openai.com/v1/chat/completions`
- **Authentication:** Bearer token (API key)
- **Models:** gpt-4o, gpt-4, gpt-3.5-turbo
- **Protocol:** OpenAI Chat Completions API

#### 6.1.2 Anthropic Claude API
- **Endpoint:** `https://api.anthropic.com/v1/messages`
- **Authentication:** x-api-key header
- **API Version:** 2023-06-01
- **Models:** claude-3-5-sonnet-latest, claude-3-5-haiku-latest
- **Protocol:** Anthropic Messages API

#### 6.1.3 Google Gemini API
- **Endpoint:** `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`
- **Authentication:** API key in query string
- **Models:** gemini-2.0-flash-exp
- **Protocol:** Google Generative AI API

#### 6.1.4 Azure OpenAI Service
- **Endpoint:** Custom (user-provided)
- **Authentication:** api-key header
- **API Version:** 2024-02-15-preview
- **Protocol:** Azure OpenAI Chat Completions API

#### 6.1.5 Ollama (Local)
- **Endpoint:** `http://localhost:11434/api/chat`
- **Authentication:** None (local)
- **Models:** User-installed (llama3.2, mistral, etc.)
- **Protocol:** OpenAI-compatible API

#### 6.1.6 GitHub Copilot API
- **Endpoint:** `https://api.githubcopilot.com/chat/completions`
- **Authentication:** OAuth Bearer token
- **OAuth Endpoint:** `https://github.com/login/device/code`
- **Models:** gpt-4o, gpt-4-turbo
- **Protocol:** OpenAI-compatible with OAuth

### 6.2 OAuth Integration (GitHub)
- **OAuth Flow:** Device Authorization Grant (RFC 8628)
- **Device Code Endpoint:** `https://github.com/login/device/code`
- **Token Endpoint:** `https://github.com/login/oauth/access_token`
- **Scopes:** `read:user` (minimal)
- **Token Refresh:** Supported via refresh_token

### 6.3 System Dependencies
- **.NET 10.0 Runtime** - Required for execution
- **File System Access** - Sandboxed to working directory
- **Process Execution** - Via System.Diagnostics.Process
- **Network Access** - HTTPS for API calls

---

## 7. FILE AND FOLDER STRUCTURE

### 7.1 Solution Structure
```
DraCode/
├── DraCode.slnx                        # Solution file
├── DraCode/                            # Main CLI project
│   ├── DraCode.csproj                  # Project file
│   ├── Program.cs                      # Entry point
│   ├── appsettings.json                # Default configuration
│   ├── appsettings.local.json          # Local overrides (gitignored)
│   ├── GITHUB_OAUTH_SETUP.md           # OAuth setup guide
│   └── Properties/
│       └── launchSettings.json         # Debug profiles
│
└── DraCode.Agent/                      # Agent library
    ├── DraCode.Agent.csproj            # Project file
    ├── Agent.cs                        # Main agent Wyvern
    ├── AgentFactory.cs                 # Provider factory
    │
    ├── Auth/                           # OAuth components
    │   ├── GitHubOAuthService.cs       # Device flow implementation
    │   └── TokenStorage.cs             # Token persistence
    │
    ├── Helpers/                        # Utility classes
    │   └── PathHelper.cs               # Path safety validation
    │
    ├── LLMs/                           # LLM abstractions
    │   ├── Message.cs                  # Conversation message
    │   ├── ContentBlock.cs             # Content representation
    │   ├── LlmResponse.cs              # LLM response wrapper
    │   │
    │   └── Providers/                  # LLM implementations
    │       ├── ILlmProvider.cs         # Provider interface
    │       ├── LlmProviderBase.cs      # OpenAI-compatible base
    │       ├── OpenAiProvider.cs       # OpenAI implementation
    │       ├── ClaudeProvider.cs       # Anthropic implementation
    │       ├── GeminiProvider.cs       # Google implementation
    │       ├── AzureOpenAiProvider.cs  # Azure implementation
    │       ├── OllamaProvider.cs       # Local Ollama
    │       └── GitHubCopilotProvider.cs # GitHub Copilot w/ OAuth
    │
    └── Tools/                          # Tool implementations
        ├── Tool.cs                     # Abstract tool base
        ├── ListFiles.cs                # Directory listing
        ├── ReadFile.cs                 # File reading
        ├── WriteFile.cs                # File writing
        ├── SearchCode.cs               # Code search (grep)
        └── RunCommand.cs               # Command execution
```

### 7.2 Runtime Directories
```
~/.dracode/                             # User data directory
└── github_token.json                   # OAuth tokens (DO NOT COMMIT)
```

---

## 8. CONFIGURATION REQUIREMENTS AND ENVIRONMENT VARIABLES

### 8.1 Configuration Files

#### 8.1.1 appsettings.json (Defaults)
Primary configuration file with placeholder values for all providers.

#### 8.1.2 appsettings.local.json (User-specific)
Overrides defaults with actual API keys and preferences. Should be gitignored.

#### 8.1.3 Configuration Priority
1. **Command-line arguments** (highest)
2. **Environment variables**
3. **appsettings.local.json**
4. **appsettings.json** (lowest)

### 8.2 Environment Variables

#### 8.2.1 Required per Provider

**OpenAI:**
- `OPENAI_API_KEY` - API key from OpenAI platform

**Claude:**
- `ANTHROPIC_API_KEY` - API key from Anthropic

**Gemini:**
- `GEMINI_API_KEY` - API key from Google AI Studio

**Azure OpenAI:**
- `AZURE_OPENAI_ENDPOINT` - Full endpoint URL
- `AZURE_OPENAI_API_KEY` - Azure subscription key

**GitHub Copilot:**
- `GITHUB_CLIENT_ID` - OAuth app client ID

**Ollama:**
- No credentials required (local)

#### 8.2.2 Optional Environment Variables
- `APPSETTINGS_PATH` - Override default config file location

### 8.3 Command-Line Arguments

```bash
--provider=<name>     # Override provider selection
--task=<text|path>    # Task description or file path
```

**Examples:**
```bash
dotnet run --provider=openai --task="Create a hello world app"
dotnet run --provider=claude --task="tasks/refactor.txt"
```

---

## 9. APIs, ENDPOINTS, AND INTERFACES

### 9.1 Public API (CLI)

#### 9.1.1 Command-Line Interface
```
DraCode.exe [--provider=<name>] [--task=<text|path>]
```

**Exit Codes:**
- `0` - Success
- Non-zero - Error (uncaught exceptions)

### 9.2 Internal APIs

#### 9.2.1 ILlmProvider Interface
```csharp
public interface ILlmProvider
{
    Task<LlmResponse> SendMessageAsync(
        List<Message> messages, 
        List<Tool> tools, 
        string systemPrompt
    );
    
    string Name { get; }
}
```

**Contract:**
- **Input:** Conversation history, available tools, system instructions
- **Output:** Response with content blocks and stop reason
- **Behavior:** Must handle tool schema serialization per provider format

#### 9.2.2 Tool Abstract Class
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

**Contract:**
- **Name:** Unique identifier for LLM to reference
- **Description:** Human-readable explanation for LLM
- **InputSchema:** JSON Schema for parameters
- **Execute:** Synchronous execution returning string result

### 9.3 LLM Provider Request/Response Formats

#### 9.3.1 OpenAI Format (OpenAI, Azure, Ollama, GitHub Copilot)
**Request:**
```json
{
  "model": "gpt-4o",
  "messages": [
    {"role": "system", "content": "..."},
    {"role": "user", "content": "..."}
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "tool_name",
        "description": "...",
        "parameters": { /* JSON Schema */ }
      }
    }
  ]
}
```

**Response:**
```json
{
  "choices": [{
    "message": {
      "content": "text response",
      "tool_calls": [{
        "id": "call_abc123",
        "type": "function",
        "function": {
          "name": "tool_name",
          "arguments": "{\"param\":\"value\"}"
        }
      }]
    }
  }]
}
```

#### 9.3.2 Claude Format
**Request:**
```json
{
  "model": "claude-3-5-sonnet-latest",
  "max_tokens": 4096,
  "system": "system prompt",
  "messages": [
    {"role": "user", "content": "..."}
  ],
  "tools": [
    {
      "name": "tool_name",
      "description": "...",
      "input_schema": { /* JSON Schema */ }
    }
  ]
}
```

**Response:**
```json
{
  "stop_reason": "tool_use",
  "content": [
    {
      "type": "tool_use",
      "id": "toolu_abc123",
      "name": "tool_name",
      "input": {"param": "value"}
    }
  ]
}
```

#### 9.3.3 Gemini Format
**Request:**
```json
{
  "contents": [
    {
      "role": "user",
      "parts": [{"text": "..."}]
    }
  ],
  "systemInstruction": {
    "parts": [{"text": "system prompt"}]
  },
  "tools": [{
    "functionDeclarations": [{
      "name": "tool_name",
      "description": "...",
      "parameters": { /* JSON Schema */ }
    }]
  }]
}
```

**Response:**
```json
{
  "candidates": [{
    "content": {
      "parts": [{
        "functionCall": {
          "name": "tool_name",
          "args": {"param": "value"}
        }
      }]
    }
  }]
}
```

---

## 10. BUSINESS LOGIC, VALIDATION RULES, AND ALGORITHMS

### 10.1 Core Business Logic

#### 10.1.1 Agent Loop Algorithm
```
FUNCTION RunAsync(task, maxIterations=10):
    conversation = [{"role": "user", "content": task}]
    
    FOR iteration = 1 TO maxIterations:
        response = LLM.SendMessage(conversation, tools, systemPrompt)
        conversation.append({"role": "assistant", "content": response.content})
        
        IF response.stopReason == "tool_use":
            toolResults = []
            FOR each toolBlock IN response.content WHERE type="tool_use":
                tool = FindTool(toolBlock.name)
                result = tool.Execute(workingDirectory, toolBlock.input)
                toolResults.append({
                    "type": "tool_result",
                    "tool_use_id": toolBlock.id,
                    "content": result
                })
            conversation.append({"role": "user", "content": toolResults})
            CONTINUE
        
        ELSE IF response.stopReason == "end_turn":
            BREAK
        
        ELSE:
            HANDLE_ERROR(response.stopReason)
            BREAK
    
    RETURN conversation
```

#### 10.1.2 Path Safety Validation
```
FUNCTION IsPathSafe(path, workingDirectory):
    fullPath = GetFullPath(path)
    workingPath = GetFullPath(workingDirectory)
    RETURN fullPath.StartsWith(workingPath)
```

**Purpose:** Prevent directory traversal attacks (e.g., `../../etc/passwd`)

#### 10.1.3 OAuth Token Management
```
FUNCTION GetValidToken(forceRefresh=false):
    token = LoadTokenFromFile()
    
    IF token == NULL OR forceRefresh:
        RETURN Authenticate()  // Device flow
    
    IF DateTime.Now + 5 minutes >= token.expiresAt:
        refreshedToken = RefreshToken(token.refreshToken)
        IF refreshedToken != NULL:
            RETURN refreshedToken
        ELSE:
            RETURN Authenticate()  // Re-auth required
    
    RETURN token
```

### 10.2 Validation Rules

#### 10.2.1 Configuration Validation
1. **Provider Existence:** Selected provider must exist in configuration
2. **Required Fields:** API keys, endpoints must be non-empty (except Ollama)
3. **Working Directory:** Must exist and be accessible
4. **Model Names:** Must be valid strings

#### 10.2.2 Tool Input Validation
**ListFiles:**
- `directory` (optional): Must be within working directory
- `recursive` (optional): Must be boolean

**ReadFile:**
- `file_path` (required): Must exist and be within working directory

**WriteFile:**
- `file_path` (required): Must be within working directory
- `content` (required): Any string
- `create_directories` (optional): Must be boolean

**SearchCode:**
- `query` (required): Non-empty string
- `directory` (optional): Must be within working directory
- `pattern` (optional): Valid glob pattern
- `recursive` (optional): Must be boolean
- `regex` (optional): Must be boolean
- `case_sensitive` (optional): Must be boolean

**RunCommand:**
- `command` (required): Non-empty string
- `arguments` (optional): Any string
- `timeout_seconds` (optional): Positive integer (default 120)

#### 10.2.3 OAuth Validation
1. **Client ID:** Must be valid GitHub OAuth app client ID
2. **Device Code:** Must be obtained from GitHub
3. **Token Expiration:** Checked before each API call
4. **Refresh Token:** Must be valid for refresh operation

### 10.3 Important Algorithms

#### 10.3.1 Configuration Merging
```
FUNCTION LoadConfiguration():
    // 1. Load base configuration
    baseConfig = ParseJson("appsettings.json")
    
    // 2. Merge local overrides if exists
    IF FileExists("appsettings.local.json"):
        localConfig = ParseJson("appsettings.local.json")
        baseConfig = MergeRecursive(baseConfig, localConfig)
    
    // 3. Apply environment variable overrides
    FOR each key IN baseConfig.Providers[provider]:
        envValue = GetEnvironmentVariable(key)
        IF envValue != NULL:
            baseConfig.Providers[provider][key] = envValue
    
    // 4. Apply command-line overrides
    FOR each arg IN commandLineArgs:
        IF arg.StartsWith("--provider="):
            baseConfig.Agent.Provider = arg.Substring(11)
        IF arg.StartsWith("--task="):
            // Parse comma-separated tasks
            tasks = arg.Substring(7).Split(',')
            baseConfig.Agent.Tasks.AddRange(tasks)
    
    RETURN baseConfig
```

#### 10.3.2 Provider Response Normalization
Different LLM providers return different response formats. Each provider implementation normalizes to:

```csharp
LlmResponse {
    StopReason: "end_turn" | "tool_use" | "error",
    Content: [
        ContentBlock {
            Type: "text" | "tool_use",
            Text: "...",           // if type="text"
            Id: "...",             // if type="tool_use"
            Name: "...",           // if type="tool_use"
            Input: {...}           // if type="tool_use"
        }
    ]
}
```

#### 10.3.3 Tool Schema Transformation
Tools define JSON Schema once, but each provider needs different format:

**OpenAI/Azure/Ollama/GitHub:**
```json
{
  "type": "function",
  "function": {
    "name": "...",
    "description": "...",
    "parameters": { /* schema */ }
  }
}
```

**Claude:**
```json
{
  "name": "...",
  "description": "...",
  "input_schema": { /* schema */ }
}
```

**Gemini:**
```json
{
  "functionDeclarations": [{
    "name": "...",
    "description": "...",
    "parameters": { /* schema */ }
  }]
}
```

---

## 11. SECURITY MEASURES, ERROR HANDLING, AND EDGE CASES

### 11.1 Security Measures

#### 11.1.1 File System Security
**Sandboxing:**
- All file operations validated with `PathHelper.IsPathSafe()`
- Prevents directory traversal attacks (e.g., `../../etc/passwd`)
- Restricts access to working directory and subdirectories only

**Implementation:**
```csharp
public static bool IsPathSafe(string path, string workingDirectory)
{
    var fullPath = Path.GetFullPath(path);
    var workingPath = Path.GetFullPath(workingDirectory);
    return fullPath.StartsWith(workingPath);
}
```

**Applied to:**
- `ListFiles` - Directory enumeration
- `ReadFile` - File reading
- `WriteFile` - File creation/modification
- `SearchCode` - Code searching

#### 11.1.2 Command Execution Security
**Timeout Protection:**
- Default 120-second timeout prevents infinite hangs
- Configurable per-command via `timeout_seconds` parameter
- Process killed if timeout exceeded

**Process Isolation:**
- Commands run in working directory only
- Output captured via redirected streams
- No shell execute to prevent injection

**Limitations:**
- No input validation on command names (relies on OS security)
- No command whitelist/blacklist

#### 11.1.3 OAuth Token Security
**Storage:**
- Tokens stored in user profile directory (`~/.dracode/`)
- Plain text JSON (not encrypted in current implementation)
- File permissions rely on OS user isolation

**Transmission:**
- HTTPS only for OAuth and API calls
- Bearer token in Authorization header
- No token logging in verbose mode

**Token Lifecycle:**
- Automatic expiration checking
- Refresh before 5-minute expiration window
- Re-authentication on refresh failure

**Security Notes:**
- ⚠️ Tokens stored in plain text (enhancement opportunity)
- ✅ Never committed to source control (gitignored)
- ✅ Short-lived tokens (8 hours typical)

#### 11.1.4 API Key Security
**Configuration:**
- Environment variable override recommended
- Placeholder values (e.g., `${OPENAI_API_KEY}`) in committed configs
- `appsettings.local.json` gitignored

**Runtime:**
- Keys loaded at startup only
- Not logged in verbose mode
- Transmitted via HTTPS only

#### 11.1.5 Input Validation
**Tool Parameters:**
- Type checking on all tool inputs
- Required parameter validation
- Safe default values for optional parameters

**Configuration:**
- JSON parsing with error handling
- Empty/null checks on critical fields
- Fallback to defaults on invalid values

### 11.2 Error Handling

#### 11.2.1 Configuration Errors
```
ERROR: File not found
HANDLING: Fall back to default configuration
LOCATION: Program.cs:46

ERROR: Invalid JSON
HANDLING: Throw exception, terminate program
LOCATION: JsonDocument.Parse()

ERROR: Provider not found
HANDLING: Throw ArgumentException
LOCATION: AgentFactory.Create()
```

#### 11.2.2 Network Errors
```
ERROR: HTTP request failure
HANDLING: Exception bubbles to caller, iteration stops
LOCATION: HttpClient.PostAsync()

ERROR: Timeout
HANDLING: HttpClient throws TimeoutException
LOCATION: HttpClient (Ollama: 5-minute timeout)

ERROR: 401 Unauthorized (GitHub Copilot)
HANDLING: Attempt token refresh, retry once
LOCATION: GitHubCopilotProvider.SendMessageAsync()
```

#### 11.2.3 File System Errors
```
ERROR: File not found (read)
HANDLING: Return error message to LLM
LOCATION: ReadFile.Execute()

ERROR: Directory not found
HANDLING: Return error message to LLM
LOCATION: ListFiles.Execute(), SearchCode.Execute()

ERROR: Access denied
HANDLING: Return error message to LLM
LOCATION: All file tools (via PathHelper)

ERROR: Disk full (write)
HANDLING: Return error message to LLM
LOCATION: WriteFile.Execute()
```

#### 11.2.4 Tool Execution Errors
```
ERROR: Tool not found
HANDLING: Return error message to LLM
LOCATION: Agent.RunAsync() line 88-90

ERROR: Invalid tool parameters
HANDLING: Exception caught, return error message to LLM
LOCATION: Tool.Execute() catch blocks

ERROR: Command timeout
HANDLING: Kill process, return timeout error to LLM
LOCATION: RunCommand.Execute()
```

#### 11.2.5 LLM Provider Errors

**Since v2.5.1, all providers use retry logic with proper error propagation:**

```
ERROR: Network failures (timeouts, connection errors)
HANDLING: Retry with exponential backoff (default: 3 retries)
          After exhaustion, error properly propagates to task status
          Tasks marked as "Failed" with error message
LOCATION: LlmProviderBase.SendWithRetryAsync()

ERROR: API rate limit (429)
HANDLING: Retry with exponential backoff, respects Retry-After header
          Falls back to configured backoff if no header
LOCATION: LlmProviderBase.SendWithRetryAsync()

ERROR: Server errors (5xx)
HANDLING: Retry with exponential backoff (retryable status codes)
LOCATION: LlmProviderBase.SendWithRetryAsync()

ERROR: Invalid API key (401, 403)
HANDLING: Immediate failure, no retry (non-retryable status code)
LOCATION: Provider.SendMessageAsync()

ERROR: Invalid response format
HANDLING: JsonException during parsing, returns error response
LOCATION: Provider.ParseResponse()

ERROR: Provider not configured
HANDLING: Return NotConfigured stop reason with error message
          Properly detected and marks task as Failed (v2.5.1+)
LOCATION: Provider.IsConfigured()
```

**Error Propagation (v2.5.1+):**
- When `StopReason = "error"` or `"NotConfigured"`, error text is injected into message content
- `Kobold.HasErrorInMessages()` detects these patterns and marks tasks as Failed
- Prior to v2.5.1, empty content errors were missed and tasks incorrectly marked as Done

### 11.3 Edge Cases

#### 11.3.1 Configuration Edge Cases
**Case:** `appsettings.local.json` exists but is empty
**Handling:** Treated as `{}`, no overrides applied

**Case:** Environment variable is empty string
**Handling:** Treated as not set, config value used

**Case:** Task prompt is a file path but file doesn't exist
**Handling:** Used as literal text prompt

**Case:** Working directory doesn't exist
**Handling:** Likely fails during tool execution, not validated at startup

**Case:** Multiple providers configured but no provider specified
**Handling:** Uses `Agent.Provider` from config

#### 11.3.2 Tool Execution Edge Cases
**Case:** Empty file to read
**Handling:** Returns empty string to LLM

**Case:** Writing empty string to file
**Handling:** Creates empty file

**Case:** Searching empty directory
**Handling:** Returns empty string (no results)

**Case:** Command outputs nothing
**Handling:** Returns empty string

**Case:** Command exits with non-zero code
**Handling:** Output still returned (stderr included)

**Case:** Binary file in search
**Handling:** Skipped via extension check (.png, .jpg, .pdf, .zip)

**Case:** Unreadable file during search
**Handling:** Silently skipped (catch block)

#### 11.3.3 Conversation Edge Cases
**Case:** Max iterations reached
**Handling:** Loop breaks, conversation returned as-is

**Case:** LLM returns both text and tool_use
**Handling:** Tool use prioritized (stop_reason="tool_use")

**Case:** Tool result too large
**Handling:** Preview shown in verbose mode (200 chars), full result sent to LLM

**Case:** Multiple tool calls in one response
**Handling:** All executed, all results sent back together

**Case:** Conversation exceeds context window
**Handling:** LLM provider error, likely truncation by provider

#### 11.3.4 OAuth Edge Cases
**Case:** User doesn't authorize within 10 minutes
**Handling:** Timeout returned, authentication fails

**Case:** Token file corrupted
**Handling:** Deserialization fails, returns null, triggers re-auth

**Case:** Refresh token expired
**Handling:** Refresh fails, triggers full device flow re-auth

**Case:** Network disconnected during OAuth
**Handling: HTTP exceptions, authentication fails

**Case:** GitHub app deleted
**Handling:** Device code request fails, error returned

**Case:** Token file deleted while running
**Handling:** LoadTokenAsync() returns null, triggers re-auth

#### 11.3.5 Provider-Specific Edge Cases
**Ollama:**
- **Case:** Ollama service not running
- **Handling:** HTTP connection error, exception thrown

**Claude:**
- **Case:** Model doesn't support tool use
- **Handling:** Likely API error from Anthropic

**Gemini:**
- **Case:** Role mapping (assistant → model)
- **Handling:** Handled in BuildRequestPayload()

**Azure OpenAI:**
- **Case:** Deployment doesn't exist
- **Handling:** Azure API error

**GitHub Copilot:**
- **Case:** No active Copilot subscription
- **Handling:** OAuth succeeds, but API calls return 403

---

## 12. PERFORMANCE CONSIDERATIONS

### 12.1 Bottlenecks
1. **LLM API Latency:** 1-30 seconds per iteration
2. **Network I/O:** HTTPS requests to remote APIs
3. **File Search:** Large directories with recursive search
4. **Command Execution:** Long-running build/test commands

### 12.2 Optimizations
- **Async/Await:** All I/O operations are asynchronous
- **Lazy Evaluation:** Tools only executed when LLM calls them
- **No Caching:** Each request is independent (stateless)

### 12.3 Resource Limits
- **Max Iterations:** 10 (prevents infinite loops)
- **Command Timeout:** 120 seconds default (configurable)
- **HTTP Timeout:** Default (100 seconds), Ollama 5 minutes
- **Memory:** Unbounded (conversation history grows)

---

## 13. TESTING STRATEGY

### 13.1 Current State
- **Unit Tests:** None implemented
- **Integration Tests:** None implemented
- **Manual Testing:** Via CLI invocation

### 13.2 Recommended Testing Approach
1. **Unit Tests:** Tool execution with mock file system
2. **Integration Tests:** End-to-end with mock LLM responses
3. **Provider Tests:** Real API calls to verify compatibility
4. **OAuth Tests:** Mock GitHub OAuth endpoints

---

## 14. DEPLOYMENT AND DISTRIBUTION

### 14.1 Build Process
```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Publish self-contained
dotnet publish -c Release -r win-x64 --self-contained
```

### 14.2 Distribution
- **Console Application:** Standalone executable
- **Dependencies:** .NET 10.0 runtime required (or self-contained)
- **Configuration:** appsettings.json bundled, appsettings.local.json user-created

### 14.3 Installation
1. Clone repository
2. Install .NET 10.0 SDK
3. Copy `appsettings.json` to `appsettings.local.json`
4. Configure API keys in `appsettings.local.json`
5. Run: `dotnet run --provider=<name> --task="<task>"`

---

## 15. FUTURE ENHANCEMENTS

### 15.1 Identified Gaps
- No encrypted token storage
- No command whitelist/blacklist
- No conversation persistence
- No tool call history
- No rate limiting/retry logic
- No cost tracking
- No telemetry/logging framework
- No multi-workspace support

### 15.2 Potential Features
1. **Encrypted Token Storage** (Windows DPAPI, macOS Keychain)
2. **Conversation Export** (JSON, Markdown)
3. **Custom Tool Plugins** (via reflection)
4. **Rate Limiting** (per-provider quotas)
5. **Cost Tracking** (token usage monitoring)
6. **Telemetry** (Application Insights, Serilog)
7. **Web UI** (Blazor or React frontend)
8. **Multi-Agent Collaboration** (agent-to-agent communication)

---

## 16. GLOSSARY

| Term | Definition |
|------|------------|
| **Agent** | Autonomous AI system that executes tasks via LLM and tools |
| **LLM** | Large Language Model (e.g., GPT-4, Claude) |
| **Tool** | Executable capability exposed to LLM (e.g., read_file) |
| **Provider** | LLM service implementation (e.g., OpenAI, Claude) |
| **Working Directory** | Sandboxed filesystem location for agent operations |
| **Content Block** | Unit of content in LLM response (text or tool_use) |
| **Stop Reason** | Reason LLM ended generation (end_turn, tool_use, etc.) |
| **Device Flow** | OAuth flow for devices without web browsers |
| **Iteration** | One round of LLM request → tool execution → response |
| **System Prompt** | Instructions that guide LLM behavior |
| **Tool Use** | LLM decision to invoke a tool with parameters |

---

## 17. REVISION HISTORY

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0 | 2026-01-20 | Initial comprehensive specification | System |

---

## 18. APPENDICES

### Appendix A: Sample Configurations

#### A.1 Minimal OpenAI Configuration
```json
{
  "Agent": {
    "Provider": "openai",
    "WorkingDirectory": "./workspace",
    "Verbose": true,
    "Tasks": [],
    "Providers": {
      "openai": {
        "apiKey": "sk-...",
        "model": "gpt-4o"
      }
    }
  }
}
```

#### A.2 Multi-Provider Configuration
```json
{
  "Agent": {
    "Provider": "claude",
    "WorkingDirectory": "./workspace",
    "Verbose": true,
    "Tasks": [],
    "Providers": {
      "openai": {
        "apiKey": "${OPENAI_API_KEY}",
        "model": "gpt-4o"
      },
      "claude": {
        "apiKey": "${ANTHROPIC_API_KEY}",
        "model": "claude-3-5-sonnet-latest"
      },
      "ollama": {
        "model": "llama3.2"
      }
    }
  }
}
```

### Appendix B: Tool JSON Schemas

#### B.1 list_files Schema
```json
{
  "type": "object",
  "properties": {
    "directory": {
      "type": "string",
      "description": "Optional path relative to workspace root"
    },
    "recursive": {
      "type": "boolean",
      "description": "List files recursively",
      "default": false
    }
  }
}
```

#### B.2 search_code Schema
```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string",
      "description": "Text or regex to search for"
    },
    "directory": {
      "type": "string",
      "description": "Optional subdirectory"
    },
    "pattern": {
      "type": "string",
      "description": "File glob pattern",
      "default": "*"
    },
    "recursive": {
      "type": "boolean",
      "default": true
    },
    "regex": {
      "type": "boolean",
      "default": false
    },
    "case_sensitive": {
      "type": "boolean",
      "default": false
    }
  },
  "required": ["query"]
}
```

---

**END OF TECHNICAL SPECIFICATION**
