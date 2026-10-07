using System.IdentityModel.Tokens.Jwt;
using Birko.Security;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Token refresh and logout for interactively issued tokens (GitHub login, TASK-033). Sign-in itself is GitHub federation
/// or the OAuth server; the static username/password <c>/auth/login</c> was removed (TASK-036).
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/refresh", HandleRefresh);
        app.MapPost("/auth/logout", HandleLogout);
    }

    private static IResult HandleRefresh(
        RefreshRequest request,
        ITokenProvider tokenProvider,
        RefreshTokenStore refreshStore,
        IOptions<JwtAuthenticationConfiguration> config)
    {
        var entry = refreshStore.Validate(request.RefreshToken);
        if (entry == null)
            return Results.Unauthorized();

        // Revoke old refresh token (rotation)
        refreshStore.Revoke(request.RefreshToken);

        // Generate a new token pair with the same claim set, including the scope/permission claim
        var permissions = KoboldLairPermissionChecker.ExpandRolesToPermissions(entry.Roles);
        var claims = new Dictionary<string, string>
        {
            [JwtRegisteredClaimNames.Sub] = entry.OwnerSub,
            ["name"] = entry.Username,
            ["roles"] = string.Join(",", entry.Roles),
            ["scope"] = string.Join(",", permissions)
        };

        var tokenResult = tokenProvider.GenerateToken(claims);
        var newRefreshToken = tokenProvider.GenerateRefreshToken();

        var jwtConfig = config.Value;
        var refreshExpiry = DateTime.UtcNow.AddDays(jwtConfig.RefreshExpirationDays);
        refreshStore.Store(newRefreshToken, entry.OwnerSub, entry.Username, entry.Roles, refreshExpiry);

        return Results.Ok(new LoginResponse
        {
            Token = tokenResult.Token,
            RefreshToken = newRefreshToken,
            ExpiresAt = tokenResult.ExpiresAt,
            Roles = entry.Roles,
            Username = entry.Username
        });
    }

    private static IResult HandleLogout(
        LogoutRequest request,
        RefreshTokenStore refreshStore)
    {
        refreshStore.Revoke(request.RefreshToken);
        return Results.Ok(new { message = "Logged out" });
    }
}

// Request/Response DTOs
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public List<string> Roles { get; set; } = [];
    public string Username { get; set; } = string.Empty;
}
