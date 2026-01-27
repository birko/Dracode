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

// Serve dynamic configuration (MUST come before wildcard /api/{**path})
app.MapGet("/api/config", async (IHttpClientFactory httpClientFactory) =>
{
    try
    {
        // Get the server URL from the HttpClient (resolved by service discovery)
        var client = httpClientFactory.CreateClient("KoboldLairServer");
        var baseAddress = client.BaseAddress?.ToString().TrimEnd('/');
        
        // Convert HTTP URL to WebSocket URL
        var wsUrl = baseAddress?.Replace("http://", "ws://").Replace("https://", "wss://") ?? "ws://localhost:5000";
        
        var config = new
        {
            serverUrl = wsUrl,
            apiUrl = baseAddress ?? "http://localhost:5000",
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
        // Fallback configuration
        var config = new
        {
            serverUrl = "ws://localhost:5000",
            apiUrl = "http://localhost:5000",
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

// Serve static files
app.UseStaticFiles();

// Serve index.html as default
app.MapFallbackToFile("index.html");

app.Run();
