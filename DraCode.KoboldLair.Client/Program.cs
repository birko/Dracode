using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Bind configuration
var koboldLairConfig = builder.Configuration.GetSection("KoboldLair");
var serverUrl = koboldLairConfig.GetValue<string>("ServerUrl") ?? "ws://localhost:5000";
var authToken = koboldLairConfig.GetValue<string>("AuthToken") ?? "";

var app = builder.Build();

// Map Aspire default endpoints
app.MapDefaultEndpoints();

// Enable WebSockets
app.UseWebSockets();

// Serve configuration endpoint for frontend
app.MapGet("/api/config", () => Results.Json(new
{
    serverUrl,
    authToken,
    endpoints = new
    {
        wyvern = "/ws",
        dragon = "/dragon"
    }
}));

// WebSocket proxy for /dragon endpoint
app.Map("/dragon", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    try
    {
        // Convert http(s):// to ws(s):// for WebSocket connection
        var wsUrl = serverUrl.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/') + "/dragon";
        
        // Extract token if present in query or use config
        var token = context.Request.Query["token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            token = authToken;
        }
        
        if (!string.IsNullOrEmpty(token))
        {
            wsUrl += $"?token={token}";
        }

        var clientWs = await context.WebSockets.AcceptWebSocketAsync();
        var serverWs = new ClientWebSocket();
        
        // Bypass SSL certificate validation for development
        serverWs.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
        
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
app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    try
    {
        // Convert http(s):// to ws(s):// for WebSocket connection
        var wsUrl = serverUrl.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/') + "/ws";
        
        // Extract token if present in query or use config
        var token = context.Request.Query["token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            token = authToken;
        }
        
        if (!string.IsNullOrEmpty(token))
        {
            wsUrl += $"?token={token}";
        }

        var clientWs = await context.WebSockets.AcceptWebSocketAsync();
        var serverWs = new ClientWebSocket();
        
        // Bypass SSL certificate validation for development
        serverWs.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
        
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
var staticFileOptions = new StaticFileOptions();

// Disable caching in development
if (app.Environment.IsDevelopment())
{
    staticFileOptions.OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
        ctx.Context.Response.Headers.Append("Expires", "0");
    };
}

app.UseStaticFiles(staticFileOptions);

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
                Console.WriteLine($"[KoboldLair Client] WebSocket close received from {(source == destination ? "client" : "server")}");
                await destination.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            // Log received message for debugging
            var messageText = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"[KoboldLair Client] Message received ({result.Count} bytes): {messageText}");

            await destination.SendAsync(
                new ArraySegment<byte>(buffer, 0, result.Count),
                result.MessageType,
                result.EndOfMessage,
                cancellationToken);

            // Log sent message for debugging
            Console.WriteLine($"[KoboldLair Client] Message sent ({result.Count} bytes): {messageText}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[KoboldLair Client] WebSocket relay error: {ex.Message}");
    }
}
