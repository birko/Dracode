namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// OAuth2 authorization-server settings, mapped from "Authentication:OAuth".
/// <para>
/// Issuer / Audience / signing Secret are intentionally NOT defined here — the OAuth server
/// reuses <see cref="JwtAuthenticationConfiguration"/> (Secret/Issuer/Audience) so the tokens it
/// issues validate under the same JWT bearer middleware (TASK-032) by construction. This section
/// carries only OAuth-specific knobs.
/// </para>
/// </summary>
public class OAuthServerConfiguration
{
    /// <summary>Whether the OAuth authorization-server endpoints are mapped (default: false).</summary>
    public bool Enabled { get; set; }

    /// <summary>Device-code verification URI shown to the user (RFC 8628 §3.2).</summary>
    public string DeviceVerificationUri { get; set; } = "https://localhost/device";

    /// <summary>Lifetime of issued access tokens, seconds. Default 3600 (1h).</summary>
    public int AccessTokenLifetimeSeconds { get; set; } = 3600;

    /// <summary>Lifetime of issued refresh tokens, seconds. Default 14 days.</summary>
    public int RefreshTokenLifetimeSeconds { get; set; } = 60 * 60 * 24 * 14;

    /// <summary>Lifetime of authorization codes, seconds. Default 60.</summary>
    public int AuthorizationCodeLifetimeSeconds { get; set; } = 60;

    /// <summary>Lifetime of device codes, seconds. Default 600 (10m).</summary>
    public int DeviceCodeLifetimeSeconds { get; set; } = 600;

    /// <summary>Minimum device-code token polling interval, seconds. Default 5.</summary>
    public int DeviceCodePollingIntervalSeconds { get; set; } = 5;

    /// <summary>Rotate refresh tokens on every refresh (RFC 6819 §5.2.2.3). Default true.</summary>
    public bool RotateRefreshTokens { get; set; } = true;

    /// <summary>Require PKCE for public clients. Default true.</summary>
    public bool RequirePkceForPublicClients { get; set; } = true;

    /// <summary>
    /// Whether the RFC 7591 dynamic client-registration endpoint (<c>/register</c>) is mapped at all.
    /// Independent of <see cref="Enabled"/>; default <c>false</c> so enabling OAuth does NOT expose an
    /// open "create yourself a confidential client" endpoint. Admin-auth gating lands in TASK-032.
    /// </summary>
    public bool AllowDynamicRegistration { get; set; }
}
