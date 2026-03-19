namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// JWT authentication configuration for KoboldLair WebSocket server.
/// Mapped from "Authentication:Jwt" in appsettings.json.
/// </summary>
public class JwtAuthenticationConfiguration
{
    /// <summary>
    /// Whether JWT authentication is enabled (default: false for backward compatibility)
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// HMAC-SHA256 secret for signing JWTs. Minimum 32 characters.
    /// Supports environment variable expansion: "${KOBOLDLAIR_JWT_SECRET}"
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// JWT issuer claim
    /// </summary>
    public string Issuer { get; set; } = "KoboldLair";

    /// <summary>
    /// JWT audience claim
    /// </summary>
    public string Audience { get; set; } = "KoboldLair";

    /// <summary>
    /// Access token expiration in minutes (default: 60)
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh token expiration in days (default: 7)
    /// </summary>
    public int RefreshExpirationDays { get; set; } = 7;

    /// <summary>
    /// Embedded user store for simple deployments.
    /// For production, replace with a database-backed user store.
    /// </summary>
    public List<JwtUser> Users { get; set; } = [];

    /// <summary>
    /// Resolves the secret, expanding environment variables if needed.
    /// </summary>
    public string ResolveSecret()
    {
        if (string.IsNullOrEmpty(Secret)) return string.Empty;

        // Expand ${ENV_VAR} patterns
        if (Secret.StartsWith("${") && Secret.EndsWith("}"))
        {
            var envVar = Secret[2..^1];
            return Environment.GetEnvironmentVariable(envVar) ?? string.Empty;
        }

        return Secret;
    }
}

/// <summary>
/// Embedded user definition for JWT authentication.
/// </summary>
public class JwtUser
{
    /// <summary>
    /// Unique user identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Username for login
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password hash (use Pbkdf2PasswordHasher to generate)
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Assigned roles: "admin", "user", "viewer"
    /// </summary>
    public List<string> Roles { get; set; } = ["user"];
}
