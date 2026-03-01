using Birko.Communication.WebSocket.Middleware;
using Birko.Communication.WebSocket.Services;
using DraCode.WebSocket.Models;
using DraCode.WebSocket.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Bind Agent configuration from appsettings.json
builder.Services.Configure<AgentConfiguration>(
    builder.Configuration.GetSection("Agent"));

// Add services to the container
builder.Services.AddSingleton<AgentConnectionManager>();

// Use Birko.Communication WebSocket authentication service
builder.Services.Configure<WebSocketAuthenticationConfiguration>(
    builder.Configuration.GetSection("Authentication"));
builder.Services.AddSingleton<WebSocketAuthenticationService>();

// Add CORS for web client
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Map Aspire default endpoints
app.MapDefaultEndpoints();

// Enable CORS
app.UseCors();

// Enable WebSockets
app.UseWebSockets();

// WebSocket endpoint using Birko.Communication middleware
app.MapWebSocket("/ws", async (webSocket, context) =>
{
    var connectionId = Guid.NewGuid().ToString();
    var manager = context.RequestServices.GetRequiredService<AgentConnectionManager>();
    await manager.HandleWebSocketAsync(webSocket, connectionId);
}, requireAuthentication: true);

// Health check endpoint
app.MapGet("/", () => new { status = "running", endpoint = "/ws" });

app.Run();
