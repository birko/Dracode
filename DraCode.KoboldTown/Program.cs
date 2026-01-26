using DraCode.Agent;
using DraCode.KoboldTown.Factories;
using DraCode.KoboldTown.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.AddServiceDefaults();

// Register project management components
builder.Services.AddSingleton<ProjectRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ProjectRepository>>();
    return new ProjectRepository("./projects", logger);
});

builder.Services.AddSingleton<WyrmFactory>(sp =>
{
    return new WyrmFactory(
        defaultProvider: "openai",
        defaultConfig: new Dictionary<string, string>
        {
            ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
            ["model"] = "gpt-4o"
        },
        defaultOptions: new AgentOptions
        {
            WorkingDirectory = "./workspace",
            Verbose = false
        }
    );
});

builder.Services.AddSingleton<ProjectService>(sp =>
{
    var repository = sp.GetRequiredService<ProjectRepository>();
    var wyrmFactory = sp.GetRequiredService<WyrmFactory>();
    var logger = sp.GetRequiredService<ILogger<ProjectService>>();
    return new ProjectService(repository, wyrmFactory, logger, "./workspace");
});

// Register factories as singletons
builder.Services.AddSingleton<KoboldFactory>();
builder.Services.AddSingleton<DrakeFactory>(sp =>
{
    var koboldFactory = sp.GetRequiredService<KoboldFactory>();
    return new DrakeFactory(
        koboldFactory,
        defaultProvider: "openai",
        defaultConfig: new Dictionary<string, string>
        {
            ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
            ["model"] = "gpt-4o"
        },
        defaultOptions: new AgentOptions
        {
            WorkingDirectory = "./workspace",
            Verbose = false
        }
    );
});

// Register services
builder.Services.AddSingleton<WyvernService>();
builder.Services.AddSingleton<DragonService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DragonService>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    return new DragonService(
        logger,
        defaultProvider: "openai",
        defaultConfig: new Dictionary<string, string>
        {
            ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
            ["model"] = "gpt-4o"
        },
        projectService: projectService
    );
});

// Register background monitoring service (checks every 60 seconds)
builder.Services.AddHostedService<DrakeMonitoringService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DrakeMonitoringService>>();
    var drakeFactory = sp.GetRequiredService<DrakeFactory>();
    return new DrakeMonitoringService(logger, drakeFactory, monitoringIntervalSeconds: 60);
});

// Register Wyrm processing background service (checks every 60 seconds)
builder.Services.AddHostedService<WyrmProcessingService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WyrmProcessingService>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    return new WyrmProcessingService(logger, projectService, "./specifications", checkIntervalSeconds: 60);
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Enable WebSocket
app.UseWebSockets();

// Serve static files
app.UseStaticFiles();

// WebSocket endpoint for Wyvern (task delegation)
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var WyvernService = context.RequestServices.GetRequiredService<WyvernService>();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await WyvernService.HandleWebSocketAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// WebSocket endpoint for Dragon (requirements gathering)
app.Map("/dragon", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var dragonService = context.RequestServices.GetRequiredService<DragonService>();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await dragonService.HandleWebSocketAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Serve index.html as default
app.MapFallbackToFile("index.html");

app.Run();
