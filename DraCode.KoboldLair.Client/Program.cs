using DraCode.KoboldLair.Client;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Bind configuration
var koboldLairConfig = builder.Configuration.GetSection("KoboldLair");
var serverUrl = koboldLairConfig.GetValue<string>("ServerUrl") ?? "ws://localhost:57087";

logger.LogInformation("KoboldLair Client starting with server URL: {ServerUrl}", serverUrl);

// Map Aspire default endpoints
app.MapDefaultEndpoints();

// Enable WebSockets
app.UseWebSockets();

// Serve configuration endpoint for frontend. It carries no token (TASK-119): anyone who reaches this host can read it,
// so the browser gets its token through sign-in instead.
app.MapGet("/api/config", () => Results.Json(new
{
    serverUrl,
    endpoints = new
    {
        wyvern = "/wyvern",
        dragon = "/dragon"
    }
}));

// WebSocket proxies to the server (TASK-119): certificate validated, the caller's own token forwarded, close status relayed.
ServerWebSocketProxy.Map(app, "/dragon", serverUrl);
ServerWebSocketProxy.Map(app, "/wyvern", serverUrl);

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
