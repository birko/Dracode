namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// GitHub federation configuration for human login (TASK-033 / FEATURE-019 D1, D11, D13).
/// Mapped from "Authentication:GitHub" in appsettings.json. Disabled by default — the
/// endpoints are only mapped when <see cref="Enabled"/> is true, and federation mints DraCode
/// JWTs so it additionally requires "Authentication:Jwt:Enabled".
/// </summary>
public class GitHubFederationConfiguration
{
    /// <summary>
    /// Whether GitHub federation is enabled (default: false). When false the
    /// <c>/auth/github/*</c> endpoints are not mapped.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// GitHub OAuth app client id. Supports environment variable expansion:
    /// "${KOBOLDLAIR_GITHUB_CLIENT_ID}".
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// GitHub OAuth app client secret. Supports environment variable expansion:
    /// "${KOBOLDLAIR_GITHUB_CLIENT_SECRET}".
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The callback URL registered with the GitHub OAuth app — must match
    /// <c>/auth/github/callback</c> on this server (e.g. "https://localhost:7xxx/auth/github/callback").
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// OAuth scope requested from GitHub. "read:user" is sufficient to read the stable numeric id.
    /// </summary>
    public string Scope { get; set; } = "read:user";

    /// <summary>
    /// Roles granted to a freshly federated human. Expanded to the JWT scope/permission claim via
    /// <see cref="KoboldLairPermissionChecker.ExpandRolesToPermissions"/>.
    /// </summary>
    public List<string> DefaultRoles { get; set; } = ["user"];

    /// <summary>
    /// Admission allowlist (FEATURE-019 D13): GitHub numeric user ids permitted to federate.
    /// GitHub OAuth proves <em>who</em> a caller is, not <em>whether</em> they may sign in — without
    /// this gate anyone with a GitHub account could mint a token. Keyed on the stable numeric id
    /// (usernames are mutable). <strong>Empty ⇒ deny all.</strong>
    /// </summary>
    public List<long> AllowedGitHubIds { get; set; } = [];

    /// <summary>
    /// Absolute URL of the SPA to redirect to after a successful login. The minted tokens are
    /// appended in the URL fragment (<c>#access_token=…&amp;refresh_token=…</c>) so they never reach
    /// the server/logs; the SPA reads them from <c>location.hash</c> (TASK-056). Must be a fixed
    /// config value, never reflected from the request (open-redirect guard).
    /// </summary>
    public string PostLoginRedirectUri { get; set; } = string.Empty;

    /// <summary>Resolves <see cref="ClientId"/>, expanding a <c>${ENV_VAR}</c> pattern if present.</summary>
    public string ResolveClientId() => ResolveEnv(ClientId);

    /// <summary>Resolves <see cref="ClientSecret"/>, expanding a <c>${ENV_VAR}</c> pattern if present.</summary>
    public string ResolveClientSecret() => ResolveEnv(ClientSecret);

    /// <summary>
    /// Expands a <c>${ENV_VAR}</c> placeholder to its environment value; returns the raw value
    /// otherwise. Mirrors <see cref="JwtAuthenticationConfiguration.ResolveSecret"/>.
    /// </summary>
    private static string ResolveEnv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        if (value.StartsWith("${") && value.EndsWith("}"))
        {
            var envVar = value[2..^1];
            return Environment.GetEnvironmentVariable(envVar) ?? string.Empty;
        }

        return value;
    }
}
