using DraCode.Agent;
using DraCode.KoboldLair.Server.Agents.Kobold;
using DraCode.KoboldLair.Server.Agents.Wyvern;
using DraCode.KoboldLair.Server.Models;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Server.Supervisors;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.AddServiceDefaults();

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

builder.Services.AddSingleton<WyvernFactory>(sp =>
{
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    return new WyvernFactory(
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
    var wyvernFactory = sp.GetRequiredService<WyvernFactory>();
    var projectConfigService = sp.GetRequiredService<ProjectConfigurationService>();
    var logger = sp.GetRequiredService<ILogger<ProjectService>>();
    return new ProjectService(repository, wyvernFactory, projectConfigService, logger, "./workspace");
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
builder.Services.AddSingleton<WyrmService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WyrmService>>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    var commandHandler = sp.GetRequiredService<WebSocketCommandHandler>();
    return new WyrmService(logger, providerConfigService, commandHandler);
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

// Register Wyvern processing background service (checks every 60 seconds)
builder.Services.AddHostedService<WyvernProcessingService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WyvernProcessingService>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    return new WyvernProcessingService(logger, projectService, "./specifications", checkIntervalSeconds: 60);
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

    if (logger?.IsEnabled(LogLevel.Information) ?? false)
    {
        logger.LogInformation("Environment: {EnvironmentName}", builder.Environment.EnvironmentName);
        logger.LogInformation("Initializing configurations for existing projects...");
    }
    projectService.InitializeProjectConfigurations(providerConfigService);
    if (logger?.IsEnabled(LogLevel.Information) ?? false)
    {
        logger.LogInformation("Configuration initialization complete");
    }
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

// WebSocket endpoint for Wyvern (project analysis) with authentication
app.Map("/wyvern", async context =>
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

        var wyrmService = context.RequestServices.GetRequiredService<WyrmService>();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await wyrmService.HandleWebSocketAsync(webSocket);
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
app.MapGet("/", () => new { status = "running", endpoints = new[] { "/wyvern", "/dragon" } });

app.Run();
