# DraCode.KoboldLair.Client

**Modern, minimalistic SPA dashboard** for the KoboldLair autonomous multi-agent coding system.

## Features

- **Modern SPA Architecture** - Built with vanilla JavaScript ES6+ modules
- **Minimalistic Design** - Clean, dark-themed dashboard with intuitive navigation
- **Real-time Updates** - WebSocket integration for live data
- **Responsive Layout** - Flexbox-based design that works on all screen sizes
- **Multiple Views**:
  - **Dashboard**: Overview with statistics and recent projects
  - **Dragon**: Interactive chat interface for Dragon requirements agent
  - **Hierarchy**: Visual representation of the agent hierarchy
  - **Projects**: Detailed project listing and management
  - **Providers**: AI provider configuration and status

## Technology Stack

- **HTML5**: Semantic markup
- **CSS3**: Modern styling with CSS variables and Flexbox
- **Vanilla JavaScript**: ES6+ modules, no frameworks
- **WebSocket API**: Real-time bidirectional communication
- **Fetch API**: RESTful API integration

## Configuration

The client now uses **WebSocket-only communication**. Configure the server connection in `appsettings.json`:

```json
{
  "KoboldLair": {
    "ServerUrl": "ws://localhost:5000",
    "AuthToken": ""
  }
}
```

### Server URL Configuration

Set the `ServerUrl` to point to your KoboldLair.Server WebSocket endpoint:

- **Local development**: `ws://localhost:5000`
- **Remote server**: `ws://192.168.1.100:5000`
- **Production with TLS**: `wss://your-domain.com`
- **Custom port**: `ws://localhost:8080`

The URL should use `ws://` for unencrypted or `wss://` for encrypted connections.

### Authentication

If the server has authentication enabled:

1. Get a valid token from your server administrator
2. Set it in `appsettings.json`:
```json
{
  "KoboldLair": {
    "ServerUrl": "ws://your-server:5000",
    "AuthToken": "your-token-here"
  }
}
```

The token will be automatically appended to all WebSocket connections.

## Running the Client

### Development
```bash
dotnet run --project DraCode.KoboldLair.Client
```

The client will be available at `http://localhost:5001` (or the port shown in console).

### Production

Build and deploy as a static website:

```bash
dotnet publish DraCode.KoboldLair.Client -c Release -o ./publish
```

Deploy the contents of `publish/wwwroot` to any web server (nginx, Apache, IIS, etc.).

### With Aspire

Run via the AppHost:
```bash
dotnet run --project DraCode.AppHost
```

Start `dracode-koboldlair-client` from the Aspire Dashboard.

## Pages

### Status Monitor (`/`)
- Real-time agent status
- Task execution logs
- Connection status
- System statistics

### Dragon (`/dragon.html`)
- Modern, polished chat interface with AI assistant
- **Markdown support** - Rich text formatting in responses
- **Streaming messages** - Real-time typing effect for AI responses
- **Smooth animations** - Beautiful message transitions and effects
- **Mobile responsive** - Optimized for all screen sizes
- **Agent Reload** - 🔄 Reload agent button to clear context and reload provider settings
- Requirements gathering and specification generation
- Project creation and management

### Hierarchy (`/hierarchy.html`)
- Visual agent hierarchy
- Dragon → Wyrms → Drakes → Kobolds
- Status indicators
- Task counts

### Providers (`/providers.html`)
- Configure AI providers
- Set API keys
- Choose models
- Agent-provider mappings

### Project Config (`/project-config.html`)
- Project-specific settings
- Provider overrides
- Agent toggles

## Connecting to a Remote Server

To connect to a KoboldLair.Server running on another machine, edit `appsettings.json`:

```json
{
  "KoboldLair": {
    "ServerUrl": "ws://192.168.1.100:5000",
    "AuthToken": "your-server-token"
  }
}
```

For production with TLS/SSL:

```json
{
  "KoboldLair": {
    "ServerUrl": "wss://your-server.com",
    "AuthToken": "your-server-token"
  }
}
```

**Note**: The server must be accessible from your client machine on the specified port.

## Troubleshooting

### Connection Issues

**Problem**: WebSocket connection fails
- Check `appsettings.json` has correct `ServerUrl`
- Verify server is running and accessible
- Check firewall/network allows WebSocket connections on the specified port
- Look for connection errors in browser console

**Problem**: "Unauthorized" error
- Verify `AuthToken` in appsettings.json matches server configuration
- Check if your IP is in allowed list (if IP binding enabled)
- Ensure token is not expired

### Dragon Agent Issues

**Problem**: Dragon agent gives stale or incorrect responses
- Click the "🔄 Reload Agent" button in the chat header
- This clears the conversation context and reloads provider settings
- Useful when provider configuration has changed

## Communication Architecture

The client now uses **pure WebSocket communication** instead of HTTP API calls.

### How It Works

```
┌─────────┐          ┌─────────────────┐          ┌─────────────────┐
│ Browser │ ────────►│ KoboldLair      │ ────────►│ KoboldLair      │
│         │   WS     │ Client          │   WS     │ Server          │
│         │          │ (Proxy)         │          │ (Handlers)      │
└─────────┘          └─────────────────┘          └─────────────────┘
           /ws, /dragon                 /ws, /dragon
```

### WebSocket Message Protocol

**Request Format:**
```json
{
  "id": "req_1234567890_abc",
  "command": "get_projects",
  "data": { /* optional command data */ }
}
```

**Response Format:**
```json
{
  "id": "req_1234567890_abc",
  "type": "response",
  "data": { /* response data */ },
  "timestamp": "2026-01-28T09:30:00.000Z"
}
```

### Available Commands

All API operations are now WebSocket commands:
- `get_hierarchy` - Get project hierarchy
- `get_projects` - List all projects
- `get_stats` - System statistics
- `get_providers` - List LLM providers
- `configure_provider` - Configure agent providers
- `get_project_config` - Get project settings
- `update_project_config` - Update project settings
- `toggle_agent` - Enable/disable agent
- And more...

### Benefits

1. **Unified Protocol**: Single WebSocket connection for all operations
2. **Real-time**: Instant updates without polling
3. **Configurable**: Easy server URL configuration
4. **Simplified**: No HTTP proxy layer needed
5. **Efficient**: Persistent connection reduces overhead

## Development

The client is a pure HTML/CSS/JavaScript application with no build step required.

### File Structure
```
wwwroot/
  ├── index.html              # Main status page
  ├── dragon.html             # Requirements gathering
  ├── hierarchy.html          # Agent hierarchy
  ├── providers.html          # Provider config
  ├── project-config.html     # Project settings
  ├── css/
  │   ├── styles.css
  │   └── dragon.css
  └── js/
      ├── config.js           # Configuration (EDIT THIS)
      ├── main.js             # Main application
      ├── dragon.js           # Dragon client
      ├── hierarchy.js        # Hierarchy visualization
      ├── websocket.js        # WebSocket manager
      ├── taskManager.js      # Task management
      └── ui.js               # UI controller
```

### Making Changes

1. Edit files in `wwwroot/`
2. Refresh browser (no rebuild needed)
3. For production, run `dotnet publish`

**Note**: Configuration changes in `appsettings.json` require restarting the client application.

## Related

- [DraCode.KoboldLair.Server](../DraCode.KoboldLair.Server/README.md) - WebSocket server
- [WebSocket Protocol Documentation](../docs/websocket-protocol.md) - Detailed protocol specification
