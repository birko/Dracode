using DraCode.KoboldLair.Server.Models.Configuration;
using Microsoft.Extensions.Options;
using System.Net;

namespace DraCode.KoboldLair.Server.Services
{
    public class WebSocketAuthenticationService
    {
        private readonly AuthenticationConfiguration _config;
        private readonly ILogger<WebSocketAuthenticationService> _logger;

        public WebSocketAuthenticationService(
            IOptions<AuthenticationConfiguration> config,
            ILogger<WebSocketAuthenticationService> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public bool IsAuthenticationEnabled()
        {
            return _config.Enabled && (_config.Tokens.Any() || _config.TokenBindings.Any());
        }

        public bool ValidateToken(string? token, string? clientIp)
        {
            // If authentication is not enabled, allow all connections
            if (!IsAuthenticationEnabled())
            {
                return true;
            }

            // If authentication is enabled but no token provided, reject
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Connection attempt without token from IP: {ClientIp}", clientIp ?? "unknown");
                return false;
            }

            // First, check token bindings (token + IP validation)
            if (_config.TokenBindings.Any())
            {
                foreach (var binding in _config.TokenBindings)
                {
                    var expandedToken = ExpandEnvironmentVariable(binding.Token);
                    if (expandedToken == token)
                    {
                        // Token matches, now check IP
                        if (string.IsNullOrWhiteSpace(clientIp))
                        {
                            _logger.LogWarning("Token matched but client IP is unknown for token binding validation");
                            return false;
                        }

                        var expandedIps = binding.AllowedIps
                            .Select(ExpandEnvironmentVariable)
                            .Where(ip => !string.IsNullOrWhiteSpace(ip))
                            .ToList();

                        if (expandedIps.Contains(clientIp))
                        {
                            _logger.LogInformation("Authenticated token with IP binding: {ClientIp}", clientIp);
                            return true;
                        }
                        else
                        {
                            _logger.LogWarning("Token valid but IP {ClientIp} not in allowed list for this token", clientIp);
                            return false;
                        }
                    }
                }
            }

            // Fall back to simple token validation (no IP binding)
            if (_config.Tokens.Any())
            {
                var validTokens = _config.Tokens
                    .Select(ExpandEnvironmentVariable)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                if (!validTokens.Any())
                {
                    _logger.LogWarning("Authentication enabled but no valid tokens configured");
                    return false;
                }

                var isValid = validTokens.Contains(token);

                if (isValid)
                {
                    _logger.LogInformation("Authenticated token (no IP binding) from IP: {ClientIp}", clientIp ?? "unknown");
                }
                else
                {
                    _logger.LogWarning("Invalid token attempt from IP: {ClientIp}", clientIp ?? "unknown");
                }

                return isValid;
            }

            _logger.LogWarning("Authentication enabled but no tokens or bindings configured");
            return false;
        }

        public string? ExtractTokenFromQuery(HttpContext context)
        {
            return context.Request.Query["token"].FirstOrDefault();
        }

        public string? GetClientIpAddress(HttpContext context)
        {
            // Check for forwarded IP (behind proxy/load balancer)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                // X-Forwarded-For can contain multiple IPs, take the first one (original client)
                var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (ips.Length > 0)
                {
                    return ips[0];
                }
            }

            // Check for X-Real-IP header (nginx)
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(realIp))
            {
                return realIp;
            }

            // Fall back to direct connection IP
            return context.Connection.RemoteIpAddress?.ToString();
        }

        private string ExpandEnvironmentVariable(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Check if value is in format ${ENV_VAR}
            if (value.StartsWith("${") && value.EndsWith("}"))
            {
                var envVar = value.Substring(2, value.Length - 3);
                return Environment.GetEnvironmentVariable(envVar) ?? value;
            }

            return value;
        }
    }
}
