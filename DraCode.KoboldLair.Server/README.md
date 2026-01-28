# DraCode.KoboldLair.Server

WebSocket server for the KoboldLair autonomous multi-agent coding system with token-based IP authentication.

## Features

- **WebSocket Endpoints**:
  - `/ws` - Wyvern task delegation endpoint
  - `/dragon` - Dragon requirements gathering endpoint
- **Token-based Authentication** with IP binding support
- **REST API** for project management and provider configuration
- **Multi-provider AI support** (OpenAI, Claude, Gemini, Ollama, Azure OpenAI, GitHub Copilot)
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
        "CompatibleAgents": ["dragon", "wyrm", "drake", "kobold"],
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

**Provider Types:**
- `openai` - OpenAI GPT models
- `claude` - Anthropic Claude models  
- `gemini` - Google Gemini models
- `ollama` - Local Ollama server
- `llamacpp` - Local llama.cpp server
- `azureopenai` - Azure OpenAI Service
- `githubcopilot` - GitHub Copilot API

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
Clients must provide a valid token in the query string: `ws://server/ws?token=your-secret-token-here`

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
ws://server:port/ws?token=your-token-here
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

## Related

- [DraCode.KoboldLair.Client](../DraCode.KoboldLair.Client/README.md) - Web UI client
