using Birko.Security.Authorization;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Permission checker with role-to-permission mapping for KoboldLair.
/// </summary>
public class KoboldLairPermissionChecker : IPermissionChecker
{
    private readonly JwtAuthenticationConfiguration _config;

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

    public KoboldLairPermissionChecker(IOptions<JwtAuthenticationConfiguration> config)
    {
        _config = config.Value;
    }

    public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct = default)
    {
        var user = _config.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null) return Task.FromResult(false);

        var hasPermission = user.Roles.Any(role =>
            RolePermissions.TryGetValue(role, out var perms) && perms.Contains(permission));

        return Task.FromResult(hasPermission);
    }

    public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = _config.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null) return Task.FromResult<IReadOnlyList<string>>([]);

        var permissions = user.Roles
            .Where(role => RolePermissions.ContainsKey(role))
            .SelectMany(role => RolePermissions[role])
            .Distinct()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(permissions.AsReadOnly());
    }
}
