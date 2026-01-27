using DraCode.Agent;
using DraCode.KoboldLair.Server.Factories;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Server.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.AddServiceDefaults();

// Bind Authentication configuration from appsettings.json
builder.Services.Configure<AuthenticationConfiguration>(
    builder.Configuration.GetSection("Authentication"));

// Register authentication service
builder.Services.AddSingleton<WebSocketAuthenticationService>();

// Configure provider settings
builder.Services.Configure<KoboldTownProviderConfiguration>(
    builder.Configuration.GetSection("KoboldTownProviders"));

// Register provider configuration service
builder.Services.AddSingleton<ProviderConfigurationService>();

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
    var logger = sp.GetRequiredService<ILogger<ProjectService>>();
    return new ProjectService(repository, wyrmFactory, logger, "./workspace");
});

// Register factories as singletons
builder.Services.AddSingleton<KoboldFactory>();
builder.Services.AddSingleton<DrakeFactory>(sp =>
{
    var koboldFactory = sp.GetRequiredService<KoboldFactory>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    return new DrakeFactory(koboldFactory, providerConfigService);
});

// Register services
builder.Services.AddSingleton<WyvernService>();
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

// Initialize project providers on startup
using (var scope = app.Services.CreateScope())
{
    var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
    var providerConfigService = scope.ServiceProvider.GetRequiredService<ProviderConfigurationService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("Initializing provider settings for existing projects...");
    projectService.InitializeProjectProviders(providerConfigService);
    logger.LogInformation("Provider initialization complete");
}

app.MapDefaultEndpoints();

// Enable CORS
app.UseCors();

// Enable WebSocket
app.UseWebSockets();

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

// API endpoint for hierarchy data
app.MapGet("/api/hierarchy", (
    ProjectService projectService,
    DragonService dragonService,
    DrakeFactory drakeFactory,
    WyrmFactory wyrmFactory) =>
{
    var projects = projectService.GetAllProjects();
    var stats = projectService.GetStatistics();
    var dragonStats = dragonService.GetStatistics();
    var drakes = drakeFactory.GetAllDrakes();
    
    // Calculate totals
    var totalKobolds = drakes.Sum(d => d.GetStatistics().WorkingKobolds);
    var totalWyrms = wyrmFactory.TotalWyrms;
    
    var response = new
    {
        statistics = new
        {
            dragonSessions = dragonStats.ActiveSessions,
            projects = stats.TotalProjects,
            wyrms = totalWyrms,
            drakes = drakes.Count,
            koboldsWorking = totalKobolds
        },
        projects = projects.Select(p => new
        {
            p.Id,
            p.Name,
            p.Status,
            p.WyrmId,
            p.CreatedAt,
            p.AnalyzedAt,
            p.OutputPath,
            p.SpecificationPath,
            p.TaskFiles,
            p.ErrorMessage
        }),
        hierarchy = new
        {
            dragon = new
            {
                name = "Dragon Requirements Agent",
                icon = "🐉",
                status = dragonStats.ActiveSessions > 0 ? "active" : "idle",
                activeSessions = dragonStats.ActiveSessions
            },
            projects = projects.Where(p => p.WyrmId != null).Select(p =>
            {
                var wyrm = wyrmFactory.GetWyrm(p.Name);
                return new
                {
                    id = p.Id,
                    name = p.Name,
                    icon = "📁",
                    status = p.Status.ToString().ToLower(),
                    wyrm = wyrm != null ? new
                    {
                        id = p.WyrmId,
                        name = $"Wyrm ({p.Name})",
                        icon = "🐲",
                        status = p.Status == ProjectStatus.Analyzed ? "active" : "working",
                        analyzed = p.Status >= ProjectStatus.Analyzed,
                        totalTasks = wyrm.Analysis?.TotalTasks ?? 0
                    } : null
                };
            }).ToList()
        }
    };
    
    return Results.Json(response);
});

// API endpoint for project statistics
app.MapGet("/api/projects", (ProjectService projectService) =>
{
    var projects = projectService.GetAllProjects();
    return Results.Json(projects);
});

// API endpoint for statistics
app.MapGet("/api/stats", (
    ProjectService projectService,
    DragonService dragonService,
    DrakeFactory drakeFactory,
    WyrmFactory wyrmFactory) =>
{
    var projectStats = projectService.GetStatistics();
    var dragonStats = dragonService.GetStatistics();
    var drakes = drakeFactory.GetAllDrakes();
    
    var response = new
    {
        projects = projectStats,
        dragon = dragonStats,
        drakes = drakes.Count,
        wyrms = wyrmFactory.TotalWyrms,
        koboldsWorking = drakes.Sum(d => d.GetStatistics().WorkingKobolds)
    };
    
    return Results.Json(response);
});

// API endpoints for provider configuration
app.MapGet("/api/providers", (ProviderConfigurationService providerService) =>
{
    var config = providerService.GetConfiguration();
    var providers = config.Providers.Select(p => new
    {
        p.Name,
        p.DisplayName,
        p.Type,
        p.DefaultModel,
        p.CompatibleAgents,
        p.IsEnabled,
        p.RequiresApiKey,
        p.Description,
        IsConfigured = providerService.ValidateProvider(p.Name).isValid
    });
    
    return Results.Json(new
    {
        providers,
        agentProviders = config.AgentProviders
    });
});

app.MapPost("/api/providers/configure", (
    ProviderConfigurationService providerService,
    AgentProviderUpdate update) =>
{
    try
    {
        providerService.SetProviderForAgent(update.AgentType, update.ProviderName, update.ModelOverride);
        return Results.Ok(new { success = true, message = $"Updated {update.AgentType} to use {update.ProviderName}" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapGet("/api/providers/validate/{providerName}", (
    ProviderConfigurationService providerService,
    string providerName) =>
{
    var (isValid, message) = providerService.ValidateProvider(providerName);
    return Results.Json(new { isValid, message, providerName });
});

app.MapGet("/api/providers/agents/{agentType}", (
    ProviderConfigurationService providerService,
    string agentType) =>
{
    var providers = providerService.GetProvidersForAgent(agentType);
    var currentProvider = providerService.GetProviderForAgent(agentType);
    
    return Results.Json(new
    {
        agentType,
        currentProvider,
        availableProviders = providers.Select(p => new
        {
            p.Name,
            p.DisplayName,
            p.DefaultModel,
            p.Description,
            IsConfigured = providerService.ValidateProvider(p.Name).isValid
        })
    });
});

// Project-specific provider endpoints
app.MapGet("/api/projects/{projectId}/providers", (
    ProjectService projectService,
    ProviderConfigurationService providerConfigService,
    string projectId) =>
{
    try
    {
        var project = projectService.GetProject(projectId);
        if (project == null)
        {
            return Results.NotFound(new { error = "Project not found" });
        }

        var settings = projectService.GetProjectProviders(projectId, providerConfigService);
        
        return Results.Json(new
        {
            projectId,
            projectName = project.Name,
            providers = settings,
            availableProviders = providerConfigService.GetAvailableProviders().Select(p => new
            {
                p.Name,
                p.DisplayName,
                p.DefaultModel,
                p.CompatibleAgents,
                IsConfigured = providerConfigService.ValidateProvider(p.Name).isValid
            })
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/projects/{projectId}/providers", (
    ProjectService projectService,
    string projectId,
    ProjectProviderSettings settings) =>
{
    try
    {
        projectService.SetProjectProviders(projectId, settings);
        return Results.Ok(new { success = true, message = "Provider settings updated for project" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapPost("/api/projects/{projectId}/agents/{agentType}/toggle", (
    ProjectService projectService,
    string projectId,
    string agentType,
    AgentToggleRequest request) =>
{
    try
    {
        projectService.SetAgentEnabled(projectId, agentType, request.Enabled);
        var status = request.Enabled ? "enabled" : "disabled";
        return Results.Ok(new { success = true, message = $"{agentType} {status} for project", enabled = request.Enabled });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapGet("/api/projects/{projectId}/agents/{agentType}/status", (
    ProjectService projectService,
    string projectId,
    string agentType) =>
{
    try
    {
        var enabled = projectService.IsAgentEnabled(projectId, agentType);
        return Results.Json(new { projectId, agentType, enabled });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Health check endpoint
app.MapGet("/", () => new { status = "running", endpoints = new[] { "/ws", "/dragon" } });

app.Run();

// DTOs
record AgentProviderUpdate(string AgentType, string ProviderName, string? ModelOverride);
record AgentToggleRequest(bool Enabled);
