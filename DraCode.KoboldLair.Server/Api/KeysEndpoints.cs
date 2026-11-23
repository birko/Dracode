using Birko.Security.AspNetCore;
using Birko.Security.OAuth.Server;
using Birko.Security.OAuth.Server.Stores;
using Birko.Security.OAuth.Server.Models;
using Birko.Security.OAuth.Server.Endpoints.ClientRegistration;
using DraCode.KoboldLair.Server.Auth;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// Service-account API keys for the person signed in (TASK-162, FEATURE-026 D6). A key is a confidential OAuth client
/// on the client-credentials grant; it acts as <c>service:&lt;name&gt;</c> (FEATURE-019 D14). Decided by the owner
/// (2026-11-22): any signed-in user may create keys, and a key gets a fixed set of permissions
/// (<see cref="KeyPermissions"/>) narrowed to its creator's, so it never holds more than they do. Owner-scoped through
/// <see cref="ApiOwnership"/>: a caller sees and revokes their own keys (another owner's reads as 404); an admin sees all
/// with <c>scope=all</c> and may revoke any. The secret is returned once, by the create call.
/// </summary>
public static class KeysEndpoints
{
    /// <summary>The permissions a key is given, before narrowing to the creator's (FEATURE-019 D15 vocabulary).</summary>
    public static readonly string[] KeyPermissions =
        [KoboldLairPermissionChecker.ViewOwn, KoboldLairPermissionChecker.ManageProjects, KoboldLairPermissionChecker.ExecuteAgents];

    public sealed record CreateKeyDto(string? Name);

    public static RouteGroupBuilder MapKeyEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/keys", async (string? scope, ICurrentUser user, ServiceKeyRegistry registry, IOAuthClientStore clients) =>
        {
            var all = ApiOwnership.IsAdmin(user) && scope == "all";
            var caller = ApiOwnership.CallerId(user);
            if (!all && caller is null) return Results.Ok(Array.Empty<object>());
            var keys = await registry.ListAsync(all ? null : caller);
            var views = new List<object>();
            foreach (var key in keys)
            {
                var client = await clients.GetByClientIdAsync(key.ClientId);
                views.Add(View(key, client));
            }
            return Results.Ok(views);
        });

        api.MapPost("/keys", async (CreateKeyDto dto, ICurrentUser user, ServiceKeyRegistry registry, OAuthClientNames clientNames,
            OAuthServer server, ILoggerFactory loggers) =>
        {
            var caller = ApiOwnership.CallerId(user);
            if (caller is null) return Results.Forbid();
            var name = (dto.Name ?? "").Trim();
            if (name.Length is 0 or > 100 || name.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
                return Results.BadRequest(new { error = "A key needs a name of 1-100 characters without spaces, e.g. my-ci-runner." });
            // Every client's name counts, not only keys recorded here: one registered before owners were recorded would
            // otherwise lend its service:<name> identity, and its projects, to the new key (TASK-194)
            bool taken;
            try
            {
                taken = await registry.NameTakenAsync(name) || await clientNames.ExistsAsync(name);
            }
            catch (Exception ex)
            {
                loggers.CreateLogger("Keys").LogError(ex, "Could not check whether API key name {Name} is taken", name);
                return Results.Problem("Could not check whether the name is taken; no key was created.", statusCode: 503);
            }
            if (taken)
                return Results.Conflict(new { error = $"A key or client named '{name}' already exists. A key acts as service:<name>, so names must be unique." });

            var scopes = KeyPermissions.Where(p => user.Permissions.Contains("*") || user.Permissions.Contains(p)).ToList();
            if (scopes.Count == 0)
                return Results.BadRequest(new { error = "You have none of the permissions a key is given, so a key could do nothing." });

            ClientRegistrationResponse r;
            try
            {
                r = await server.ClientRegistration.RegisterAsync(new ClientRegistrationRequest
                {
                    Name = name,
                    ClientType = OAuthClientType.Confidential,
                    AllowedGrantTypes = [OAuthGrantTypes.ClientCredentials],
                    AllowedScopes = scopes
                });
            }
            catch (OAuthServerException ex) { return Results.BadRequest(new { error = ex.ErrorDescription ?? ex.ErrorCode }); }

            var key = new ServiceKey(r.ClientId, caller, name, r.CreatedAt);
            await registry.AddAsync(key);
            return Results.Json(new
            {
                clientId = r.ClientId,
                clientSecret = r.ClientSecret, // returned this once; the store keeps only its hash
                name,
                ownerId = caller,
                scopes,
                createdAt = r.CreatedAt,
                disabled = false
            }, statusCode: 201);
        });

        api.MapPost("/keys/{clientId}/revoke", async (string clientId, ICurrentUser user, ServiceKeyRegistry registry, IOAuthClientStore clients) =>
        {
            var key = await registry.GetAsync(clientId);
            if (key is null || !ApiOwnership.CanAccess(user, key.OwnerId)) return Results.NotFound();
            var client = await clients.GetByClientIdAsync(clientId);
            if (client is null) return Results.NotFound();
            if (client.IsEnabled)
            {
                client.IsEnabled = false;
                await clients.UpdateAsync(client);
            }
            return Results.Ok(View(key, client));
        });

        return api;
    }

    private static object View(ServiceKey key, OAuthClient? client) => new
    {
        clientId = key.ClientId,
        name = key.Name,
        ownerId = key.OwnerId,
        scopes = client?.AllowedScopes ?? new List<string>(),
        createdAt = key.CreatedAt,
        disabled = client is null || !client.IsEnabled
    };
}
