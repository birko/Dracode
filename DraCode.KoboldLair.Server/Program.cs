using Birko.Communication.WebSocket.Middleware;
using Birko.Communication.WebSocket.Services;
using Birko.Security;
using Birko.Security.Authorization;
using Birko.Security.Jwt;
using Birko.Security.Hashing;
using DraCode.Agent;
using Birko.EventBus;
using Birko.EventBus.Extensions;
using Birko.MessageQueue;
using Birko.MessageQueue.InMemory;
using DraCode.KoboldLair.Data;
using DraCode.KoboldLair.Data.Migrations;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Events;
using DraCode.KoboldLair.Events.Handlers;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Services;
using Birko.BackgroundJobs;
using Birko.BackgroundJobs.Processing;
using DraCode.KoboldLair.Server.Jobs;
using DraCode.KoboldLair.Server.Services;
using Birko.Validation;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Validation;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Events.Specification;
using DraCode.KoboldLair.Services.EventSourcing;
using DraCode.KoboldLair.Messages;
using DraCode.KoboldLair.MessageQueue;

var builder = WebApplication.CreateBuilder(args);

// Load local configuration (not committed to git)
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

// Add services
builder.AddServiceDefaults();

// Register Birko.Communication authentication service (legacy token auth)
builder.Services.Configure<WebSocketAuthenticationConfiguration>(
    builder.Configuration.GetSection("Authentication"));
builder.Services.AddSingleton<WebSocketAuthenticationService>();

// Register Birko.Security.Jwt authentication
builder.Services.Configure<JwtAuthenticationConfiguration>(
    builder.Configuration.GetSection("Authentication:Jwt"));
builder.Services.AddSingleton<ITokenProvider>(sp =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtAuthenticationConfiguration>>().Value;
    var secret = config.ResolveSecret();
    if (string.IsNullOrEmpty(secret))
    {
        // Use a development-only key when no secret is configured
        secret = "KoboldLair-Development-Secret-Key-Do-Not-Use-In-Production!";
    }
    return new JwtTokenProvider(new TokenOptions
    {
        Secret = secret,
        Issuer = config.Issuer,
        Audience = config.Audience,
        ExpirationMinutes = config.ExpirationMinutes,
        RefreshExpirationDays = config.RefreshExpirationDays
    });
});
builder.Services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher());
builder.Services.AddSingleton<IRoleProvider, KoboldLairRoleProvider>();
builder.Services.AddSingleton<IPermissionChecker, KoboldLairPermissionChecker>();
builder.Services.AddSingleton<RefreshTokenStore>();

// Register Birko.Validation validators
builder.Services.AddSingleton<IValidator<Specification>, SpecificationValidator>();
builder.Services.AddSingleton<IValidator<Feature>, FeatureValidator>();
builder.Services.AddSingleton<IValidator<AgentsConfig>, ProjectConfigValidator>();

// Configure KoboldLair settings (providers, defaults, limits - all in one place)
builder.Services.Configure<KoboldLairConfiguration>(
    builder.Configuration.GetSection("KoboldLair"));

// Register provider configuration service (must be registered before ProjectConfigurationService)
builder.Services.AddSingleton<ProviderConfigurationService>();

// Register project configuration service (depends on ProviderConfigurationService for defaults)
builder.Services.AddSingleton<ProjectConfigurationService>();

// Register provider circuit breaker for failure tracking
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ProviderCircuitBreaker");
    return new ProviderCircuitBreaker(logger: logger);
});

// Register git service for version control integration
builder.Services.AddSingleton<GitService>();

// Register project management components
// Configure data storage (SQLite or JSON based on config)
builder.Services.Configure<DataStorageConfig>(
    builder.Configuration.GetSection("KoboldLair:Data"));

builder.Services.AddSingleton<ProjectRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ProjectRepository>>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    return new ProjectRepository(config.ProjectsPath ?? "./projects", logger);
});

// Register IProjectRepository - uses SQLite when configured, falls back to JSON ProjectRepository
builder.Services.AddSingleton<IProjectRepository>(sp =>
{
    var dataConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataStorageConfig>>().Value;
    var koboldConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    dataConfig.ProjectsPath = koboldConfig.ProjectsPath ?? "./projects";

    if (dataConfig.DefaultBackend == StorageBackend.SqLite)
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var repo = RepositoryFactory.CreateProjectRepositoryAsync(dataConfig, loggerFactory).GetAwaiter().GetResult();
        return repo;
    }

    // Default: use existing JSON-based ProjectRepository
    return sp.GetRequiredService<ProjectRepository>();
});

// Register ITaskRepository - uses SQLite when configured, null otherwise (TaskTracker uses JSON fallback)
builder.Services.AddSingleton<ITaskRepository>(sp =>
{
    var dataConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataStorageConfig>>().Value;
    var koboldConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    dataConfig.ProjectsPath = koboldConfig.ProjectsPath ?? "./projects";

    if (dataConfig.DefaultBackend == StorageBackend.SqLite)
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var repo = RepositoryFactory.CreateTaskRepositoryAsync(dataConfig, loggerFactory).GetAwaiter().GetResult();
        return repo;
    }

    return null!; // TaskTracker falls back to JSON file persistence
});

// Register SQL plan repository (null when not using SQLite)
builder.Services.AddSingleton<SqlPlanRepository>(sp =>
{
    var dataConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataStorageConfig>>().Value;
    if (dataConfig.DefaultBackend == StorageBackend.SqLite)
    {
        var dbPath = RepositoryFactory.ResolveSqLitePath(dataConfig);
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<SqlPlanRepository>();
        var repo = new SqlPlanRepository(dbPath, logger);
        repo.InitializeAsync().GetAwaiter().GetResult();
        return repo;
    }
    return null!;
});

// Register Birko.Data.EventSourcing event store (SQLite-backed specification audit trail)
builder.Services.AddSingleton<SqlEventStoreRepository>(sp =>
{
    var dataConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataStorageConfig>>().Value;
    if (dataConfig.DefaultBackend == StorageBackend.SqLite)
    {
        var dbPath = RepositoryFactory.ResolveSqLitePath(dataConfig);
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<SqlEventStoreRepository>();
        var repo = new SqlEventStoreRepository(dbPath, logger);
        repo.InitializeAsync().GetAwaiter().GetResult();
        return repo;
    }
    return null!;
});

// Register SpecificationEventService for recording specification change events
builder.Services.AddSingleton<SpecificationEventService>(sp =>
{
    var eventStore = sp.GetService<SqlEventStoreRepository>();
    if (eventStore == null) return null!;
    var logger = sp.GetRequiredService<ILogger<SpecificationEventService>>();
    return new SpecificationEventService(eventStore, logger);
});

// Register plan service for implementation plan persistence
builder.Services.AddSingleton<KoboldPlanService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<KoboldPlanService>>();
    var projectRepository = sp.GetRequiredService<IProjectRepository>();
    var planRepository = sp.GetService<SqlPlanRepository>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var debounceIntervalMs = config.Planning?.PlanSaveDebounceIntervalMs ?? 2500;
    return new KoboldPlanService(config.ProjectsPath ?? "./projects", logger, projectRepository, debounceIntervalMs, planRepository);
});

// Register shared planning context service for cross-agent coordination
builder.Services.AddSingleton<SharedPlanningContextService>(sp =>
{
    var planService = sp.GetRequiredService<KoboldPlanService>();
    var projectRepository = sp.GetRequiredService<IProjectRepository>();
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

// Register factories as singletons
// Register DrakeFactory before ProjectService since ProjectService depends on it
builder.Services.AddSingleton<DrakeFactory>(sp =>
{
    var koboldFactory = sp.GetRequiredService<KoboldFactory>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    var projectConfigService = sp.GetRequiredService<ProjectConfigurationService>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var gitService = sp.GetRequiredService<GitService>();
    var projectRepository = sp.GetRequiredService<IProjectRepository>();
    var circuitBreaker = sp.GetRequiredService<ProviderCircuitBreaker>();
    var sharedPlanningContext = sp.GetRequiredService<SharedPlanningContextService>();
    var taskRepository = sp.GetService<ITaskRepository>();
    var eventBus = sp.GetService<IEventBus>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var factory = new DrakeFactory(koboldFactory, providerConfigService, projectConfigService, config,
        loggerFactory, gitService, projectRepository, circuitBreaker, sharedPlanningContext, taskRepository, eventBus);

    // Wire feature completion notifications so users get notified when branches are ready for merge
    var notificationService = sp.GetRequiredService<ProjectNotificationService>();
    factory.OnFeatureBranchReady = (projectName, featureName, branchName) =>
    {
        notificationService.NotifyFeatureBranchReady(projectName, featureName, branchName);
    };

    // Wire escalation notifications to Dragon client
    factory.OnEscalation = (projectName, alert, resolution) =>
    {
        notificationService.Notify(projectName, "escalation",
            $"[{alert.Type}] Task {alert.TaskId?[..8]}: {alert.Summary}. Action: {resolution}",
            new Dictionary<string, string>
            {
                ["taskId"] = alert.TaskId ?? "",
                ["escalationType"] = alert.Type.ToString(),
                ["source"] = alert.Source.ToString(),
                ["resolution"] = resolution
            });
    };

    return factory;
});

builder.Services.AddSingleton<ProjectService>(sp =>
{
    var repository = sp.GetRequiredService<IProjectRepository>();
    var wyvernFactory = sp.GetRequiredService<WyvernFactory>();
    var logger = sp.GetRequiredService<ILogger<ProjectService>>();
    var gitService = sp.GetRequiredService<GitService>();
    var projectConfigService = sp.GetRequiredService<ProjectConfigurationService>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var drakeFactory = sp.GetRequiredService<DrakeFactory>();
    return new ProjectService(repository, wyvernFactory, logger, gitService, config, projectConfigService, drakeFactory);
});

// Register remaining factories
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
builder.Services.AddSingleton<ProjectNotificationService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ProjectNotificationService>>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    return new ProjectNotificationService(logger, config.ProjectsPath ?? "./projects");
});

// Register services (DragonRequestQueue must be registered first)
builder.Services.AddSingleton<WebSocketCommandHandler>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WebSocketCommandHandler>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    var dragonService = sp.GetRequiredService<DragonService>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    var projectRepository = sp.GetRequiredService<IProjectRepository>();
    var drakeFactory = sp.GetRequiredService<DrakeFactory>();
    var wyvernFactory = sp.GetRequiredService<WyvernFactory>();
    var dragonRequestQueue = sp.GetRequiredService<DragonRequestQueue>();
    return new WebSocketCommandHandler(logger, projectService, dragonService, providerConfigService, projectRepository, drakeFactory, wyvernFactory, dragonRequestQueue);
});
builder.Services.AddSingleton<WyrmService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WyrmService>>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var commandHandler = sp.GetRequiredService<WebSocketCommandHandler>();
    return new WyrmService(logger, providerConfigService, config, commandHandler);
});

// Register SQL history repository (null when not using SQLite)
builder.Services.AddSingleton<SqlHistoryRepository>(sp =>
{
    var dataConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataStorageConfig>>().Value;
    if (dataConfig.DefaultBackend == StorageBackend.SqLite)
    {
        var dbPath = RepositoryFactory.ResolveSqLitePath(dataConfig);
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<SqlHistoryRepository>();
        var repo = new SqlHistoryRepository(dbPath, logger);
        repo.InitializeAsync().GetAwaiter().GetResult();
        return repo;
    }
    return null!;
});

// Register Dragon request queue before DragonService
builder.Services.AddSingleton<DragonRequestQueue>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DragonRequestQueue>>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var maxConcurrent = config.Limits?.MaxConcurrentDragonRequests ?? 5;
    var timeout = config.Limits?.DragonRequestTimeoutSeconds ?? 300;
    return new DragonRequestQueue(logger, maxConcurrent, timeout);
});

builder.Services.AddSingleton<DragonService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DragonService>>();
    var providerConfigService = sp.GetRequiredService<ProviderConfigurationService>();
    var projectConfigService = sp.GetRequiredService<ProjectConfigurationService>();
    var projectService = sp.GetRequiredService<ProjectService>();
    var projectRepository = sp.GetRequiredService<IProjectRepository>();
    var gitService = sp.GetRequiredService<GitService>();
    var koboldFactory = sp.GetRequiredService<KoboldFactory>();
    var drakeFactory = sp.GetRequiredService<DrakeFactory>();
    var planService = sp.GetRequiredService<KoboldPlanService>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var maxConcurrent = config.Limits?.MaxConcurrentDragonRequests ?? 5;
    var notificationService = sp.GetRequiredService<ProjectNotificationService>();
    var historyRepository = sp.GetService<SqlHistoryRepository>();
    var specEventService = sp.GetService<SpecificationEventService>();
    return new DragonService(logger, providerConfigService, projectConfigService, projectService, projectRepository, gitService, config, koboldFactory, drakeFactory, planService, maxConcurrent, notificationService, historyRepository, specEventService);
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
    var notificationService = sp.GetRequiredService<ProjectNotificationService>();
    return new DrakeExecutionService(
        logger,
        projectService,
        drakeFactory,
        shutdownCoordinator,
        notificationService,
        executionIntervalSeconds: 30,
        maxKoboldIterations: maxIterations);
});

// Register Wyrm processing background service (New → WyrmAssigned, checks every 60 seconds)
builder.Services.AddHostedService<WyrmProcessingService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WyrmProcessingService>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    var wyrmFactory = sp.GetRequiredService<WyrmFactory>();
    var sharedPlanningContext = sp.GetRequiredService<SharedPlanningContextService>();
    return new WyrmProcessingService(logger, projectService, wyrmFactory, sharedPlanningContext, checkIntervalSeconds: 60);
});

// Register Wyvern processing background service (WyrmAssigned → Analyzed, checks every 60 seconds)
builder.Services.AddHostedService<WyvernProcessingService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WyvernProcessingService>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    return new WyvernProcessingService(logger, projectService, checkIntervalSeconds: 60);
});

// Register Wyvern Verification Service (AwaitingVerification → Verified, checks every 30 seconds)
builder.Services.AddHostedService<WyvernVerificationService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WyvernVerificationService>>();
    var projectService = sp.GetRequiredService<ProjectService>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new WyvernVerificationService(logger, projectService, configuration, checkIntervalSeconds: 30);
});

// Register Reasoning Monitor Service (detects stuck/stalled Kobolds, triggers escalations)
builder.Services.AddHostedService<ReasoningMonitorService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ReasoningMonitorService>>();
    var koboldFactory = sp.GetRequiredService<KoboldFactory>();
    var drakeFactory = sp.GetRequiredService<DrakeFactory>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var reflectionConfig = config.Reflection ?? new DraCode.KoboldLair.Models.Configuration.ReflectionConfiguration();
    return new ReasoningMonitorService(
        logger,
        koboldFactory,
        drakeFactory,
        reflectionConfig,
        monitorIntervalSeconds: reflectionConfig.MonitorIntervalSeconds);
});

// Register FailureRecoveryJob for DI resolution by JobExecutor
builder.Services.AddTransient<FailureRecoveryJob>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FailureRecoveryJob>>();
    var projectRepository = sp.GetRequiredService<IProjectRepository>();
    var projectService = sp.GetRequiredService<ProjectService>();
    var circuitBreaker = sp.GetRequiredService<ProviderCircuitBreaker>();
    var drakeFactory = sp.GetRequiredService<DrakeFactory>();
    return new FailureRecoveryJob(logger, projectRepository, projectService, circuitBreaker, drakeFactory, maxRetryAttempts: 5);
});

// Register Birko.BackgroundJobs infrastructure
var clock = new Birko.Time.SystemDateTimeProvider();
builder.Services.AddSingleton<IJobQueue>(new InMemoryJobQueue(clock));
builder.Services.AddSingleton<IJobExecutor>(sp =>
    new JobExecutor(type => sp.GetRequiredService(type)));
builder.Services.AddSingleton<JobDispatcher>(sp =>
    new JobDispatcher(sp.GetRequiredService<IJobQueue>(), clock));

// Register RecurringJobScheduler as hosted service with FailureRecoveryJob at 5-minute interval
builder.Services.AddHostedService(sp =>
{
    var scheduler = new RecurringJobScheduler(sp.GetRequiredService<IJobQueue>(), clock);
    scheduler.Register<FailureRecoveryJob>("failure-recovery", TimeSpan.FromMinutes(5));
    return new RecurringJobSchedulerHostedService(scheduler);
});

// Register BackgroundJobProcessor as hosted service to process enqueued jobs
builder.Services.AddHostedService(sp =>
{
    var processor = new BackgroundJobProcessor(
        sp.GetRequiredService<IJobQueue>(),
        sp.GetRequiredService<IJobExecutor>());
    return new BackgroundJobProcessorHostedService(processor);
});

// Register Birko.EventBus (in-process) and event handlers
builder.Services.AddEventBus();
builder.Services.AddEventHandler<TaskStatusChangedEvent, TaskStatusChangedHandler>();
builder.Services.AddEventHandler<KoboldLifecycleEvent, KoboldLifecycleHandler>();

// Register Birko.MessageQueue (InMemory for dev, MQTT for production)
{
    var koboldConfig = builder.Configuration.GetSection("KoboldLair").Get<KoboldLairConfiguration>() ?? new KoboldLairConfiguration();
    var messagingConfig = koboldConfig.Messaging ?? new MessagingConfiguration();

    if (messagingConfig.Enabled)
    {
        // Register InMemory message queue (default for dev)
        builder.Services.AddSingleton<IMessageQueue>(sp =>
        {
            var queue = new InMemoryMessageQueue();
            queue.ConnectAsync().GetAwaiter().GetResult();
            return queue;
        });
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IMessageQueue>().Producer);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IMessageQueue>().Consumer);

        // Register queue-based dispatcher
        builder.Services.AddSingleton<ITaskDispatcher>(sp =>
        {
            var producer = sp.GetRequiredService<IMessageProducer>();
            var logger = sp.GetRequiredService<ILogger<QueueTaskDispatcher>>();
            return new QueueTaskDispatcher(producer, messagingConfig.Queues.TaskAssignment, logger);
        });

        // Register task completion handler
        builder.Services.AddSingleton<TaskCompletionHandler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TaskCompletionHandler>>();
            var eventBus = sp.GetService<IEventBus>();
            return new TaskCompletionHandler(logger, eventBus);
        });
    }
    else
    {
        // Default: direct in-process dispatcher (no message queue)
        builder.Services.AddSingleton<ITaskDispatcher>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DirectTaskDispatcher>>();
            return new DirectTaskDispatcher(logger: logger);
        });
    }
}

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

// Auto-migrate JSON → SQLite on first startup when SQLite backend is enabled
using (var scope = app.Services.CreateScope())
{
    var dataConfig = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataStorageConfig>>().Value;
    var koboldConfig = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    dataConfig.ProjectsPath = koboldConfig.ProjectsPath ?? "./projects";

    if (dataConfig.DefaultBackend == StorageBackend.SqLite)
    {
        var dbPath = Path.IsPathRooted(dataConfig.SqLitePath)
            ? dataConfig.SqLitePath
            : Path.Combine(dataConfig.ProjectsPath, dataConfig.SqLitePath);

        var projectsJsonPath = Path.Combine(dataConfig.ProjectsPath, "projects.json");
        var migrationMarker = dbPath + ".migrated";

        // Only migrate if projects.json exists and we haven't migrated yet
        if (File.Exists(projectsJsonPath) && !File.Exists(migrationMarker))
        {
            logger.LogInformation("SQLite backend enabled — migrating existing JSON data...");
            try
            {
                var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
                var migration = await JsonToSqlMigration.CreateAsync(dataConfig, loggerFactory);
                var result = await migration.MigrateAsync();

                logger.LogInformation(
                    "Migration complete: {Projects} projects, {Tasks} tasks migrated. Errors: {Errors}",
                    result.ProjectsMigrated, result.TasksMigrated, result.Errors.Count);

                foreach (var error in result.Errors)
                {
                    logger.LogWarning("Migration error: {Error}", error);
                }

                // Write marker so we don't re-migrate on every restart
                await File.WriteAllTextAsync(migrationMarker,
                    $"Migrated at {DateTime.UtcNow:O} — {result.ProjectsMigrated} projects, {result.TasksMigrated} tasks");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "JSON → SQLite migration failed. Falling back to JSON storage.");
            }
        }
        else if (File.Exists(migrationMarker))
        {
            logger.LogDebug("SQLite migration already completed, skipping");
        }
    }
}

// Initialize circuit breaker persistence (uses same SQLite database)
{
    var dataConfig = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataStorageConfig>>().Value;
    if (dataConfig.DefaultBackend == StorageBackend.SqLite)
    {
        var cbLogger = app.Services.GetRequiredService<ILogger<Program>>();
        var circuitBreaker = app.Services.GetRequiredService<ProviderCircuitBreaker>();
        var dbPath = RepositoryFactory.ResolveSqLitePath(dataConfig);
        try
        {
            await circuitBreaker.InitializePersistenceAsync(dbPath);
            cbLogger.LogInformation("Circuit breaker state loaded from SQLite");
        }
        catch (Exception ex)
        {
            cbLogger.LogWarning(ex, "Failed to initialize circuit breaker persistence - using in-memory only");
        }
    }
}

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

    // Prune stale git worktrees left by previous crashes
    var gitService = scope.ServiceProvider.GetRequiredService<GitService>();
    foreach (var project in projectService.GetAllProjects())
    {
        try
        {
            // Resolve the git root: external SourcePath or KoboldLair project folder
            string? projectFolder = null;
            if (project.Metadata.TryGetValue("IsExistingProject", out var isExisting) &&
                isExisting == "true" &&
                project.Metadata.TryGetValue("SourcePath", out var sourcePath) &&
                Directory.Exists(sourcePath))
            {
                projectFolder = sourcePath;
            }
            else
            {
                // For new projects: resolve from specification path
                var specDir = Path.GetDirectoryName(
                    Path.IsPathRooted(project.Paths.Specification)
                        ? project.Paths.Specification
                        : Path.Combine(projectService.ProjectsPath, project.Paths.Specification));
                if (!string.IsNullOrEmpty(specDir) && Directory.Exists(specDir))
                    projectFolder = specDir;
            }

            if (!string.IsNullOrEmpty(projectFolder))
                await gitService.PruneStaleWorktreesAsync(projectFolder);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to prune worktrees for project {ProjectName}", project.Name);
        }
    }
    logger?.LogInformation("Stale worktree cleanup complete");
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

        // 5. Persist pending notifications
        logger.LogInformation("Persisting pending notifications on shutdown...");
        var notificationSvc = scope.ServiceProvider.GetRequiredService<ProjectNotificationService>();
        notificationSvc.PersistAll();
        logger.LogInformation("Notifications persisted successfully");

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

// Map JWT auth endpoints (login, refresh, logout)
{
    var jwtConfig = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtAuthenticationConfiguration>>().Value;
    if (jwtConfig.Enabled)
    {
        app.MapAuthEndpoints();
    }
}

// Enable WebSocket with keep-alive
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
};
app.UseWebSockets(webSocketOptions);

// WebSocket endpoint for Wyvern (project analysis) with authentication using Birko.Communication
app.MapWebSocket("/wyvern", async (webSocket, context) =>
{
    var wyrmService = context.RequestServices.GetRequiredService<WyrmService>();
    await wyrmService.HandleWebSocketAsync(webSocket);
}, requireAuthentication: true);

// WebSocket endpoint for Dragon (requirements gathering) with authentication using Birko.Communication
app.MapWebSocket("/dragon", async (webSocket, context) =>
{
    // Extract sessionId from query string for session resumption
    var sessionId = context.Request.Query["sessionId"].FirstOrDefault();

    var dragonService = context.RequestServices.GetRequiredService<DragonService>();
    await dragonService.HandleWebSocketAsync(webSocket, sessionId);
}, requireAuthentication: true);

// Health check endpoint
app.MapGet("/", () => new { status = "running", endpoints = new[] { "/wyvern", "/dragon" } });

app.Run();
