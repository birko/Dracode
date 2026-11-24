using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Server.Auth;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// What the web Settings screen may show the caller (TASK-161). Always mapped, unlike <c>/providers</c>, so the screen
/// can tell "not yours to manage" from "not enabled on this server".
/// </summary>
public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder api, bool providersEnabled, bool keysEnabled = false)
    {
        api.MapGet("/settings/capabilities", (ICurrentUser user) => Results.Ok(new
        {
            // The /providers routes need manage_config; "*" is every permission
            canManageProviders = user.Permissions.Contains("*") || user.Permissions.Contains(KoboldLairPermissionChecker.ManageConfig),
            // The /budgets routes need manage_config too (TASK-163)
            canManageBudgets = user.Permissions.Contains("*") || user.Permissions.Contains(KoboldLairPermissionChecker.ManageConfig),
            // They are mapped only when a provider master key is set (DB-backed provider configuration, TASK-073)
            providersEnabled,
            providersHint = providersEnabled ? null
                : "Provider management is off on this server: set KoboldLair:ProviderConfig:MasterKey (or the KOBOLDLAIR_MASTER_KEY environment variable) and restart it.",
            // Service-account API keys (TASK-162): any signed-in user, when the OAuth server is on
            keysEnabled,
            keysHint = keysEnabled ? null
                : "API keys are off on this server: set Authentication:OAuth:Enabled (with JWT on) and restart it."
        }));
        return api;
    }
}
