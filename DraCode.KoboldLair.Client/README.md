# DraCode.KoboldLair.Client

**Modern, minimalistic SPA dashboard** for the KoboldLair autonomous multi-agent coding system.

## Features

- **Modern SPA Architecture** - TypeScript Shadow DOM views on Birko.Web.Shell, bundled with esbuild
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
    "ServerUrl": "ws://localhost:57087"
  }
}
```

### Server URL Configuration

Set the `ServerUrl` to point to your KoboldLair.Server WebSocket endpoint. The server binds
`http://localhost:57087` / `https://localhost:57085` (`DraCode.KoboldLair.Server/Properties/launchSettings.json`),
so WebSocket connections use `ws://localhost:57087` (or `wss://localhost:57085`):

- **Local development**: `ws://localhost:57087`
- **Remote server**: `ws://192.168.1.100:5000`
- **Production with TLS**: `wss://your-domain.com`
- **Custom port**: `ws://localhost:8080`

The URL should use `ws://` for unencrypted or `wss://` for encrypted connections.

### Authentication

The client host holds no token. `GET /api/config` returns only `serverUrl` and the endpoint paths, so nobody who can
reach the host can read a credential from it. When the server has JWT auth on, the browser needs its own token: until
sign-in lands (TASK-114 / TASK-056), set it in the web client's settings. The `/dragon` and `/wyvern` proxies forward
the caller's own `?token=`, URL-encoded, and never attach one of their own.

The proxies validate the server's TLS certificate. For a local server with a self-signed certificate, set
`KoboldLair:AllowInvalidServerCertificate` to `true`; it is honoured only in the Development environment and logs a
warning at startup.

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
- **Retry Analysis** - 🔄 Retry button for failed projects to trigger Wyvern reanalysis
- **Error Display** - Shows error messages for failed projects
- Requirements gathering and specification generation
- Project creation and management

### Hierarchy (`/hierarchy.html`)
- Visual agent hierarchy
- Dragon → Wyverns → Drakes → Kobolds
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
    "ServerUrl": "ws://192.168.1.100:5000"
  }
}
```

For production with TLS/SSL:

```json
{
  "KoboldLair": {
    "ServerUrl": "wss://your-server.com"
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
- Verify the token set in the web client's settings is valid for this server
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
           /wyvern, /dragon            /wyvern, /dragon
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

The UI is TypeScript in `src/`, bundled by `build.js` (esbuild) into `wwwroot/dist/app.js`. `wwwroot/index.html` loads
the bundle and the Birko.Web stylesheets and mounts the shell (`<kobold-lair-shell>`, `src/app-shell.ts`); views render
into its `#route-outlet`. The old vanilla-JS pages are kept under `wwwroot/archive/`.

### File Structure
```
src/
  ├── app-shell-init.ts       # Entry point: registers Birko components, boots the shell and router
  ├── app-shell.ts            # <kobold-lair-shell>, extends Birko.Web.Shell's BAppShell
  ├── module-store.ts         # Module manifests (ribbon tabs)
  ├── routes.ts               # Birko Router: module-manifest routes → views, auth guard
  ├── components/             # KoboldComponent base, server selector
  ├── services/               # Config, WebSocket API client, toasts
  └── views/                  # One Shadow DOM view per route
wwwroot/
  ├── index.html              # Mounts the shell
  ├── dist/app.js             # Built bundle (generated, gitignored)
  ├── css/birko/              # Birko.Web tokens, reset, themes (copied by build.js, gitignored)
  └── archive/                # Legacy vanilla-JS pages, not served by default
tests/ui-e2e/                 # Playwright smoke + shell specs (see its README)
```

### Making Changes

1. Edit files in `src/`
2. `npm run build` (or `npm run watch`), then refresh the browser; `dotnet build` also runs the TypeScript build
   unless `-p:SkipTypeScriptCompile=true`
3. For production, run `dotnet publish`

**Note**: Configuration changes in `appsettings.json` require restarting the client application.

## Related

- [DraCode.KoboldLair.Server](../DraCode.KoboldLair.Server/README.md) - WebSocket server
- [WebSocket Protocol Documentation](../docs/websocket-protocol.md) - Detailed protocol specification
