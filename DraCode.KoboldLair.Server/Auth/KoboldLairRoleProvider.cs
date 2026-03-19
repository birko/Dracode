using Birko.Security.Authorization;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Role provider backed by the embedded user store in JwtAuthenticationConfiguration.
/// </summary>
public class KoboldLairRoleProvider : IRoleProvider
{
    private readonly JwtAuthenticationConfiguration _config;

    public KoboldLairRoleProvider(IOptions<JwtAuthenticationConfiguration> config)
    {
        _config = config.Value;
    }

    public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = _config.Users.FirstOrDefault(u => u.Id == userId);
        IReadOnlyList<string> roles = user?.Roles.AsReadOnly() ?? (IReadOnlyList<string>)[];
        return Task.FromResult(roles);
    }

    public Task<bool> IsInRoleAsync(Guid userId, string role, CancellationToken ct = default)
    {
        var user = _config.Users.FirstOrDefault(u => u.Id == userId);
        return Task.FromResult(user?.Roles.Contains(role, StringComparer.OrdinalIgnoreCase) ?? false);
    }
}
