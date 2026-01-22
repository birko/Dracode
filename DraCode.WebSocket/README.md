# DraCode WebSocket Server

A WebSocket API server for communication with DraCode agents. Each WebSocket connection is handled by a dedicated agent instance.

## Running the Server

```bash
dotnet run --project DraCode.WebSocket
```

The server will start on `http://localhost:5000` (or `https://localhost:5001` for HTTPS).

## WebSocket Endpoint

Connect to: `ws://localhost:5000/ws`

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
const ws = new WebSocket('ws://localhost:5000/ws');

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
