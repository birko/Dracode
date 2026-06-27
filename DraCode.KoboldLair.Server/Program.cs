using Birko.Communication.WebSocket.Middleware;
using Birko.AI.Resilience.Services;
using Birko.Communication.WebSocket.Services;
using Birko.Security;
using Birko.Security.AspNetCore;
using Birko.Security.Authorization;
using Birko.Security.Jwt;
using Birko.Security.OAuth.Server;
using Birko.Security.OAuth.Server.Stores;
using Birko.Security.Hashing;
using System.Security.Claims;
using Birko.AI;
using Birko.AI.Agents;
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
using DraCode.KoboldLair.Security;
using DraCode.KoboldLair.Services;
using Birko.BackgroundJobs;
using Birko.BackgroundJobs.Processing;
using DraCode.KoboldLair.Server.Jobs;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Server.Api;
using Scalar.AspNetCore;
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
builder.Services.AddSingleton<RefreshTokenStore>();

// GitHub federation (TASK-033 / FEATURE-019 D1, D11, D13). Bound + registered unconditionally so
// the services are inert when disabled; the endpoints are only mapped when Enabled (see below).
builder.Services.Configure<GitHubFederationConfiguration>(
    builder.Configuration.GetSection("Authentication:GitHub"));
builder.Services.AddSingleton<OAuthStateStore>();
builder.Services.AddHttpClient<IGitHubUserInfoClient, GitHubUserInfoClient>();
builder.Services.AddHttpClient<IGitHubTokenExchanger, GitHubTokenExchanger>();
builder.Services.AddScoped<GitHubFederationService>();

// Daemon loopback bypass options (TASK-032). Bound from Authentication:Daemon; off by default.
builder.Services.Configure<DaemonAuthOptions>(
    builder.Configuration.GetSection("Authentication:Daemon"));

// Birko JWT Bearer validation services (TASK-032). Registered UNCONDITIONALLY so the authorization
// pipeline (AddAuthorization) is always available; the middleware itself is added only when
// Authentication:Jwt:Enabled (see the pipeline section), preserving auth-disabled-by-default behavior.
// Registering the services when disabled is inert — with no UseAuthentication/UseAuthorization in the
// pipeline, no route is enforced. (Gating registration on a build-time config read instead diverges
// from the runtime IOptions read under WebApplicationFactory — one decision point avoids that.)
// AddBirkoSecurity wires AddAuthentication + AddJwtBearer (reads ?token= from the query for SSE/WS) +
// AddAuthorization, plus claims-based ICurrentUser/IPermissionChecker used by PermissionEndpointFilter.
// The JWT secret/issuer/audience are reused so login- and OAuth-issued tokens validate by construction.
{
    var jwtCfgForSecurity = builder.Configuration
        .GetSection("Authentication:Jwt").Get<JwtAuthenticationConfiguration>() ?? new JwtAuthenticationConfiguration();
    var securitySecret = jwtCfgForSecurity.ResolveSecret();
    if (string.IsNullOrEmpty(securitySecret))
        securitySecret = "KoboldLair-Development-Secret-Key-Do-Not-Use-In-Production!";

    builder.Services.AddBirkoSecurity(options =>
    {
        options.Jwt.Secret = securitySecret;
        options.Jwt.Issuer = jwtCfgForSecurity.Issuer;
        options.Jwt.Audience = jwtCfgForSecurity.Audience;
        options.Jwt.ExpirationMinutes = jwtCfgForSecurity.ExpirationMinutes;
        options.Jwt.RefreshExpirationDays = jwtCfgForSecurity.RefreshExpirationDays;
        // Service-account tokens carry their granted scopes in the "scope" claim; interactive login
        // tokens emit the same claim (AuthEndpoints). Read permissions from it so PermissionEndpointFilter
        // enforces both. NOTE: ClaimsCurrentUser splits the claim on commas — a single OAuth scope works,
        // but a space-delimited multi-scope token is a known limitation (follow-up).
        options.Jwt.Claims.PermissionClaim = "scope";
    });
}

// Register Birko OAuth 2.1 authorization server (TASK-030). Endpoints are mapped only when
// Authentication:OAuth:Enabled; the server + stores register unconditionally (harmless DI singletons).
// Issuer/Audience/Secret reuse the JWT config so OAuth-issued tokens validate under the JWT bearer
// middleware (TASK-032) by construction.
builder.Services.Configure<OAuthServerConfiguration>(
    builder.Configuration.GetSection("Authentication:OAuth"));
// OAuth stores — singletons, ONE shared instance each (the device-code flow creates a record in one
// request and reads/mutates it across later requests). SQLite-backed when KoboldLair:Data selects the
// SqLite backend (TASK-031), else in-memory. The four scalar models persist directly via
// AsyncSQLiteStore<T>; OAuthClient uses an entity+JSON-column mapper for its List<string> properties.
var oauthDataConfig = builder.Configuration.GetSection("KoboldLair:Data").Get<DataStorageConfig>()
    ?? new DataStorageConfig();
oauthDataConfig.ProjectsPath =
    builder.Configuration.GetSection("KoboldLair").Get<KoboldLairConfiguration>()?.ProjectsPath
    ?? oauthDataConfig.ProjectsPath;
var oauthUseSqlite = oauthDataConfig.DefaultBackend == StorageBackend.SqLite;
var oauthDbPath = oauthUseSqlite ? RepositoryFactory.ResolveSqLitePath(oauthDataConfig) : null;
builder.Services.AddOAuthServerStores(oauthUseSqlite, oauthDbPath);
builder.Services.AddSingleton<OAuthServer>(sp =>
{
    var jwt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtAuthenticationConfiguration>>().Value;
    var oauth = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OAuthServerConfiguration>>().Value;
    var secret = jwt.ResolveSecret();
    if (string.IsNullOrEmpty(secret))
        secret = "KoboldLair-Development-Secret-Key-Do-Not-Use-In-Production!";
    var settings = new OAuthServerSettings
    {
        Issuer = jwt.Issuer,
        AccessTokenLifetimeSeconds = oauth.AccessTokenLifetimeSeconds,
        RefreshTokenLifetimeSeconds = oauth.RefreshTokenLifetimeSeconds,
        AuthorizationCodeLifetimeSeconds = oauth.AuthorizationCodeLifetimeSeconds,
        DeviceCodeLifetimeSeconds = oauth.DeviceCodeLifetimeSeconds,
        DeviceCodePollingIntervalSeconds = oauth.DeviceCodePollingIntervalSeconds,
        RotateRefreshTokens = oauth.RotateRefreshTokens,
        RequirePkceForPublicClients = oauth.RequirePkceForPublicClients
    };
    var tokenOptions = new TokenOptions
    {
        Secret = secret,
        Issuer = jwt.Issuer,
        Audience = jwt.Audience,
        ExpirationMinutes = jwt.ExpirationMinutes,
        RefreshExpirationDays = jwt.RefreshExpirationDays
    };
    return new OAuthServer(
        settings,
        sp.GetRequiredService<ITokenProvider>(),
        tokenOptions,
        sp.GetRequiredService<IOAuthClientStore>(),
        sp.GetRequiredService<IAuthorizationCodeStore>(),
        sp.GetRequiredService<IRefreshTokenStore>(),
        sp.GetRequiredService<IDeviceCodeStore>(),
        sp.GetRequiredService<IConsentStore>(),
        oauth.DeviceVerificationUri,
        new Birko.Time.SystemDateTimeProvider());
});

// Service-account client_credentials token issuer (TASK-035 / FEATURE-019 D14). Mints
// sub="service:<name>" + comma-joined permission scopes via the shared ITokenProvider; the
// /token endpoint routes client_credentials here. Inert unless OAuth is enabled + mapped.
builder.Services.AddSingleton<ServiceAccountTokenIssuer>(sp => new ServiceAccountTokenIssuer(
    sp.GetRequiredService<IOAuthClientStore>(),
    sp.GetRequiredService<ITokenProvider>()));

// Register Birko.Validation validators
builder.Services.AddSingleton<IValidator<Specification>, SpecificationValidator>();
builder.Services.AddSingleton<IValidator<Feature>, FeatureValidator>();
builder.Services.AddSingleton<IValidator<AgentsConfig>, ProjectConfigValidator>();

// Configure KoboldLair settings (providers, defaults, limits - all in one place)
builder.Services.Configure<KoboldLairConfiguration>(
    builder.Configuration.GetSection("KoboldLair"));

// Register provider configuration service (must be registered before ProjectConfigurationService).
// TASK-073: when a provider-config master key is configured (config or KOBOLDLAIR_MASTER_KEY env), run in
// DB-backed mode — providers + models + agent assignments in the database, API keys encrypted at rest. The
// master key is resolved through Birko's ISecretProvider (Phase 1 = config; Phase 2 = Vault, a DI swap here
// only). Without a master key, stay in legacy appsettings/user-settings/env mode (no behaviour change).
// Registered lazily (only constructed when something resolves them) and decided from IConfiguration at
// construction time — NOT from builder.Configuration pre-Build, so test hosts that inject config via
// WebApplicationFactory are honoured. In legacy mode the repo/cipher are never resolved (the service goes
// legacy and the admin endpoints aren't mapped), so these factories never run.
static string? ResolveProviderMasterKey(IConfiguration cfg) =>
    cfg["KoboldLair:ProviderConfig:MasterKey"] ?? Environment.GetEnvironmentVariable("KOBOLDLAIR_MASTER_KEY");

builder.Services.AddSingleton<SqlProviderConfigRepository>(sp =>
{
    var dataConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataStorageConfig>>().Value;
    var dbPath = RepositoryFactory.ResolveSqLitePath(dataConfig);
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<SqlProviderConfigRepository>();
    var repo = new SqlProviderConfigRepository(dbPath, logger);
    repo.InitializeAsync().GetAwaiter().GetResult();
    return repo;
});

// ISecretProvider is constructed inline (not container-registered) to avoid colliding with other
// ISecretProvider registrations (e.g. OAuth/Vault). Phase 2 swaps this one line for a Vault provider.
builder.Services.AddSingleton<ProviderKeyCipher>(sp => new ProviderKeyCipher(
    new ConfigSecretProvider(new Dictionary<string, string>
    {
        [ProviderKeyCipher.MasterKeySecretName] = ResolveProviderMasterKey(sp.GetRequiredService<IConfiguration>()) ?? ""
    })));

builder.Services.AddSingleton<ProviderConfigurationService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ProviderConfigurationService>>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>();
    var masterKey = ResolveProviderMasterKey(sp.GetRequiredService<IConfiguration>());
    if (string.IsNullOrWhiteSpace(masterKey))
        return new ProviderConfigurationService(logger, options); // legacy: appsettings + user-settings + env

    var svc = new ProviderConfigurationService(
        logger, options, "./user-settings.json",
        sp.GetRequiredService<SqlProviderConfigRepository>(),
        sp.GetRequiredService<ProviderKeyCipher>());
    svc.InitializeAsync().GetAwaiter().GetResult();
    return svc;
});

// Register project configuration service (depends on ProviderConfigurationService for defaults)
builder.Services.AddSingleton<ProjectConfigurationService>();

// Canonical spec/feature persistence (TASK-043) — shared by the Dragon council tools and the
// /api/v1 resource endpoints so the two surfaces can't drift on the on-disk spec layout.
builder.Services.AddSingleton<SpecificationService>(sp =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    return new SpecificationService(config.ProjectsPath ?? "./projects");
});

// Register provider circuit breaker for failure tracking
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ProviderCircuitBreaker");
    return new ProviderCircuitBreaker(logger: logger);
});

// Register rate limiter
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ProviderRateLimiter");
    return new ProviderRateLimiter(config.RateLimiting, logger);
});

// Register SQL usage repository (null when not using SQLite)
builder.Services.AddSingleton<SqlUsageRepository>(sp =>
{
    var dataConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataStorageConfig>>().Value;
    if (dataConfig.DefaultBackend == StorageBackend.SqLite)
    {
        var dbPath = RepositoryFactory.ResolveSqLitePath(dataConfig);
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<SqlUsageRepository>();
        var repo = new SqlUsageRepository(dbPath, logger);
        repo.InitializeAsync().GetAwaiter().GetResult();
        return repo;
    }
    return null!;
});

// Register cost tracking service
builder.Services.AddSingleton<CostTrackingService>(sp =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    var logger = sp.GetRequiredService<ILogger<CostTrackingService>>();
    var usageRepo = sp.GetService<SqlUsageRepository>();
    return new CostTrackingService(config.CostTracking, logger, usageRepo);
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

// Register IUserRepository - uses SQLite when configured, null otherwise (TASK-034 / FEATURE-019).
// Keyed by the stable `sub`; populated lazily when a caller first creates a project.
builder.Services.AddSingleton<IUserRepository>(sp =>
{
    var dataConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataStorageConfig>>().Value;
    var koboldConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KoboldLairConfiguration>>().Value;
    dataConfig.ProjectsPath = koboldConfig.ProjectsPath ?? "./projects";

    if (dataConfig.DefaultBackend == StorageBackend.SqLite)
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var repo = RepositoryFactory.CreateUserRepositoryAsync(dataConfig, loggerFactory).GetAwaiter().GetResult();
        return repo;
    }

    return null!; // No user persistence without SQLite; ownership still keyed on `sub`
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
// Per-run telemetry stream (TASK-037) — shared foundation for the /kobold WS (TASK-038) and SSE
// (TASK-045) transports. Singleton; Kobolds publish to it, transports subscribe by runId.
builder.Services.AddSingleton<KoboldRunEventSource>();

// Queryable run directory (TASK-044) — folds the live event stream into per-run status that outlives the
// run, so REST/SSE callers can poll a runId after completion. Shared by the /kobold WS + /api/v1/runs.
builder.Services.AddSingleton<RunRegistry>();

// /kobold WebSocket endpoint (TASK-038): mode-handler strategies + the protocol service that
// subscribes to KoboldRunEventSource and relays kobold_* frames. Ad-hoc/project mechanics are TASK-039/040.
builder.Services.AddSingleton<IKoboldRunModeHandler, AdHocRunModeHandler>();
builder.Services.AddSingleton<IKoboldRunModeHandler, ProjectRunModeHandler>();
builder.Services.AddSingleton<KoboldEndpointService>();

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

    var rateLimiter = sp.GetRequiredService<ProviderRateLimiter>();
    var costTracker = sp.GetRequiredService<CostTrackingService>();
    var runEventSource = sp.GetRequiredService<KoboldRunEventSource>();
    return new KoboldFactory(projectConfigService, loggerFactory, config, getMaxParallel,
        rateLimiter: rateLimiter, costTracker: costTracker, runEventSource: runEventSource);
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
    var costTracker = sp.GetRequiredService<CostTrackingService>();
    var rateLimiter = sp.GetRequiredService<ProviderRateLimiter>();
    var planService = sp.GetRequiredService<KoboldPlanService>();
    return new WebSocketCommandHandler(logger, projectService, dragonService, providerConfigService, projectRepository, drakeFactory, wyvernFactory, dragonRequestQueue, costTracker, rateLimiter, planService);
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
    var userRepository = sp.GetService<IUserRepository>();
    return new DragonService(logger, providerConfigService, projectConfigService, projectService, projectRepository, gitService, config, koboldFactory, drakeFactory, planService, maxConcurrent, notificationService, historyRepository, specEventService, userRepository);
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

// /api/v1 REST facade (TASK-042): built-in OpenAPI document generation, and camelCase JSON for
// minimal-API endpoints (matches the JS client conventions). The explicit snake_case options the
// OAuth/Auth endpoints pass to Results.Json are unaffected — they win over these global defaults.
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
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
        // Note: Circuit breaker persistence is now handled via ICircuitBreakerStore in constructor
        // For now using in-memory circuit breaker state
        cbLogger.LogInformation("Circuit breaker using in-memory state persistence");
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

// JWT validation middleware (TASK-032). Enabled only when Authentication:Jwt:Enabled; otherwise the
// server stays auth-free (backward compatible). UseAuthentication is always safe to run (it only
// populates HttpContext.User from a valid bearer/?token= when present); UseAuthorization is what
// enforces .RequireAuthorization()/.RequirePermission() metadata.
var jwtRuntimeEnabled = app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtAuthenticationConfiguration>>().Value.Enabled;
if (jwtRuntimeEnabled)
{
    app.UseAuthentication();

    // Daemon loopback bypass: when configured AND every bind is loopback, trust the OS user and run
    // requests as a synthetic principal with the "*" permission so both RequireAuthorization and
    // PermissionEndpointFilter pass. Any non-loopback bind enforces JWT.
    var daemonOpts = app.Services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<DaemonAuthOptions>>().Value;
    var bindUrls = app.Urls.Count > 0
        ? app.Urls.AsEnumerable()
        : (app.Configuration["urls"] ?? app.Configuration["ASPNETCORE_URLS"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (DaemonLoopback.ShouldBypass(daemonOpts.LoopbackBypass, bindUrls))
    {
        app.Logger.LogWarning(
            "⚠ Daemon loopback bypass ACTIVE — JWT validation is skipped (all binds are loopback). " +
            "Never enable LoopbackBypass on a non-loopback bind.");
        app.Use(async (ctx, next) =>
        {
            if (ctx.User?.Identity?.IsAuthenticated != true)
            {
                ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("scope", "*"),
                        new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString())
                    ],
                    authenticationType: "DaemonLoopback"));
            }
            await next();
        });
    }

    app.UseAuthorization();
}

// Map JWT auth endpoints (login, refresh, logout)
if (jwtRuntimeEnabled)
{
    app.MapAuthEndpoints();

    // The /api/v1 REST facade group (TASK-042 / STORY-016) — group-wide RequireAuthorization +
    // whoami + the agents/active stub. Resource endpoints (TASK-043/044/046) extend the same group.
    // Mapped here (under jwtRuntimeEnabled) because RequireAuthorization only bites once
    // UseAuthorization is in the pipeline, which is itself gated on JWT being enabled.
    var apiV1 = app.MapApiV1();
    apiV1.MapRunEndpoints(); // POST/GET /api/v1/runs (TASK-044)
    // Admin /api/v1/providers CRUD (TASK-073) — only in DB mode. Read post-build so test-host config is seen.
    if (!string.IsNullOrWhiteSpace(ResolveProviderMasterKey(app.Configuration)))
        apiV1.MapProviderEndpoints();
}

// OpenAPI document + Scalar docs UI for the /api/v1 facade (TASK-042). Mapped anonymously (NOT under
// the authed group) so a browser can open the docs / fetch the spec without a token.
app.MapOpenApi("/api/v1/openapi.json");
app.MapScalarApiReference("/api/v1/docs", o => o.WithOpenApiRoutePattern("/api/v1/openapi.json"));

// Map OAuth 2.1 authorization-server endpoints (TASK-030) when enabled.
{
    var oauthConfig = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OAuthServerConfiguration>>().Value;
    if (oauthConfig.Enabled)
    {
        // OAuth issues JWTs that the bearer middleware validates, and its admin routes require the
        // authorization pipeline. Enabling OAuth without JWT would 500 on those routes — fail fast
        // with a clear message instead.
        if (!jwtRuntimeEnabled)
            throw new InvalidOperationException(
                "Authentication:OAuth:Enabled requires Authentication:Jwt:Enabled — the OAuth server " +
                "issues JWTs validated by the JWT bearer middleware, and its /device/approve and " +
                "/register routes require the authorization pipeline (TASK-032).");

        app.MapOAuthEndpoints(oauthConfig.AllowDynamicRegistration);
    }
}

// Map GitHub federation endpoints (TASK-033) when enabled.
{
    var gitHubConfig = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<GitHubFederationConfiguration>>().Value;
    if (gitHubConfig.Enabled)
    {
        // Federation mints DraCode JWTs validated by the bearer middleware, so JWT must be on.
        // Fail fast with a clear message rather than 500 on the callback (mirrors the OAuth gate).
        if (!jwtRuntimeEnabled)
            throw new InvalidOperationException(
                "Authentication:GitHub:Enabled requires Authentication:Jwt:Enabled — GitHub federation " +
                "mints JWTs validated by the JWT bearer middleware (TASK-033 / FEATURE-019 D11).");

        app.MapGitHubAuthEndpoints();
    }
}

// Enable WebSocket with keep-alive
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
};
app.UseWebSockets(webSocketOptions);

// WebSocket endpoints for Wyvern + Dragon. Authenticated via the JWT bearer pipeline
// (UseAuthentication populates context.User from the `?token=` query for WS upgrades) instead
// of the legacy shared-token validator (TASK-034 / FEATURE-019 D8). The full teardown of the
// legacy WebSocketAuthenticationService stays TASK-036.
//
// jwtCaptured snapshots whether JWT is enabled; when disabled (auth-off local dev) the connection
// is treated as the loopback single-user owner. When enabled, an unauthenticated upgrade is
// rejected with 401 *before* the socket is accepted.
var jwtCaptured = jwtRuntimeEnabled;

// Resolves caller identity from the JWT-populated principal. Returns the raw `sub`
// (NameIdentifier), admin flag (view_all/* scope), and best-effort name/email.
static (string? sub, bool isAdmin, string? name, string? email) ResolveCaller(HttpContext context)
{
    var user = context.User;
    if (user?.Identity?.IsAuthenticated != true)
        return (null, true, null, null); // auth disabled → local single-user owner + admin
    var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var isAdmin = user.FindAll("scope")
        .SelectMany(c => c.Value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        .Any(s => s == "*" || s == KoboldLairPermissionChecker.ViewAll);
    var name = user.FindFirst("name")?.Value ?? user.FindFirst(ClaimTypes.Name)?.Value;
    var email = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;
    return (sub, isAdmin, name, email);
}

app.Map("/wyvern", async (HttpContext context) =>
{
    if (jwtCaptured && context.User?.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    var wyrmService = context.RequestServices.GetRequiredService<WyrmService>();
    await wyrmService.HandleWebSocketAsync(webSocket);
});

app.Map("/dragon", async (HttpContext context) =>
{
    if (jwtCaptured && context.User?.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    var sessionId = context.Request.Query["sessionId"].FirstOrDefault();
    var caller = ResolveCaller(context);
    var dragonService = context.RequestServices.GetRequiredService<DragonService>();
    await dragonService.HandleWebSocketAsync(webSocket, sessionId, caller.sub, caller.isAdmin, caller.name, caller.email);
});

app.Map("/kobold", async (HttpContext context) =>
{
    if (jwtCaptured && context.User?.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    var caller = ResolveCaller(context);
    var endpointService = context.RequestServices.GetRequiredService<KoboldEndpointService>();
    await endpointService.HandleWebSocketAsync(
        webSocket,
        new KoboldCaller(caller.sub, caller.isAdmin, caller.name, caller.email),
        context.RequestAborted);
});

// Health check endpoint
app.MapGet("/", () => new { status = "running", endpoints = new[] { "/wyvern", "/dragon", "/kobold" } });

app.Run();

// Exposes the implicit Program entry point to the test host (WebApplicationFactory<Program>, TASK-032).
public partial class Program { }
