namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// KoboldLair's permission names and the role → permission map used when a token is minted. Enforcement reads the
/// token's <c>scope</c> claim (Birko's <c>PermissionEndpointFilter</c>); there is no server-side user lookup (TASK-036).
/// </summary>
public static class KoboldLairPermissionChecker
{
    // Permission constants
    public const string ManageProjects = "manage_projects";
    public const string ExecuteAgents = "execute_agents";
    public const string ViewAll = "view_all";
    public const string ViewOwn = "view_own";
    public const string ManageUsers = "manage_users";
    public const string ManageConfig = "manage_config";

    private static readonly Dictionary<string, HashSet<string>> RolePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] = [ManageProjects, ExecuteAgents, ViewAll, ViewOwn, ManageUsers, ManageConfig],
        ["user"] = [ManageProjects, ExecuteAgents, ViewOwn],
        ["viewer"] = [ViewAll]
    };

    /// <summary>
    /// Expands a set of role names into the distinct permissions they grant. Used when minting
    /// tokens (TASK-032) so the JWT carries a permission/scope claim that
    /// <c>ClaimsCurrentUser.Permissions</c> + <c>PermissionEndpointFilter</c> can enforce.
    /// </summary>
    public static IReadOnlyList<string> ExpandRolesToPermissions(IEnumerable<string> roles) =>
        roles
            .Where(role => RolePermissions.ContainsKey(role))
            .SelectMany(role => RolePermissions[role])
            .Distinct()
            .ToList();
}
