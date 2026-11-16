using System.Security.Claims;
using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Server.Api;
using DraCode.KoboldLair.Server.Services;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>The caller of a <c>/dragon</c> or <c>/kobold</c> connection.</summary>
public static class WebSocketCaller
{
    /// <summary>
    /// The subject is <see cref="ApiOwnership.CallerId"/>, as on <c>/api/v1</c>. Without an
    /// authenticated user (JWT off) the caller is the local single-user owner, an admin with no subject.
    /// </summary>
    public static KoboldCaller From(ICurrentUser user)
    {
        if (!user.IsAuthenticated)
            return new KoboldCaller(null, true, null, null);

        return new KoboldCaller(
            ApiOwnership.CallerId(user),
            ApiOwnership.IsAdmin(user),
            user.GetClaim("name") ?? user.GetClaim(ClaimTypes.Name),
            user.GetClaim("email") ?? user.Email);
    }
}
