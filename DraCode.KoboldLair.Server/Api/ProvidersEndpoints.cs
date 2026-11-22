using System.Text.Json;
using Birko.AI.Models;
using Birko.AI.Tools;
using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Security;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// Admin REST surface for DB-backed provider configuration (TASK-073). All routes are gated by
/// <see cref="KoboldLairPermissionChecker.ManageConfig"/> and only mapped when provider-config DB mode is on.
/// API keys are write-only — a provider view exposes <c>hasKey</c>, never the ciphertext or plaintext.
/// Every write persists through the repository and then calls <see cref="ProviderConfigurationService.ReloadAsync"/>
/// so the change takes effect on the next agent run without a restart.
/// </summary>
public static class ProvidersEndpoints
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static RouteGroupBuilder MapProviderEndpoints(this RouteGroupBuilder api)
    {
        // --- providers -------------------------------------------------------------------------

        api.MapGet("/providers", async (SqlProviderConfigRepository repo) =>
            {
                var views = new List<object>();
                foreach (var p in await repo.GetAllProvidersAsync())
                {
                    var models = await repo.GetModelsAsync(p.Name);
                    views.Add(ToView(p, models));
                }
                return Results.Ok(views);
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        api.MapGet("/providers/{name}", async (string name, SqlProviderConfigRepository repo) =>
            {
                var p = await repo.GetProviderAsync(name);
                return p is null ? Results.NotFound() : Results.Ok(ToView(p, await repo.GetModelsAsync(name)));
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        // Create or update a provider's metadata (NOT the key — that's the /key route). Preserves the key.
        api.MapPost("/providers", (ProviderUpsertDto dto, SqlProviderConfigRepository repo, ProviderConfigurationService svc) =>
                UpsertAsync(dto.Name, dto, repo, svc))
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        // The same upsert keyed by the route (TASK-161): what the web Settings screen's edit sends (Birko Shell's
        // CRUD page updates with PUT {resource}/{id}). The name in the route wins over one in the body.
        api.MapPut("/providers/{name}", (string name, ProviderUpsertDto dto, SqlProviderConfigRepository repo, ProviderConfigurationService svc) =>
                UpsertAsync(name, dto, repo, svc))
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        // Validate (TASK-161): the configuration check, then one tiny live request to the provider, so a wrong key or
        // URL fails with the provider's own message. Answers 200 { valid, message } either way; 404 for an unknown one.
        api.MapPost("/providers/{name}/validate", async (string name, SqlProviderConfigRepository repo,
                ProviderConfigurationService svc, ILoggerFactory loggers, CancellationToken ct) =>
            {
                if (await repo.GetProviderAsync(name) is null) return Results.NotFound();
                var (valid, message) = await ValidateAsync(name, svc, loggers.CreateLogger("ProviderValidation"), ct);
                return Results.Ok(new { name, valid, message });
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        // Write-only API key. Empty/null clears it. Never returned by any GET.
        api.MapPatch("/providers/{name}/key", async (string name, SetKeyDto dto, SqlProviderConfigRepository repo, ProviderKeyCipher cipher, ProviderConfigurationService svc) =>
            {
                if (await repo.GetProviderAsync(name) is null) return Results.NotFound();
                await repo.SetApiKeyCiphertextAsync(name, string.IsNullOrEmpty(dto.ApiKey) ? null : cipher.Encrypt(dto.ApiKey));
                await svc.ReloadAsync();
                return Results.NoContent();
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        api.MapDelete("/providers/{name}", async (string name, SqlProviderConfigRepository repo, ProviderConfigurationService svc) =>
            {
                if (await repo.GetProviderAsync(name) is null) return Results.NotFound();
                try
                {
                    await svc.DeleteProviderAsync(name);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Conflict(new { error = ex.Message });
                }
                return Results.NoContent();
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        // --- models (a provider's switchable set) ----------------------------------------------

        api.MapPost("/providers/{name}/models", async (string name, ModelDto dto, SqlProviderConfigRepository repo, ProviderConfigurationService svc) =>
            {
                if (await repo.GetProviderAsync(name) is null) return Results.NotFound();
                if (string.IsNullOrWhiteSpace(dto.ModelId)) return Results.BadRequest(new { error = "modelId is required" });

                await repo.UpsertModelAsync(new ProviderModelEntity
                {
                    ProviderName = name,
                    ModelId = dto.ModelId,
                    DisplayName = dto.DisplayName,
                    Enabled = dto.Enabled ?? true,
                    SortOrder = dto.SortOrder ?? 0,
                    Reasoning = dto.Reasoning ?? false,
                    ContextWindow = dto.ContextWindow ?? 0,
                    InputModalities = dto.InputModalities
                });
                await svc.ReloadAsync();
                return Results.Ok(new { name, model = dto.ModelId });
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        api.MapDelete("/providers/{name}/models/{modelId}", async (string name, string modelId, SqlProviderConfigRepository repo, ProviderConfigurationService svc) =>
            {
                await repo.DeleteModelAsync(name, modelId);
                await svc.ReloadAsync();
                return Results.NoContent();
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        // --- agent → provider/model assignments ------------------------------------------------

        // The agent types a per-Kobold-type override can name (TASK-192): every registered agent but the orchestrators,
        // which have their own settings (dragon, wyvern, wyrm, kobold).
        api.MapGet("/providers/agent-types", () =>
            {
                Birko.AI.Agents.AgentRegistration.RegisterAll();
                var orchestrators = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dragon", "wyvern", "wyrm", "kobold" };
                return Results.Ok(Birko.AI.Factories.AgentFactory.GetRegisteredAgentTypes()
                    .Where(t => !orchestrators.Contains(t))
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .ToList());
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        api.MapGet("/providers/settings", (ProviderConfigurationService svc) =>
                Results.Ok(svc.GetUserSettings()))
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        // Default provider — a DB flag (no appsettings fallback in DB mode).
        api.MapGet("/providers/default", (ProviderConfigurationService svc) =>
                Results.Ok(new { defaultProvider = svc.GetDefaultProvider() }))
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        api.MapPut("/providers/default", (DefaultProviderDto dto, ProviderConfigurationService svc) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Provider))
                    return Results.BadRequest(new { error = "provider is required" });
                try
                {
                    svc.SetDefaultProvider(dto.Provider);
                    return Results.NoContent();
                }
                catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        // Orchestrator roles (dragon/wyvern/wyrm/kobold): provider required, validated for compatibility.
        api.MapPut("/providers/settings/agent", (AgentSettingDto dto, ProviderConfigurationService svc) =>
            {
                if (string.IsNullOrWhiteSpace(dto.AgentType) || string.IsNullOrWhiteSpace(dto.Provider))
                    return Results.BadRequest(new { error = "agentType and provider are required" });
                try
                {
                    svc.SetProviderForAgent(dto.AgentType, dto.Provider!, dto.Model);
                    return Results.NoContent();
                }
                catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        // Per-Kobold-type override; null provider+model clears the override.
        api.MapPut("/providers/settings/kobold", (AgentSettingDto dto, ProviderConfigurationService svc) =>
            {
                if (string.IsNullOrWhiteSpace(dto.AgentType))
                    return Results.BadRequest(new { error = "agentType is required" });
                try
                {
                    svc.SetProviderForKoboldAgentType(dto.AgentType, dto.Provider, dto.Model);
                    return Results.NoContent();
                }
                catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        return api;
    }

    private static async Task<IResult> UpsertAsync(string name, ProviderUpsertDto dto, SqlProviderConfigRepository repo,
        ProviderConfigurationService svc)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dto.Type))
            return Results.BadRequest(new { error = "name and type are required" });

        // The runtime reads the provider's base URL from its configuration, not the BaseUrl column (TASK-161)
        var configuration = new Dictionary<string, string>(dto.Configuration ?? new Dictionary<string, string>());
        if (!string.IsNullOrWhiteSpace(dto.BaseUrl))
            configuration["baseUrl"] = dto.BaseUrl;

        await repo.UpsertProviderAsync(new ProviderConfigEntity
        {
            Name = name,
            DisplayName = dto.DisplayName,
            Type = dto.Type,
            Enabled = dto.Enabled ?? true,
            BaseUrl = dto.BaseUrl,
            RequiresApiKey = dto.RequiresApiKey ?? true,
            DefaultModel = dto.DefaultModel,
            CompatibleAgentsJson = JsonSerializer.Serialize(dto.CompatibleAgents ?? new List<string>()),
            ConfigurationJson = JsonSerializer.Serialize(configuration)
        });
        await svc.ReloadAsync();
        return Results.Ok(new { name });
    }

    /// <summary>How long the live validation request may take.</summary>
    internal static TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The configuration check (<see cref="ProviderConfigurationService.ValidateProvider"/>: exists, enabled, key set when
    /// required), then one short request through the provider itself. Any failure comes back as a message the person can
    /// read: the provider's own error, a timeout, or the exception's message.
    /// </summary>
    internal static async Task<(bool Valid, string Message)> ValidateAsync(string name, ProviderConfigurationService svc,
        ILogger logger, CancellationToken ct)
    {
        var (configured, configMessage) = svc.ValidateProvider(name);
        if (!configured)
            return (false, configMessage);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ValidationTimeout);
        try
        {
            var (type, config, _) = svc.GetProviderSettingsByName(name);
            var llm = DraCode.KoboldLair.Agents.KoboldLairAgentFactory.CreateLlmProvider(type, config, logger: logger);
            var response = await llm.SendMessageAsync(
                [new Message { Role = "user", Content = "Reply with the single word OK." }],
                new List<Tool>(), "You are a connectivity check. Reply with the single word OK.", timeout.Token);
            if (string.Equals(response.StopReason, "error", StringComparison.OrdinalIgnoreCase))
                return (false, $"The provider answered with an error: {response.ErrorMessage ?? "no details"}");
            var model = config.TryGetValue("model", out var m) && !string.IsNullOrEmpty(m) ? $" (model {m})" : "";
            return (true, $"Connected: the provider answered{model}.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (false, $"The provider did not answer within {ValidationTimeout.TotalSeconds:0} s.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogInformation(ex, "Provider {Provider} failed validation", name);
            return (false, $"Could not reach the provider: {ex.Message}");
        }
    }

    private static object ToView(ProviderConfigEntity p, IReadOnlyList<ProviderModelEntity> models) => new
    {
        name = p.Name,
        displayName = p.DisplayName,
        type = p.Type,
        enabled = p.Enabled,
        baseUrl = p.BaseUrl,
        requiresApiKey = p.RequiresApiKey,
        defaultModel = p.DefaultModel,
        compatibleAgents = SafeList(p.CompatibleAgentsJson),
        hasKey = !string.IsNullOrEmpty(p.ApiKeyCiphertext),
        models = models.Select(m => new
        {
            modelId = m.ModelId, displayName = m.DisplayName, enabled = m.Enabled, sortOrder = m.SortOrder,
            reasoning = m.Reasoning, contextWindow = m.ContextWindow, inputModalities = m.InputModalities
        })
    };

    private static List<string> SafeList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json, Json) ?? new(); }
        catch { return new(); }
    }

    public sealed record ProviderUpsertDto(
        string Name, string Type, string? DisplayName, bool? Enabled, string? BaseUrl,
        bool? RequiresApiKey, string? DefaultModel, List<string>? CompatibleAgents,
        Dictionary<string, string>? Configuration);

    public sealed record SetKeyDto(string? ApiKey);

    public sealed record ModelDto(string ModelId, string? DisplayName, bool? Enabled, int? SortOrder,
        bool? Reasoning, int? ContextWindow, string? InputModalities);

    public sealed record AgentSettingDto(string AgentType, string? Provider, string? Model);

    public sealed record DefaultProviderDto(string Provider);
}
