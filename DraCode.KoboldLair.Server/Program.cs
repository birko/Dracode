using DraCode.Agent;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Server.Models;
using DraCode.KoboldLair.Server.Agents.Wyrm;
using DraCode.KoboldLair.Server.Agents.Kobold;
using DraCode.KoboldLair.Server.Supervisors;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.AddServiceDefaults();

// Log the current environment
Console.WriteLine($"[KoboldLair.Server] Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"[KoboldLair.Server] ASPNETCORE_ENVIRONMENT: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
Console.WriteLine($"[KoboldLair.Server] DOTNET_ENVIRONMENT: {Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}");

// Bind Authentication configuration from appsettings.json
builder.Services.Configure<AuthenticationConfiguration>(
    builder.Configuration.GetSection("Authentication"));

// Register authentication service
builder.Services.AddSingleton<WebSocketAuthenticationService>();

// Configure provider settings
builder.Services.Configure<KoboldLairProviderConfiguration>(
    builder.Configuration.GetSection("KoboldLairProviders"));

// Register provider configuration service
builder.Services.AddSingleton<ProviderConfigurationService>();

// Register project configuration service for resource limits
builder.Services.AddSingleton<ProjectConfigurationService>();

// Register project management components
builder.Services.AddSingleton<ProjectRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ProjectRepository>>();
    return new ProjectRepository("./projects", logger);
});

builder.Services.AddSingleton<WyrmFactory>(sp =>
{
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    return new WyrmFactory(
        providerConfigService,
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
    var projectConfigService = sp.GetRequiredService<ProjectConfigurationService>();
    var logger = sp.GetRequiredService<ILogger<ProjectService>>();
    return new ProjectService(repository, wyrmFactory, projectConfigService, logger, "./workspace");
});

// Register factories as singletons
builder.Services.AddSingleton<KoboldFactory>(sp =>
{
    var projectConfigService = sp.GetRequiredService<ProjectConfigurationService>();

    // Use ProjectConfigurationService for max parallel kobolds
    Func<string?, int> getMaxParallel = (projectId) =>
    {
        return projectConfigService.GetMaxParallelKobolds(projectId ?? string.Empty);
    };

    return new KoboldFactory(projectConfigService, getMaxParallel);
});
builder.Services.AddSingleton<DrakeFactory>(sp =>
{
    var koboldFactory = sp.GetRequiredService<KoboldFactory>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new DrakeFactory(koboldFactory, providerConfigService, loggerFactory);
});

// Register services
builder.Services.AddSingleton<WebSocketCommandHandler>();
builder.Services.AddSingleton<WyvernService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WyvernService>>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    var commandHandler = sp.GetRequiredService<WebSocketCommandHandler>();
    return new WyvernService(logger, providerConfigService, commandHandler);
});
builder.Services.AddSingleton<DragonService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DragonService>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    return new DragonService(logger, providerConfigService, projectService);
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

// Initialize project configurations on startup
using (var scope = app.Services.CreateScope())
{
    var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
    var providerConfigService = scope.ServiceProvider.GetRequiredService<ProviderConfigurationService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Initializing configurations for existing projects...");
    projectService.InitializeProjectConfigurations(providerConfigService);
    logger.LogInformation("Configuration initialization complete");
}

app.MapDefaultEndpoints();

// Enable CORS
app.UseCors();

// Enable WebSocket with keep-alive
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
};
app.UseWebSockets(webSocketOptions);

// WebSocket endpoint for Wyvern (task delegation) with authentication
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var authService = context.RequestServices.GetRequiredService<WebSocketAuthenticationService>();

        // Extract token and client IP
        var token = authService.ExtractTokenFromQuery(context);
        var clientIp = authService.GetClientIpAddress(context);

        // Validate authentication token with IP binding
        if (!authService.ValidateToken(token, clientIp))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Invalid or missing authentication token, or IP address not allowed");
            return;
        }

        var wyvernService = context.RequestServices.GetRequiredService<WyvernService>();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await wyvernService.HandleWebSocketAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// WebSocket endpoint for Dragon (requirements gathering) with authentication
app.Map("/dragon", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var authService = context.RequestServices.GetRequiredService<WebSocketAuthenticationService>();

        // Extract token and client IP
        var token = authService.ExtractTokenFromQuery(context);
        var clientIp = authService.GetClientIpAddress(context);

        // Validate authentication token with IP binding
        if (!authService.ValidateToken(token, clientIp))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Invalid or missing authentication token, or IP address not allowed");
            return;
        }

        var dragonService = context.RequestServices.GetRequiredService<DragonService>();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await dragonService.HandleWebSocketAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Health check endpoint
app.MapGet("/", () => new { status = "running", endpoints = new[] { "/ws", "/dragon" } });

app.Run();
