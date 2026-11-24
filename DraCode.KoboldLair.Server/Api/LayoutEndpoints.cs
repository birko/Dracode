using System.Text;
using System.Text.Json;
using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Server.Auth;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// Workspace layouts (TASK-058, FEATURE-026): each person's own, shared by all their projects, keyed by the caller's
/// subject (<c>view_own</c>).
/// <list type="bullet">
/// <item><c>GET</c>/<c>PUT /layouts/current</c>: the layout the web client restores on every load (tab order, Dragon's
/// width, the pin); <c>{ layout: null }</c> until the first save.</item>
/// <item><c>GET /layouts</c>, <c>PUT</c>/<c>DELETE /layouts/{name}</c>: layouts saved by name, a save replacing one of
/// the same name.</item>
/// </list>
/// The server keeps a layout as the client sends it: a JSON object of at most 16 KB. Names are 1–60 characters, at most
/// 50 per person. A caller with no identity is 403; a server with no SQLite store reads no layouts and saves none (409).
/// </summary>
public static class LayoutEndpoints
{
    public const int MaxLayoutBytes = 16 * 1024;
    public const int MaxNameLength = 60;
    public const int MaxLayoutsPerPerson = 50;

    public static RouteGroupBuilder MapLayoutEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/layouts/current", async (ICurrentUser user, IServiceProvider services) =>
            {
                if (ApiOwnership.CallerId(user) is not { } owner) return NoIdentity();
                var store = services.GetService<SqlLayoutRepository>();
                var json = store == null ? null : await store.GetCurrentAsync(owner);
                return Results.Ok(new { layout = json == null ? (JsonElement?)null : Parse(json) });
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        api.MapPut("/layouts/current", async (HttpRequest request, ICurrentUser user, IServiceProvider services) =>
            {
                if (ApiOwnership.CallerId(user) is not { } owner) return NoIdentity();
                var (json, problem) = await ReadLayout(request);
                if (problem != null) return Results.BadRequest(new { error = problem });
                if (services.GetService<SqlLayoutRepository>() is not { } store) return NoStore();
                await store.SaveCurrentAsync(owner, json!);
                return Results.NoContent();
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        api.MapGet("/layouts", async (ICurrentUser user, IServiceProvider services) =>
            {
                if (ApiOwnership.CallerId(user) is not { } owner) return NoIdentity();
                if (services.GetService<SqlLayoutRepository>() is not { } store) return Results.Ok(Array.Empty<object>());
                var rows = await store.ListAsync(owner);
                return Results.Ok(rows.Select(r => new { name = r.Name, layout = Parse(r.LayoutJson), updatedAt = r.UpdatedAt }));
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        api.MapPut("/layouts/{name}", async (string name, HttpRequest request, ICurrentUser user, IServiceProvider services) =>
            {
                if (ApiOwnership.CallerId(user) is not { } owner) return NoIdentity();
                name = name.Trim();
                if (name.Length is 0 or > MaxNameLength)
                    return Results.BadRequest(new { error = $"A layout's name is 1 to {MaxNameLength} characters." });
                var (json, problem) = await ReadLayout(request);
                if (problem != null) return Results.BadRequest(new { error = problem });
                if (services.GetService<SqlLayoutRepository>() is not { } store) return NoStore();
                if (!await store.ExistsAsync(owner, name) && await store.CountAsync(owner) >= MaxLayoutsPerPerson)
                    return Results.Conflict(new { error = $"You have {MaxLayoutsPerPerson} saved layouts, the most kept. Delete one, or save over one of them." });
                var row = await store.SaveAsync(owner, name, json!);
                return Results.Ok(new { name = row.Name, layout = Parse(row.LayoutJson), updatedAt = row.UpdatedAt });
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        api.MapDelete("/layouts/{name}", async (string name, ICurrentUser user, IServiceProvider services) =>
            {
                if (ApiOwnership.CallerId(user) is not { } owner) return NoIdentity();
                if (services.GetService<SqlLayoutRepository>() is not { } store) return Results.NotFound();
                return await store.DeleteAsync(owner, name.Trim()) ? Results.NoContent() : Results.NotFound();
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        return api;
    }

    /// <summary>The request body as a layout: a JSON object of at most <see cref="MaxLayoutBytes"/>, else why not.</summary>
    private static async Task<(string? Json, string? Problem)> ReadLayout(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var buffer = new char[MaxLayoutBytes + 1];
        var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
        if (read > MaxLayoutBytes || Encoding.UTF8.GetByteCount(buffer, 0, read) > MaxLayoutBytes)
            return (null, "A layout is at most 16 KB.");
        var text = new string(buffer, 0, read);
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? (doc.RootElement.GetRawText(), null)
                : (null, "A layout is a JSON object.");
        }
        catch (JsonException)
        {
            return (null, "A layout is a JSON object.");
        }
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static IResult NoIdentity() =>
        Results.Json(new { error = "Layouts belong to a signed-in person: this caller has no identity." }, statusCode: StatusCodes.Status403Forbidden);

    private static IResult NoStore() =>
        Results.Conflict(new { error = "Layouts cannot be saved on this server: it keeps no SQLite database." });
}
