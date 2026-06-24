using System.Text.Json;
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
        api.MapPost("/providers", async (ProviderUpsertDto dto, SqlProviderConfigRepository repo, ProviderConfigurationService svc) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Type))
                    return Results.BadRequest(new { error = "name and type are required" });

                await repo.UpsertProviderAsync(new ProviderConfigEntity
                {
                    Name = dto.Name,
                    DisplayName = dto.DisplayName,
                    Type = dto.Type,
                    Enabled = dto.Enabled ?? true,
                    BaseUrl = dto.BaseUrl,
                    RequiresApiKey = dto.RequiresApiKey ?? true,
                    DefaultModel = dto.DefaultModel,
                    CompatibleAgentsJson = JsonSerializer.Serialize(dto.CompatibleAgents ?? new List<string>()),
                    ConfigurationJson = JsonSerializer.Serialize(dto.Configuration ?? new Dictionary<string, string>())
                });
                await svc.ReloadAsync();
                return Results.Ok(new { name = dto.Name });
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
                await repo.DeleteProviderAsync(name);
                await svc.ReloadAsync();
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
                    SortOrder = dto.SortOrder ?? 0
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

        api.MapGet("/providers/settings", (ProviderConfigurationService svc) =>
                Results.Ok(svc.GetUserSettings()))
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
        models = models.Select(m => new { modelId = m.ModelId, displayName = m.DisplayName, enabled = m.Enabled, sortOrder = m.SortOrder })
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

    public sealed record ModelDto(string ModelId, string? DisplayName, bool? Enabled, int? SortOrder);

    public sealed record AgentSettingDto(string AgentType, string? Provider, string? Model);
}
