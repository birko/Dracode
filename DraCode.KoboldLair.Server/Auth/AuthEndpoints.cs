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

    private static async Task<IResult> HandleRefresh(RefreshRequest request, FederatedSessions sessions, CancellationToken ct)
    {
        // The Birko OAuth server's refresh_token grant: rotates the token and revokes the family on reuse (TASK-108)
        var login = await sessions.RefreshAsync(request.RefreshToken, ct);
        return login == null ? Results.Unauthorized() : Results.Ok(login);
    }

    private static async Task<IResult> HandleLogout(LogoutRequest request, FederatedSessions sessions, CancellationToken ct)
    {
        await sessions.RevokeAsync(request.RefreshToken, ct);
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
