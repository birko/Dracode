# DraCode.KoboldLair.Server

WebSocket server for the KoboldLair autonomous multi-agent coding system with token-based IP authentication.

## Features

- **WebSocket Endpoints**:
  - `/wyvern` - Wyvern project analysis endpoint
  - `/dragon` - Dragon requirements gathering endpoint
- **Token-based Authentication** with IP binding support
- **REST API** for project management and provider configuration
- **Multi-provider AI support** (OpenAI, Claude, Gemini, Ollama, Azure OpenAI, GitHub Copilot, Z.AI, vLLM, SGLang, LlamaCpp)
- **Per-project resource limiting** to control parallel kobold execution

## Configuration

### Provider Configuration

**IMPORTANT:** .NET configuration merges arrays by index position, not by replacement. This means:

- `appsettings.json` - Base provider configuration
- `appsettings.Development.json` - Development-specific providers that **MERGE** with base config

**To use Development providers exclusively:**

1. **Recommended:** Keep only your active providers in `appsettings.Development.json` and ensure it has the complete list you want to use
2. **Alternative:** Remove or disable unwanted providers from `appsettings.json` base file

**Configuration Structure:**

```json
{
  "KoboldLairProviders": {
    "Providers": [
      {
        "Name": "providerkey",
        "DisplayName": "Display Name",
        "Type": "openai|claude|gemini|ollama|llamacpp|githubcopilot",
        "DefaultModel": "model-name",
        "CompatibleAgents": ["dragon", "wyvern", "drake", "kobold"],
        "IsEnabled": true,
        "RequiresApiKey": true|false,
        "Description": "Description text",
        "Configuration": {
          "apiKey": "your-api-key",  // If RequiresApiKey is true
          "baseUrl": "https://api.example.com",
          // ... other provider-specific config
        }
      }
    ],
    "AgentProviders": {
      "DragonProvider": "providerkey",
      "WyvernProvider": "providerkey",
      "KoboldProvider": "providerkey"
    }
  }
}
```

### Per-Agent-Type Kobold Providers (user-settings.json)

Configure different LLM providers for different Kobold agent types:

```json
{
  "koboldProvider": "openai",
  "koboldAgentTypeSettings": [
    { "agentType": "csharp", "provider": "claude", "model": "claude-sonnet-4-20250514" },
    { "agentType": "python", "provider": "openai", "model": "gpt-4o" },
    { "agentType": "react", "provider": "gemini", "model": null }
  ]
}
```

**Provider Types:**
- `openai` - OpenAI GPT models
- `claude` - Anthropic Claude models
- `gemini` - Google Gemini models
- `ollama` - Local Ollama server
- `llamacpp` - Local llama.cpp server
- `azureopenai` - Azure OpenAI Service
- `githubcopilot` - GitHub Copilot API
- `zai` - Z.AI (Zhipu) GLM models
- `vllm` - vLLM local inference server
- `sglang` - SGLang inference server

**Checking Configuration:**

When the server starts, it logs the active environment:
```
[KoboldLair.Server] Environment: Development
[KoboldLair.Server] ASPNETCORE_ENVIRONMENT: Development
```

The `/api/providers` endpoint shows which providers are loaded and configured.

### Resource Limits (Per-Project Kobold Limits)

Control how many kobolds can run in parallel per project to manage resource consumption and prevent any single project from monopolizing system resources.

**Why Resource Limiting?**
- Controlled resource consumption per project
- Fair resource allocation across multiple projects
- Predictable API usage and costs
- Graceful handling of resource constraints

**Configuration File:** `project-configs.json`

```json
{
  "defaultMaxParallelKobolds": 1,
  "projects": [
    {
      "projectId": "my-project",
      "projectName": "My Project",
      "maxParallelKobolds": 2
    },
    {
      "projectId": "high-priority",
      "projectName": "High Priority Project",
      "maxParallelKobolds": 4
    }
  ]
}
```

**Configuration Options:**
- `defaultMaxParallelKobolds` - Default limit for projects without specific configuration (default: 1)
- `projectId` - Unique identifier for the project (matches task's ProjectId)
- `projectName` - Optional display name for the project
- `maxParallelKobolds` - Maximum kobolds that can run concurrently for this project

**How It Works:**
1. When Drake receives a task to execute, it checks the project's current active kobolds
2. If under limit: Kobold is summoned and task executes
3. If at limit: Kobold creation skipped, task remains in queue
4. Drake's monitoring service (runs every 60s) retries task assignment

**Examples:**

```json
// Conservative setup - all projects limited to 1 kobold
{
  "defaultMaxParallelKobolds": 1,
  "projects": []
}

// Priority-based allocation
{
  "defaultMaxParallelKobolds": 1,
  "projects": [
    {
      "projectId": "production-app",
      "maxParallelKobolds": 5
    },
    {
      "projectId": "dev-project",
      "maxParallelKobolds": 2
    }
  ]
}
```

**Monitoring:**
- Check `/api/stats` endpoint for active kobold count
- Review logs for "at parallel limit" messages
- Watch for task queue buildup

**Best Practices:**
1. Start conservative (1-2) and increase based on resources
2. Monitor CPU/memory consumption and API rate limits
3. Adjust limits based on project priority and complexity
4. Regularly review and tune as system grows

**Specify Config Path in appsettings.json:**
```json
{
  "ProjectConfigurationsPath": "project-configs.json"
}
```

## Authentication

The server supports token-based authentication with optional IP address binding for enhanced security.

### Configuration

Authentication is configured in `appsettings.json`:

```json
{
  "Authentication": {
    "Enabled": false,
    "Tokens": [],
    "TokenBindings": []
  }
}
```

### Authentication Modes

#### 1. Disabled (Default)
```json
{
  "Authentication": {
    "Enabled": false
  }
}
```
All connections are allowed without authentication.

#### 2. Simple Token Authentication
```json
{
  "Authentication": {
    "Enabled": true,
    "Tokens": [
      "your-secret-token-here",
      "${MY_TOKEN_ENV_VAR}"
    ]
  }
}
```
Clients must provide a valid token in the query string: `ws://server/wyvern?token=your-secret-token-here`

#### 3. Token with IP Binding (Recommended for Production)
```json
{
  "Authentication": {
    "Enabled": true,
    "TokenBindings": [
      {
        "Token": "production-token-1",
        "AllowedIps": ["192.168.1.100", "10.0.0.50"]
      },
      {
        "Token": "${SECURE_TOKEN}",
        "AllowedIps": ["${CLIENT_IP_ADDRESS}"]
      }
    ]
  }
}
```
Tokens are bound to specific IP addresses. Only requests from allowed IPs with matching tokens are accepted.

### Environment Variables

Tokens and IP addresses can be loaded from environment variables using the `${VAR_NAME}` syntax:

```json
{
  "Token": "${KOBOLDLAIR_AUTH_TOKEN}",
  "AllowedIps": ["${TRUSTED_CLIENT_IP}"]
}
```

### Behind Proxy/Load Balancer

The server automatically detects client IP addresses from:
1. `X-Forwarded-For` header (takes the first IP for the original client)
2. `X-Real-IP` header (nginx)
3. Direct connection IP (fallback)

## Running the Server

### Development
```bash
dotnet run --project DraCode.KoboldLair.Server
```

### Production
```bash
# Set environment variables for authentication
export KOBOLDLAIR_AUTH_TOKEN="your-production-token"
export TRUSTED_CLIENT_IP="192.168.1.100"

# Run the server
dotnet run --project DraCode.KoboldLair.Server --configuration Release
```

## API Endpoints

- `GET /` - Health check
- `GET /api/hierarchy` - Get project hierarchy and statistics
- `GET /api/projects` - Get all projects
- `GET /api/stats` - Get system statistics
- `GET /api/providers` - Get provider configuration
- `POST /api/providers/configure` - Update provider settings
- `GET /api/projects/{id}/providers` - Get project-specific providers
- `POST /api/projects/{id}/providers` - Update project providers

## WebSocket Protocol

### Connection
```
ws://server:port/wyvern?token=your-token-here
ws://server:port/dragon?token=your-token-here
```

### Message Format
All messages are JSON:
```json
{
  "type": "message_type",
  "data": { ... }
}
```

## Security Best Practices

1. **Always enable authentication in production**
2. **Use environment variables for tokens** - never commit secrets to source control
3. **Use IP binding** when possible to restrict access to known clients
4. **Use HTTPS/WSS** in production (configure reverse proxy like nginx)
5. **Rotate tokens regularly**
6. **Monitor authentication logs** for suspicious activity

## Project Structure

```
DraCode.KoboldLair.Server/
├── Agents/                         # Agent Implementations
│   ├── AgentFactory.cs             # Creates Dragon, Wyrm, Drake agents
│   ├── DragonAgent.cs              # Interactive requirements gathering
│   ├── WyrmAgent.cs                # Project analyzer
│   ├── WyvernAgent.cs              # Task delegator
│   └── Tools/                      # Dragon-specific tools
│       ├── AddExistingProjectTool.cs    # Import existing projects from disk
│       ├── GitMergeTool.cs              # Merge feature branches to main
│       ├── GitStatusTool.cs             # View branch status and merge readiness
│       ├── ProjectApprovalTool.cs       # Approve specs (Prototype → New)
│       ├── SpecificationWriterTool.cs   # Write specification files
│       ├── SpecificationManagementTool.cs
│       ├── FeatureManagementTool.cs
│       └── ListProjectsTool.cs
├── Factories/                      # Factory Pattern - Resource Creation
│   ├── KoboldFactory.cs            # Creates Kobolds with parallel limits
│   ├── DrakeFactory.cs             # Creates Drake supervisors
│   ├── WyvernFactory.cs            # Creates Wyvern orchestrators
│   └── WyrmFactory.cs              # Creates Wyrm analyzers
├── Orchestrators/                  # High-Level Orchestration
│   ├── Drake.cs                    # Task supervisor
│   ├── WyrmRunner.cs               # Task running orchestrator
│   └── Wyvern.cs                   # Task delegation orchestrator
├── Models/                         # Data Models (organized by domain)
│   ├── Agents/                     # Agent-related models
│   │   ├── DragonMessage.cs
│   │   ├── DragonStatistics.cs
│   │   ├── DrakeStatistics.cs
│   │   ├── Kobold.cs
│   │   ├── KoboldStatistics.cs
│   │   └── WyvernAnalysis.cs
│   ├── Configuration/              # Configuration models
│   │   ├── ProjectConfig.cs
│   │   ├── ProviderConfig.cs
│   │   ├── KoboldLairConfiguration.cs
│   │   └── UserSettings.cs
│   ├── Projects/                   # Project models
│   │   ├── Project.cs
│   │   ├── ProjectInfo.cs
│   │   ├── ProjectStatistics.cs
│   │   ├── Specification.cs
│   │   └── WorkArea.cs
│   ├── Tasks/                      # Task models
│   │   ├── TaskRecord.cs
│   │   ├── TaskStatus.cs
│   │   ├── TaskTracker.cs
│   │   └── Feature.cs
│   └── WebSocket/                  # WebSocket models
│       ├── WebSocketCommand.cs
│       └── WebSocketRequest.cs
├── Services/                       # Business Logic Services
│   ├── DragonService.cs            # Dragon WebSocket service
│   ├── GitService.cs               # Git operations (branch, merge, commit)
│   ├── WyrmService.cs              # Wyrm analysis service
│   ├── WyvernProcessingService.cs  # Wyvern background processing (60s)
│   ├── DrakeMonitoringService.cs   # Drake background monitoring (60s)
│   ├── ProjectService.cs           # Project management
│   ├── ProjectConfigurationService.cs
│   ├── ProviderConfigurationService.cs
│   └── WebSocketCommandHandler.cs
├── Program.cs                      # ASP.NET Core startup & DI
├── appsettings.json                # Base configuration (simplified)
├── appsettings.local.example.json  # Example local configuration
└── project-configs.json            # Per-project resource limits
```

## Dragon Session Management

Dragon supports multi-session with persistent connections and automatic reconnection:

**Session Features:**
- **Multi-session**: Multiple concurrent sessions per WebSocket connection
- **Persistence**: Sessions survive disconnects for 10 minutes
- **History Replay**: Up to 100 messages stored and replayed on reconnect
- **Automatic Cleanup**: Expired sessions removed every 60 seconds

**Session Protocol:**
```json
// Resume existing session
{ "sessionId": "abc123", "message": "continue conversation" }

// Session resumed response
{ "type": "session_resumed", "sessionId": "abc123", "messageCount": 5, "lastMessageId": "xyz" }

// Historical messages replayed with isReplay flag
{ "type": "dragon_message", "isReplay": true, "messageId": "...", "message": "..." }

// Thinking indicator (real-time processing feedback)
{ "type": "dragon_thinking", "message": "Analyzing project structure..." }
```

**Message Types:**
- `dragon_message` - Regular chat responses
- `dragon_thinking` - Processing/thinking indicator (real-time feedback)
- `dragon_typing` - Typing indicator
- `session_resumed` - Session reconnection confirmation
- `specification_created` - Specification successfully created
- `dragon_reloaded` - Agent reloaded with new provider
- `error` - Error occurred (types: `llm_connection`, `llm_timeout`, `llm_response`, `llm_error`, `general`)

**Provider Reload:**
```json
// Reload agent with different provider (clears context)
{ "type": "reload", "provider": "claude" }

// Response
{ "type": "dragon_reloaded", "message": "Agent reloaded with provider: claude\n\n..." }
```

## Dragon Tools

Dragon uses specialized tools for project management:

| Tool | Description |
|------|-------------|
| `add_existing_project` | Scan and import existing projects from disk with auto-detected technologies (50+ tech stacks) |
| `approve_specification` | Approve a specification (Prototype → New status) to trigger Wyvern processing |
| `write_specification` | Create or update project specification files |
| `manage_specification` | Edit and manage specification content |
| `manage_features` | Add, update, or remove project features |
| `list_projects` | List all registered projects with status |
| `git_status` | View branch status, unmerged feature branches, and merge readiness |
| `git_merge` | Merge feature branches to main with conflict detection and safe workflow |

### Tool Details

**add_existing_project**
- Scans directory for project files (.sln, .slnx, package.json, requirements.txt, etc.)
- Auto-detects 50+ technologies (C#, TypeScript, Python, Go, Rust, etc.)
- Analyzes project structure and dependencies
- Generates initial specification from existing code
- Registers project in ProjectService

**approve_specification** (Two-Stage Workflow)
1. Dragon creates specification with **Prototype** status
2. User reviews and confirms requirements
3. Dragon calls `approve_specification` tool
4. Status changes to **New** → WyvernProcessingService picks up
5. Prevents accidental task generation from incomplete specs

**list_projects**
- Returns all registered projects with: ID, Name, Status, FeatureCount, CreatedAt, UpdatedAt
- Used by Dragon to show available projects during conversation

**git_status** (NEW)
- Shows current branch and commit information
- Lists unmerged feature branches with their status
- Checks merge readiness (clean working tree, no conflicts)
- Maps branches to features for tracking progress

**git_merge** (NEW)
- Merges feature branches to main branch
- Pre-checks for conflicts before merging
- Returns detailed merge results (success, conflicts, or errors)
- Supports safe merge workflow with rollback on failure

## Related

- [DraCode.KoboldLair.Client](../DraCode.KoboldLair.Client/README.md) - Web UI client
