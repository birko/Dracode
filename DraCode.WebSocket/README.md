# DraCode WebSocket Server

A WebSocket API server for communication with DraCode agents. Each WebSocket connection is handled by a dedicated agent instance.

## Running the Server

```bash
dotnet run --project DraCode.WebSocket
```

The server will start on `http://localhost:5000` (or `https://localhost:5001` for HTTPS).

## WebSocket Endpoint

Connect to: `ws://localhost:5000/ws`

**With authentication enabled:**
```
ws://localhost:5000/ws?token=YOUR_SECRET_TOKEN
```

## Authentication

The WebSocket server supports optional token-based authentication with IP address binding. Configure it in `appsettings.json`:

### Simple Token Authentication

```json
{
  "Authentication": {
    "Enabled": true,
    "Tokens": [
      "${WEBSOCKET_AUTH_TOKEN}",
      "your-secret-token-here"
    ]
  }
}
```

### Token with IP Binding (Recommended for Production)

Bind tokens to specific IP addresses to prevent token misuse. Even if a token is stolen, it cannot be used from unauthorized IP addresses:

```json
{
  "Authentication": {
    "Enabled": true,
    "Tokens": [
      "${WEBSOCKET_AUTH_TOKEN}"
    ],
    "TokenBindings": [
      {
        "Token": "${WEBSOCKET_RESTRICTED_TOKEN}",
        "AllowedIps": [
          "192.168.1.100",
          "10.0.0.50",
          "${ALLOWED_CLIENT_IP}"
        ]
      },
      {
        "Token": "office-token",
        "AllowedIps": [
          "203.0.113.45",
          "198.51.100.20"
        ]
      }
    ]
  }
}
```

### Configuration Options

- `Enabled`: Set to `true` to enable authentication (default: `false`)
- `Tokens`: Array of valid authentication tokens (no IP restriction)
  - Use environment variables: `"${ENV_VAR_NAME}"`
  - Or hardcode tokens: `"your-secret-token"`
- `TokenBindings`: Array of token-IP binding configurations
  - `Token`: The authentication token (supports environment variables)
  - `AllowedIps`: List of IP addresses allowed to use this token
    - Supports both IPv4 and IPv6
    - Use environment variables for dynamic IPs

### How It Works

1. Client connects with token: `ws://localhost:5000/ws?token=YOUR_TOKEN`
2. Server extracts client IP address (handles proxies via X-Forwarded-For, X-Real-IP headers)
3. Validates token from `TokenBindings` first (checks both token AND IP)
4. Falls back to `Tokens` if no binding matches (token-only validation)
5. Connection rejected with 401 if validation fails

### Connecting with Authentication

When authentication is enabled, clients must provide a token as a query parameter:

```javascript
const ws = new WebSocket('ws://localhost:5000/ws?token=your-secret-token-here');
```

Without a valid token (or IP mismatch), the connection will be rejected with HTTP 401 Unauthorized.

### Behind Proxies / Load Balancers

The server automatically detects client IP from these headers:
- `X-Forwarded-For` (takes first IP in chain)
- `X-Real-IP` (nginx)
- Falls back to direct connection IP

Ensure your proxy/load balancer is configured to set these headers correctly.

### Best Practices

1. **Use IP bindings in production** - Prevents token theft/misuse
2. **Store tokens in environment variables**, not hardcoded in appsettings.json
3. **Use strong, randomly generated tokens** (e.g., `openssl rand -base64 32`)
4. **Keep authentication disabled in development** if not needed
5. **Whitelist specific IPs** - Use the most restrictive IP list possible
6. **Monitor authentication logs** - Failed attempts are logged with IP addresses

## Web Client

For a ready-to-use web client, see the **DraCode.Web** project. The WebSocket server is API-only and does not serve any UI.

## Configuration

LLM providers are configured in `appsettings.json`. You can configure API keys and default settings for each provider:

```json
{
  "Agent": {
    "WorkingDirectory": "./",
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
        "model": "claude-3-5-sonnet-latest"
      }
    }
  }
}
```

Environment variables (format: `${VAR_NAME}`) are automatically expanded at runtime.

## Commands

### 1. List
Lists all available/configured LLM providers.

```json
{
  "command": "list"
}
```

**Response:**
```json
{
  "status": "success",
  "message": "Found 6 configured provider(s)",
  "data": "[
    {
      \"name\": \"openai\",
      \"type\": \"openai\",
      \"configured\": true,
      \"model\": \"gpt-4o\"
    },
    {
      \"name\": \"claude\",
      \"type\": \"claude\",
      \"configured\": false,
      \"model\": \"claude-3-5-sonnet-latest\"
    }
  ]"
}
```

The `configured` field indicates whether the provider has valid API credentials configured (either in appsettings.json or environment variables).

### 2. Connect
Creates a new agent instance for the connection.

**Using configured provider (from appsettings.json):**
```json
{
  "command": "connect",
  "config": {
    "provider": "openai"
  }
}
```

**Overriding with custom configuration:**
```json
{
  "command": "connect",
  "config": {
    "provider": "openai",
    "apiKey": "your-api-key",
    "model": "gpt-4o",
    "workingDirectory": "/path/to/workspace",
    "verbose": "false"
  }
}
```

Configuration priority: Request config > appsettings.json > defaults

**Response:**
```json
{
  "status": "connected",
  "message": "Agent initialized with provider: openai"
}
```

### 3. Send
Sends a task to the agent.

```json
{
  "command": "send",
  "data": "Create a new file called hello.txt with 'Hello World' content"
}
```

**Response:**
```json
{
  "status": "processing",
  "message": "Agent is processing your request..."
}
```

Followed by:
```json
{
  "status": "completed",
  "message": "Task completed",
  "data": "I've created the file hello.txt with the requested content."
}
```

### 4. Reset
Reinitializes the underlying agent for the connection.

```json
{
  "command": "reset",
  "config": {
    "provider": "claude"
  }
}
```

**Response:**
```json
{
  "status": "reset",
  "message": "Agent reinitialized successfully"
}
```

### 5. Disconnect
Closes the socket connection and disposes the associated agent.

```json
{
  "command": "disconnect"
}
```

**Response:**
```json
{
  "status": "disconnected",
  "message": "Agent disposed and connection closed"
}
```

## Supported Providers

- `openai` - OpenAI GPT models
- `azureopenai` - Azure OpenAI
- `claude` - Anthropic Claude
- `gemini` - Google Gemini
- `ollama` - Local Ollama models
- `githubcopilot` - GitHub Copilot

## Configuration Options

- `provider`: LLM provider name (required for connect/reset)
- `apiKey`: API key for the provider
- `model`: Model name/identifier
- `baseUrl`: Custom API endpoint URL
- `workingDirectory`: Working directory for the agent (defaults to current directory)
- `verbose`: Enable verbose logging ("true"/"false")
- `endpoint`: Azure OpenAI endpoint (for azureopenai provider)
- `deployment`: Azure OpenAI deployment name (for azureopenai provider)
- `clientId`: GitHub Copilot client ID (for githubcopilot provider)

## Web Client

A web client is available in the **DraCode.Web** project. To use it:

```bash
# Terminal 1: Start the WebSocket server
dotnet run --project DraCode.WebSocket

# Terminal 2: Start the web client
dotnet run --project DraCode.Web

# Open browser to http://localhost:5001
```

## Example Client (JavaScript)

```javascript
// Connect with authentication
const ws = new WebSocket('ws://localhost:5000/ws?token=your-secret-token-here');

ws.onopen = () => {
  console.log('Connected to WebSocket server');
  
  // List available providers
  ws.send(JSON.stringify({ command: 'list' }));
};

ws.onmessage = (event) => {
  const response = JSON.parse(event.data);
  console.log('Response:', response);
  
  if (response.status === 'success' && response.data) {
    // Parse provider list
    const providers = JSON.parse(response.data);
    console.log('Available providers:', providers);
    
    // Connect with first configured provider (or specify your own)
    ws.send(JSON.stringify({
      command: 'connect',
      config: {
        provider: 'openai'  // Uses config from appsettings.json
      }
    }));
  }
  
  if (response.status === 'connected') {
    // Send a task
    ws.send(JSON.stringify({
      command: 'send',
      data: 'List all files in the current directory'
    }));
  }
};

ws.onerror = (error) => {
  console.error('WebSocket error:', error);
};

ws.onclose = () => {
  console.log('Connection closed');
};
```

## Error Handling

All errors are returned with status `"error"`:

```json
{
  "status": "error",
  "error": "Error message description"
}
```

Common errors:
- Invalid message format
- Unknown command
- No agent found (use 'connect' first)
- Agent execution failed
- Missing required parameters
