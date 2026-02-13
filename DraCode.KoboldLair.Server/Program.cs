using DraCode.Agent;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Services;
using DraCode.KoboldLair.Server.Models.Configuration;
using DraCode.KoboldLair.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Load local configuration (not committed to git)
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

// Add services
builder.AddServiceDefaults();

// Bind Authentication configuration
builder.Services.Configure<AuthenticationConfiguration>(
    builder.Configuration.GetSection("Authentication"));

// Register authentication service
builder.Services.AddSingleton<WebSocketAuthenticationService>();

// Configure KoboldLair settings (providers, defaults, limits - all in one place)
builder.Services.Configure<KoboldLairConfiguration>(
    builder.Configuration.GetSection("KoboldLair"));

// Register provider configuration service (must be registered before ProjectConfigurationService)
builder.Services.AddSingleton<ProviderConfigurationService>();

// Register project configuration service (depends on ProviderConfigurationService for defaults)
builder.Services.AddSingleton<ProjectConfigurationService>();

// Register provider circuit breaker for failure tracking
builder.Services.AddSingleton<ProviderCircuitBreaker>();

// Register git service for version control integration
builder.Services.AddSingleton<GitService>();

// Register project management components
builder.Services.AddSingleton<ProjectRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ProjectRepository>>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    return new ProjectRepository(config.ProjectsPath ?? "./projects", logger);
});

// Register plan service for implementation plan persistence
builder.Services.AddSingleton<KoboldPlanService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<KoboldPlanService>>();
    var projectRepository = sp.GetRequiredService<ProjectRepository>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    return new KoboldPlanService(config.ProjectsPath ?? "./projects", logger, projectRepository);
});

// Register shared planning context service for cross-agent coordination
builder.Services.AddSingleton<SharedPlanningContextService>(sp =>
{
    var planService = sp.GetRequiredService<KoboldPlanService>();
    var projectRepository = sp.GetRequiredService<ProjectRepository>();
    var logger = sp.GetRequiredService<ILogger<SharedPlanningContextService>>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    return new SharedPlanningContextService(config.ProjectsPath ?? "./projects", planService, projectRepository, logger);
});

builder.Services.AddSingleton<WyvernFactory>(sp =>
{
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    var projectConfigService = sp.GetRequiredService<ProjectConfigurationService>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var gitService = sp.GetRequiredService<GitService>();
    return new WyvernFactory(providerConfigService, projectConfigService, config, gitService: gitService);
});

builder.Services.AddSingleton<ProjectService>(sp =>
{
    var repository = sp.GetRequiredService<ProjectRepository>();
    var wyvernFactory = sp.GetRequiredService<WyvernFactory>();
    var logger = sp.GetRequiredService<ILogger<ProjectService>>();
    var gitService = sp.GetRequiredService<GitService>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    return new ProjectService(repository, wyvernFactory, logger, gitService, config);
});

// Register factories as singletons
builder.Services.AddSingleton<KoboldFactory>(sp =>
{
    var projectConfigService = sp.GetRequiredService<ProjectConfigurationService>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;

    // Use ProjectConfigurationService for max parallel kobolds
    Func<string?, int> getMaxParallel = (projectId) =>
    {
        return projectConfigService.GetMaxParallelKobolds(projectId ?? string.Empty);
    };

    return new KoboldFactory(projectConfigService, loggerFactory, config, getMaxParallel);
});
builder.Services.AddSingleton<WyrmFactory>(sp =>
{
    var projectConfigService = sp.GetRequiredService<ProjectConfigurationService>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    return new WyrmFactory(projectConfigService, providerConfigService);
});
builder.Services.AddSingleton<DrakeFactory>(sp =>
{
    var koboldFactory = sp.GetRequiredService<KoboldFactory>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    var projectConfigService = sp.GetRequiredService<ProjectConfigurationService>();
    var projectRepository = sp.GetRequiredService<ProjectRepository>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var gitService = sp.GetRequiredService<GitService>();
    var circuitBreaker = sp.GetRequiredService<ProviderCircuitBreaker>();
    var sharedPlanningContext = sp.GetRequiredService<SharedPlanningContextService>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    return new DrakeFactory(koboldFactory, providerConfigService, projectConfigService, config, loggerFactory, gitService, projectRepository, circuitBreaker, sharedPlanningContext);
});

// Register services
builder.Services.AddSingleton<WebSocketCommandHandler>();
builder.Services.AddSingleton<WyrmService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WyrmService>>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var commandHandler = sp.GetRequiredService<WebSocketCommandHandler>();
    return new WyrmService(logger, providerConfigService, config, commandHandler);
});
builder.Services.AddSingleton<DragonService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DragonService>>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    var projectConfigService = sp.GetRequiredService<ProjectConfigurationService>();
    var projectService = sp.GetRequiredService<ProjectService>();
    var gitService = sp.GetRequiredService<GitService>();
    var koboldFactory = sp.GetRequiredService<KoboldFactory>();
    var drakeFactory = sp.GetRequiredService<DrakeFactory>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    return new DragonService(logger, providerConfigService, projectConfigService, projectService, gitService, config, koboldFactory, drakeFactory);
});

// Register graceful shutdown coordinator (signals Kobolds to save state on shutdown)
builder.Services.AddSingleton<GracefulShutdownCoordinator>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<GracefulShutdownCoordinator>>();
    return new GracefulShutdownCoordinator(logger, gracePeriod: TimeSpan.FromSeconds(10));
});

// Register background monitoring service
builder.Services.AddHostedService<DrakeMonitoringService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DrakeMonitoringService>>();
    var drakeFactory = sp.GetRequiredService<DrakeFactory>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var limits = config.Limits;
    return new DrakeMonitoringService(
        logger,
        drakeFactory,
        monitoringIntervalSeconds: limits.MonitoringIntervalSeconds,
        stuckKoboldTimeoutMinutes: limits.StuckKoboldTimeoutMinutes);
});

// Register Drake execution service (picks up analyzed projects and starts Kobolds)
builder.Services.AddHostedService<DrakeExecutionService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DrakeExecutionService>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    var drakeFactory = sp.GetRequiredService<DrakeFactory>();
    var shutdownCoordinator = sp.GetRequiredService<GracefulShutdownCoordinator>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var maxIterations = config.Iterations?.MaxKoboldIterations ?? 100;
    return new DrakeExecutionService(
        logger,
        projectService,
        drakeFactory,
        shutdownCoordinator,
        executionIntervalSeconds: 30,
        maxKoboldIterations: maxIterations);
});

// Register Wyrm processing background service (New → WyrmAssigned, checks every 60 seconds)
builder.Services.AddHostedService<WyrmProcessingService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WyrmProcessingService>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    var wyrmFactory = sp.GetRequiredService<WyrmFactory>();
    return new WyrmProcessingService(logger, projectService, wyrmFactory, checkIntervalSeconds: 60);
});

// Register Wyvern processing background service (WyrmAssigned → Analyzed, checks every 60 seconds)
builder.Services.AddHostedService<WyvernProcessingService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WyvernProcessingService>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    return new WyvernProcessingService(logger, projectService, checkIntervalSeconds: 60);
});

// Register Failure Recovery Service (auto-retries transient failures, checks every 5 minutes)
builder.Services.AddHostedService<FailureRecoveryService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FailureRecoveryService>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    var drakeFactory = sp.GetRequiredService<DrakeFactory>();
    var circuitBreaker = sp.GetRequiredService<ProviderCircuitBreaker>();
    return new FailureRecoveryService(
        logger,
        projectService,
        drakeFactory,
        circuitBreaker,
        checkIntervalSeconds: 300, // 5 minutes
        maxRetryAttempts: 5);
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
        logger.LogInformation("Projects loaded: {Count}", projectService.GetAllProjects().Count);
        logger.LogInformation("Initializing configurations for existing projects...");
    }
    await projectService.InitializeProjectConfigurationsAsync(providerConfigService);
    if (logger?.IsEnabled(LogLevel.Information) ?? false)
    {
        logger.LogInformation("Configuration initialization complete");
    }
}

// Register shutdown hook for graceful shutdown
var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
appLifetime.ApplicationStopping.Register(() =>
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // 1. Signal all active Kobolds to save state and stop
        var shutdownCoordinator = scope.ServiceProvider.GetRequiredService<GracefulShutdownCoordinator>();
        shutdownCoordinator.InitiateShutdown();

        // 2. Wait for grace period to let active LLM calls complete
        logger.LogInformation("Waiting {GracePeriod}s for active Kobolds to save state...", shutdownCoordinator.GracePeriod.TotalSeconds);
        Thread.Sleep(shutdownCoordinator.GracePeriod);

        // 3. Flush all Drake save channels to ensure no task state is lost
        logger.LogInformation("Flushing all Drake save channels...");
        var drakeFactory = scope.ServiceProvider.GetRequiredService<DrakeFactory>();
        var allDrakes = drakeFactory.GetAllDrakes();
        if (allDrakes.Count > 0)
        {
            var flushTasks = allDrakes.Select(d => d.FlushAndCloseAsync()).ToArray();
            Task.WhenAll(flushTasks).GetAwaiter().GetResult();
            logger.LogInformation("Flushed {Count} Drake(s) successfully", allDrakes.Count);
        }

        // 4. Persist shared planning contexts
        logger.LogInformation("Persisting shared planning contexts on shutdown...");
        var sharedPlanningContext = scope.ServiceProvider.GetRequiredService<SharedPlanningContextService>();
        sharedPlanningContext.PersistAllContextsAsync().GetAwaiter().GetResult();
        logger.LogInformation("Shared planning contexts persisted successfully");

        logger.LogInformation("Graceful shutdown complete");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during graceful shutdown");
    }
});

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

        // Extract sessionId from query string for session resumption
        var sessionId = context.Request.Query["sessionId"].FirstOrDefault();

        var dragonService = context.RequestServices.GetRequiredService<DragonService>();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await dragonService.HandleWebSocketAsync(webSocket, sessionId);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Health check endpoint
app.MapGet("/", () => new { status = "running", endpoints = new[] { "/wyvern", "/dragon" } });

app.Run();
