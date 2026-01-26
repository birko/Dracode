using DraCode.Agent;
using DraCode.KoboldTown.Factories;
using DraCode.KoboldTown.Services;
using DraCode.KoboldTown.Models;

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

// Serve index.html as default
app.MapFallbackToFile("index.html");

app.Run();
