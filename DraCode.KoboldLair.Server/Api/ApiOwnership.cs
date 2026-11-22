using System.Security.Claims;
using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Models.Projects;
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
    /// <summary>
    /// The caller's identity: the raw <c>NameIdentifier</c> claim (the token's <c>sub</c>), or null when it has none.
    /// Not <see cref="ICurrentUser.UserGuid"/>, which is null for GitHub (<c>github:{id}</c>) and service-account
    /// (<c>service:&lt;name&gt;</c>) subjects; Dragon stores the same raw subject as a project's owner.
    /// </summary>
    public static string? CallerId(ICurrentUser user)
    {
        var sub = user.GetClaim(ClaimTypes.NameIdentifier);
        return string.IsNullOrEmpty(sub) ? null : sub;
    }

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
        var caller = CallerId(user);
        return caller is not null && ownerId is not null && ownerId == caller;
    }

    /// <summary>
    /// The caller's projects; every project for an admin asking with <c>scope=all</c> (or with no identity). Shared by the
    /// listings that span projects (notifications, needs-you).
    /// </summary>
    public static IEnumerable<Project> VisibleProjects(ICurrentUser user, IProjectRepository repo, string? scope)
    {
        var admin = IsAdmin(user);
        var caller = CallerId(user);
        return (admin && (scope == "all" || caller is null)) ? repo.GetAll() : repo.GetAllForOwner(caller ?? string.Empty);
    }
}
