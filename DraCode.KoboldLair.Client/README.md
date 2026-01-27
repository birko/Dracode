# DraCode.KoboldLair.Client

Web UI client for the KoboldLair autonomous multi-agent coding system.

## Features

- **Real-time Status Monitoring** - Track agents, projects, and tasks
- **Dragon Interface** - Interactive requirements gathering chat
- **Hierarchy Visualization** - View the agent hierarchy
- **Provider Configuration** - Manage AI provider settings
- **Project Management** - Configure project-specific settings

## Configuration

Edit `wwwroot/js/config.js` to configure the client:

```javascript
const CONFIG = {
    // Server WebSocket URL
    serverUrl: 'ws://localhost:5000',
    
    // Authentication token (if required by server)
    authToken: '',
    
    // WebSocket endpoints
    endpoints: {
        wyvern: '/ws',
        dragon: '/dragon'
    }
};
```

### Server URL

The `serverUrl` should point to your KoboldLair.Server instance:

- **Local development**: `ws://localhost:5000`
- **Production with HTTPS**: `wss://your-domain.com`
- **Custom port**: `ws://localhost:8080`

### Authentication

If the server has authentication enabled:

1. Get a valid token from your server administrator
2. Set it in config.js:
```javascript
authToken: 'your-token-here'
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
- Interactive chat interface
- Requirements gathering
- Specification generation
- Project creation

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

To connect to a KoboldLair.Server running on another machine:

1. Edit `wwwroot/js/config.js`:
```javascript
const CONFIG = {
    serverUrl: 'ws://192.168.1.100:5000',  // Server IP and port
    authToken: 'your-server-token',        // If authentication is enabled
    // ...
};
```

2. Ensure the server's CORS policy allows your client origin (already configured by default)

3. For production, use WSS (WebSocket Secure):
```javascript
serverUrl: 'wss://your-server.com'
```

## Troubleshooting

### Connection Issues

**Problem**: WebSocket connection fails
- Check `config.js` has correct `serverUrl`
- Verify server is running
- Check firewall/network allows WebSocket connections
- Look for CORS errors in browser console

**Problem**: "Unauthorized" error
- Verify `authToken` in config.js matches server configuration
- Check if your IP is in allowed list (if IP binding enabled)
- Ensure token is not expired

### API Errors

If API endpoints (hierarchy, stats) fail:
- Ensure `serverUrl` protocol conversion works (ws → http, wss → https)
- Check server API is accessible
- Verify CORS headers

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

## API Proxy Architecture

The KoboldLair Client acts as a proxy for API requests to the KoboldLair Server. This design eliminates CORS issues and simplifies the frontend architecture.

### How It Works

```
┌─────────┐          ┌─────────────────┐          ┌─────────────────┐
│ Browser │ ────────►│ KoboldLair      │ ────────►│ KoboldLair      │
│         │   HTTP   │ Client          │   HTTP   │ Server          │
│         │          │ (Proxy)         │          │ (API Endpoints) │
└─────────┘          └─────────────────┘          └─────────────────┘
          /api/projects                 /api/projects
          /api/providers                /api/providers
```

### Client-Side JavaScript

The JavaScript code makes simple relative URL requests:

```javascript
// dragon.js
async loadProjects() {
    const response = await fetch('/api/projects');
    this.projects = await response.json();
}

async loadProviders() {
    const response = await fetch('/api/providers');
    const data = await response.json();
    this.providers = data.providers || [];
}
```

### Proxy Implementation (Program.cs)

The Client application proxies these requests to the Server:

```csharp
// Configure HttpClient with Aspire service discovery
builder.Services.AddServiceDiscovery();
builder.Services.AddHttpClient("KoboldLairServer", client =>
{
    client.BaseAddress = new Uri("http://dracode-koboldlair-server");
})
.AddServiceDiscovery();

// Proxy GET requests
app.MapGet("/api/{**path}", async (string path, IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("KoboldLairServer");
    var response = await client.GetAsync($"/api/{path}");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, response.Content.Headers.ContentType?.ToString());
});

// Proxy POST requests
app.MapPost("/api/{**path}", async (string path, HttpContext context, ...) =>
{
    var client = httpClientFactory.CreateClient("KoboldLairServer");
    // Forward request body and headers
    var response = await client.PostAsync($"/api/{path}", requestContent);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, response.Content.Headers.ContentType?.ToString());
});
```

### Benefits

1. **No CORS Issues**: Browser only communicates with one origin (Client)
2. **Simplified Configuration**: No need to configure server URLs in JavaScript
3. **Service Discovery**: Aspire automatically resolves server endpoints
4. **Security**: Server URL not exposed to browser
5. **Flexibility**: Easy to add caching, rate limiting, or authentication at proxy level

### Supported Endpoints

All `/api/*` endpoints on the Server are automatically proxied:

- `GET /api/projects` - List all projects
- `GET /api/providers` - List LLM providers
- `GET /api/hierarchy` - Get project hierarchy
- `GET /api/stats` - Get system statistics
- `GET /api/projects/{id}/providers` - Get project-specific providers
- `POST /api/providers/configure` - Configure agent providers
- `POST /api/projects/{id}/agents/{type}/toggle` - Toggle agent for project

### Troubleshooting API Proxy

**Error: "Failed to load projects/providers"**

Check:
1. Is the KoboldLair Server running?
2. Is the KoboldLair Client running?
3. Check browser console for specific error messages
4. Check Client logs for proxy errors
5. Verify Aspire service discovery is working

**Error: "Service endpoint resolver cannot resolve 'http://dracode-koboldlair-server'"**

Solution:
1. Ensure `builder.AddServiceDefaults()` is called in Client
2. Ensure `builder.Services.AddServiceDiscovery()` is called
3. Ensure Client has `WithReference(koboldlairServer)` in AppHost

## Related

- [DraCode.KoboldLair.Server](../DraCode.KoboldLair.Server/README.md) - WebSocket server
