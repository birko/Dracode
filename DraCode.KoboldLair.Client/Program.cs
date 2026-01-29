using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Bind configuration
var koboldLairConfig = builder.Configuration.GetSection("KoboldLair");
var serverUrl = koboldLairConfig.GetValue<string>("ServerUrl") ?? "ws://localhost:5000";
var authToken = koboldLairConfig.GetValue<string>("AuthToken") ?? "";

logger.LogInformation("KoboldLair Client starting with server URL: {ServerUrl}", serverUrl);

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
        wyvern = "/wyvern",
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
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "WebSocket proxy error on /dragon endpoint");
        context.Response.StatusCode = 500;
    }
});

// WebSocket proxy for /wyvern endpoint (Wyvern)
app.Map("/wyvern", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    try
    {
        // Convert http(s):// to ws(s):// for WebSocket connection
        var wsUrl = serverUrl.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/') + "/wyvern";

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
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "WebSocket proxy error on /wyvern endpoint");
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
    catch (Exception)
    {
        // Connection closed or error - silently exit relay
    }
}
