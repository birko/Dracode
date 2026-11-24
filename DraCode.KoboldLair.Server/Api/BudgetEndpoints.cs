using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Server.Services;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// Settings → Budgets (TASK-163, FEATURE-026 D20), admin only (<c>manage_config</c>): <c>GET /budgets</c> gives the
/// daily, monthly and per-project budgets and the warning threshold in force, with today's and this month's spend and
/// the project that has spent most; <c>PUT /budgets</c> saves all four, which win over appsettings from then on and are
/// enforced at once (<see cref="BudgetSettingsService"/>). A value out of range is 400; a server with no SQLite store
/// cannot save one (409).
/// </summary>
public static class BudgetEndpoints
{
    public static RouteGroupBuilder MapBudgetEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/budgets", async (BudgetSettingsService budgets, CancellationToken ct) => Results.Ok(await budgets.GetAsync(ct)))
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);

        api.MapPut("/budgets", async (BudgetSettingsService.Change change, ICurrentUser user, BudgetSettingsService budgets,
                CancellationToken ct) =>
            {
                if (BudgetSettingsService.Validate(change) is { } problem)
                    return Results.BadRequest(new { error = problem });
                if (!budgets.Editable)
                    return Results.Conflict(new { error = "Budgets cannot be saved on this server: it keeps no SQLite database. Set them in appsettings." });
                await budgets.UpdateAsync(change, ApiOwnership.CallerId(user));
                return Results.Ok(await budgets.GetAsync(ct));
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageConfig);
        return api;
    }
}
