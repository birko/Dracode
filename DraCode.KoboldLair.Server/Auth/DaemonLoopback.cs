using System.Net;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Options for the daemon loopback authentication bypass (TASK-032). Bound from
/// <c>Authentication:Daemon</c>. When daemon mode (TASK-047/048/049) lands it will set the bind
/// address; until then the bypass is gated purely by <see cref="LoopbackBypass"/> + the resolved
/// bind URLs being loopback-only.
/// </summary>
public class DaemonAuthOptions
{
    /// <summary>
    /// When true AND every bound URL is loopback (127.0.0.1 / ::1 / localhost), JWT validation is
    /// skipped and requests run as a synthetic OS-user-trust principal. Any non-loopback bind
    /// enforces JWT regardless of this flag. Default: false.
    /// </summary>
    public bool LoopbackBypass { get; set; }
}

/// <summary>
/// Pure decision logic for the daemon loopback bypass — factored out of the pipeline so it can be
/// unit-tested without a running host.
/// </summary>
public static class DaemonLoopback
{
    /// <summary>
    /// Returns true only when the bypass flag is enabled AND at least one bind URL is known AND
    /// every known bind URL resolves to a loopback host. Fails safe: an unknown/empty bind set, or
    /// any non-loopback bind, returns false (enforce JWT).
    /// </summary>
    public static bool ShouldBypass(bool flagEnabled, IEnumerable<string>? urls)
    {
        if (!flagEnabled)
            return false;

        var list = urls?
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToList() ?? [];

        if (list.Count == 0)
            return false; // can't confirm loopback → enforce

        return list.All(IsLoopback);
    }

    private static bool IsLoopback(string url)
    {
        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            !Uri.TryCreate("http://" + trimmed, UriKind.Absolute, out uri))
        {
            return false;
        }

        var host = uri.Host;

        // Kestrel uses "localhost", "0.0.0.0", "[::]", or specific IPs. Wildcard binds (0.0.0.0 / ::)
        // are NOT loopback — they accept remote connections, so they must enforce.
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
    }
}
