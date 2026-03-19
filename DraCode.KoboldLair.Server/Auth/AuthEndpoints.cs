using System.IdentityModel.Tokens.Jwt;
using Birko.Security;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Minimal API endpoints for JWT authentication: login, refresh, logout.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", HandleLogin);
        app.MapPost("/auth/refresh", HandleRefresh);
        app.MapPost("/auth/logout", HandleLogout);
    }

    private static IResult HandleLogin(
        LoginRequest request,
        IOptions<JwtAuthenticationConfiguration> config,
        ITokenProvider tokenProvider,
        IPasswordHasher passwordHasher,
        RefreshTokenStore refreshStore)
    {
        var jwtConfig = config.Value;

        var user = jwtConfig.Users.FirstOrDefault(u =>
            u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase));

        if (user == null)
            return Results.Unauthorized();

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            return Results.Unauthorized();

        // Generate access token with claims
        var claims = new Dictionary<string, string>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            ["name"] = user.Username,
            ["roles"] = string.Join(",", user.Roles)
        };

        var tokenResult = tokenProvider.GenerateToken(claims);
        var refreshToken = tokenProvider.GenerateRefreshToken();

        // Store refresh token
        var refreshExpiry = DateTime.UtcNow.AddDays(jwtConfig.RefreshExpirationDays);
        refreshStore.Store(refreshToken, user.Id, user.Username, user.Roles, refreshExpiry);

        return Results.Ok(new LoginResponse
        {
            Token = tokenResult.Token,
            RefreshToken = refreshToken,
            ExpiresAt = tokenResult.ExpiresAt,
            Roles = user.Roles,
            Username = user.Username
        });
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

        // Generate new token pair
        var claims = new Dictionary<string, string>
        {
            [JwtRegisteredClaimNames.Sub] = entry.UserId.ToString(),
            ["name"] = entry.Username,
            ["roles"] = string.Join(",", entry.Roles)
        };

        var tokenResult = tokenProvider.GenerateToken(claims);
        var newRefreshToken = tokenProvider.GenerateRefreshToken();

        var jwtConfig = config.Value;
        var refreshExpiry = DateTime.UtcNow.AddDays(jwtConfig.RefreshExpirationDays);
        refreshStore.Store(newRefreshToken, entry.UserId, entry.Username, entry.Roles, refreshExpiry);

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
public record LoginRequest(string Username, string Password);
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
