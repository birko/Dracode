using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (includes service discovery)
builder.AddServiceDefaults();

// Configure service discovery
builder.Services.AddServiceDiscovery();

// Add HTTP client for proxying to KoboldLair Server with service discovery
builder.Services.AddHttpClient("KoboldLairServer", client =>
{
    // Placeholder - will be resolved by service discovery
    client.BaseAddress = new Uri("http://dracode-koboldlair-server");
})
.AddServiceDiscovery(); // Enable service discovery for this client

var app = builder.Build();

// Map Aspire default endpoints
app.MapDefaultEndpoints();

// Enable WebSockets
app.UseWebSockets();

// Serve dynamic configuration (MUST come before wildcard /api/{**path})
app.MapGet("/api/config", async (HttpContext context, IHttpClientFactory httpClientFactory) =>
{
    try
    {
        // For WebSocket connections, use relative URLs (proxy through this client)
        var config = new
        {
            serverUrl = "", // Empty means use current origin
            apiUrl = "",
            authToken = "",
            endpoints = new
            {
                wyvern = "/ws",
                dragon = "/dragon"
            }
        };
        
        return Results.Json(config);
    }
    catch
    {
        var config = new
        {
            serverUrl = "",
            apiUrl = "",
            authToken = "",
            endpoints = new
            {
                wyvern = "/ws",
                dragon = "/dragon"
            }
        };
        
        return Results.Json(config);
    }
});

// Proxy API requests to the server (comes after specific routes)
app.MapGet("/api/{**path}", async (string path, IHttpClientFactory httpClientFactory) =>
{
    try
    {
        var client = httpClientFactory.CreateClient("KoboldLairServer");
        var response = await client.GetAsync($"/api/{path}");
        var content = await response.Content.ReadAsStringAsync();
        return Results.Content(content, response.Content.Headers.ContentType?.ToString() ?? "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error proxying request: {ex.Message}", statusCode: 500);
    }
});

app.MapPost("/api/{**path}", async (string path, HttpContext context, IHttpClientFactory httpClientFactory) =>
{
    try
    {
        var client = httpClientFactory.CreateClient("KoboldLairServer");
        var requestContent = new StreamContent(context.Request.Body);
        
        foreach (var header in context.Request.Headers)
        {
            if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                requestContent.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
        
        var response = await client.PostAsync($"/api/{path}", requestContent);
        var content = await response.Content.ReadAsStringAsync();
        return Results.Content(content, response.Content.Headers.ContentType?.ToString() ?? "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error proxying request: {ex.Message}", statusCode: 500);
    }
});

// WebSocket proxy for /dragon endpoint
app.Map("/dragon", async (HttpContext context, IHttpClientFactory httpClientFactory) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    try
    {
        var client = httpClientFactory.CreateClient("KoboldLairServer");
        var serverUrl = client.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost:5000";
        var wsUrl = serverUrl.Replace("http://", "ws://").Replace("https://", "ws://") + "/dragon";
        
        // Extract token if present
        var token = context.Request.Query["token"].ToString();
        if (!string.IsNullOrEmpty(token))
        {
            wsUrl += $"?token={token}";
        }

        var clientWs = await context.WebSockets.AcceptWebSocketAsync();
        var serverWs = new ClientWebSocket();
        
        await serverWs.ConnectAsync(new Uri(wsUrl), context.RequestAborted);

        // Bidirectional proxy
        var clientToServer = RelayWebSocketAsync(clientWs, serverWs, context.RequestAborted);
        var serverToClient = RelayWebSocketAsync(serverWs, clientWs, context.RequestAborted);

        await Task.WhenAny(clientToServer, serverToClient);
        
        if (clientWs.State == WebSocketState.Open)
            await clientWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Proxy closed", CancellationToken.None);
        if (serverWs.State == WebSocketState.Open)
            await serverWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Proxy closed", CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WebSocket proxy error: {ex.Message}");
        context.Response.StatusCode = 500;
    }
});

// WebSocket proxy for /ws endpoint (Wyvern)
app.Map("/ws", async (HttpContext context, IHttpClientFactory httpClientFactory) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    try
    {
        var client = httpClientFactory.CreateClient("KoboldLairServer");
        var serverUrl = client.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost:5000";
        var wsUrl = serverUrl.Replace("http://", "ws://").Replace("https://", "ws://") + "/ws";
        
        // Extract token if present
        var token = context.Request.Query["token"].ToString();
        if (!string.IsNullOrEmpty(token))
        {
            wsUrl += $"?token={token}";
        }

        var clientWs = await context.WebSockets.AcceptWebSocketAsync();
        var serverWs = new ClientWebSocket();
        
        await serverWs.ConnectAsync(new Uri(wsUrl), context.RequestAborted);

        // Bidirectional proxy
        var clientToServer = RelayWebSocketAsync(clientWs, serverWs, context.RequestAborted);
        var serverToClient = RelayWebSocketAsync(serverWs, clientWs, context.RequestAborted);

        await Task.WhenAny(clientToServer, serverToClient);
        
        if (clientWs.State == WebSocketState.Open)
            await clientWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Proxy closed", CancellationToken.None);
        if (serverWs.State == WebSocketState.Open)
            await serverWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Proxy closed", CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WebSocket proxy error: {ex.Message}");
        context.Response.StatusCode = 500;
    }
});

// Serve static files
app.UseStaticFiles();

// Serve index.html as default
app.MapFallbackToFile("index.html");

app.Run();

// Helper method for WebSocket relaying
static async Task RelayWebSocketAsync(WebSocket source, WebSocket destination, CancellationToken cancellationToken)
{
    var buffer = new byte[1024 * 4];
    try
    {
        while (source.State == WebSocketState.Open && destination.State == WebSocketState.Open)
        {
            var result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await destination.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            await destination.SendAsync(
                new ArraySegment<byte>(buffer, 0, result.Count),
                result.MessageType,
                result.EndOfMessage,
                cancellationToken);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WebSocket relay error: {ex.Message}");
    }
}
