using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Server.Auth;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// One shared per-caller ownership rule for the <c>/api/v1</c> resource surface (TASK-043), lifted out
/// of <c>RunsEndpoints</c> so every handler enforces it identically: admins (<c>*</c> / <c>view_all</c>)
/// see everything; otherwise the caller must have a concrete identity that matches the resource owner.
/// A null caller identity or a null-owner resource is never accessible to a non-admin — and handlers
/// return <b>404, not 403</b>, so a resource's existence isn't leaked to non-owners.
/// </summary>
public static class ApiOwnership
{
    /// <summary>True when the caller carries the wildcard or <c>view_all</c> permission.</summary>
    public static bool IsAdmin(ICurrentUser user) =>
        user.Permissions.Contains("*") || user.Permissions.Contains(KoboldLairPermissionChecker.ViewAll);

    /// <summary>
    /// Whether <paramref name="user"/> may access a resource owned by <paramref name="ownerId"/>.
    /// Admins always may; otherwise both the caller identity and the owner must be present and equal.
    /// </summary>
    public static bool CanAccess(ICurrentUser user, string? ownerId)
    {
        if (IsAdmin(user))
            return true;
        var caller = user.UserId?.ToString();
        return caller is not null && ownerId is not null && ownerId == caller;
    }
}
