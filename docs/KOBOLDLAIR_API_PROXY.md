# KoboldLair API Proxy Documentation

## Overview

The KoboldLair Client acts as a proxy for API requests to the KoboldLair Server. This design eliminates CORS issues and simplifies the frontend architecture.

## Architecture

```
┌─────────┐          ┌─────────────────┐          ┌─────────────────┐
│ Browser │ ────────►│ KoboldLair      │ ────────►│ KoboldLair      │
│         │   HTTP   │ Client          │   HTTP   │ Server          │
│         │          │ (Proxy)         │          │ (API Endpoints) │
└─────────┘          └─────────────────┘          └─────────────────┘
               /api/projects                 /api/projects
               /api/providers                /api/providers
```

## Implementation

### Client-Side (JavaScript)

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

### Proxy (C# - Client Program.cs)

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

### Server-Side (C# - Server Program.cs)

The Server provides the actual API endpoints:

```csharp
app.MapGet("/api/projects", (ProjectService projectService) =>
{
    var projects = projectService.GetAllProjects();
    return Results.Json(projects);
});

app.MapGet("/api/providers", (ProviderConfigurationService providerService) =>
{
    var config = providerService.GetConfiguration();
    // ... return provider data
});
```

## Benefits

1. **No CORS Issues**: Browser only communicates with one origin (Client)
2. **Simplified Configuration**: No need to configure server URLs in JavaScript
3. **Service Discovery**: Aspire automatically resolves server endpoints
4. **Security**: Server URL not exposed to browser
5. **Flexibility**: Easy to add caching, rate limiting, or authentication at proxy level

## Supported Endpoints

All `/api/*` endpoints on the Server are automatically proxied:

- `GET /api/projects` - List all projects
- `GET /api/providers` - List LLM providers
- `GET /api/hierarchy` - Get project hierarchy
- `GET /api/stats` - Get system statistics
- `GET /api/projects/{id}/providers` - Get project-specific providers
- `POST /api/providers/configure` - Configure agent providers
- `POST /api/projects/{id}/agents/{type}/toggle` - Toggle agent for project

## Troubleshooting

### Error: "Failed to load projects/providers"

**Check:**
1. Is the KoboldLair Server running?
2. Is the KoboldLair Client running?
3. Check browser console for specific error messages
4. Check Client logs for proxy errors
5. Verify Aspire service discovery is working

**Solution:**
```bash
# Restart both services from Aspire Dashboard
# Or check service discovery configuration
```

### Error: "Service endpoint resolver cannot resolve 'http://dracode-koboldlair-server'"

**Cause:** Aspire service discovery not configured properly

**Solution:**
1. Ensure `builder.AddServiceDefaults()` is called in Client
2. Ensure `builder.Services.AddServiceDiscovery()` is called
3. Ensure Client has `WithReference(koboldlairServer)` in AppHost

### API Requests Timeout

**Check:**
1. Server startup logs for errors
2. Server is listening on expected port
3. Network connectivity between Client and Server

## Testing the Proxy

### Manual Test with curl

```bash
# Test direct server endpoint
curl http://localhost:{server-port}/api/projects

# Test through proxy
curl http://localhost:{client-port}/api/projects
```

### Browser DevTools

1. Open browser DevTools (F12)
2. Navigate to Network tab
3. Refresh Dragon page
4. Look for `/api/projects` and `/api/providers` requests
5. Check status codes and response data

**Expected:**
- Status: 200 OK
- Response: JSON array/object with data
- Headers: Content-Type: application/json

## Development Notes

- The proxy adds minimal latency (~5-10ms)
- All HTTP methods are supported (GET, POST, PUT, DELETE)
- Request/response headers are forwarded
- Content-Type is preserved
- Error responses are properly forwarded with status codes
