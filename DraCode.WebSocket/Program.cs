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

// WebSocket endpoint
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var connectionId = Guid.NewGuid().ToString();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        
        var manager = context.RequestServices.GetRequiredService<AgentConnectionManager>();
        await manager.HandleWebSocketAsync(webSocket, connectionId);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

// Health check endpoint
app.MapGet("/", () => new { status = "running", endpoint = "/ws" });

app.Run();
