using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Birko.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DraCode.KoboldLair.Tests.Api;

/// <summary>
/// Workspace layouts (TASK-058): each person's current layout (restored on every load: tab order, Dragon's width, the
/// pin) and their named layouts, stored on the server per person and shared by all their projects. The server keeps a
/// layout as the client sends it (a JSON object, at most 16 KB); names are 1–60 characters, at most 50 per person.
/// </summary>
public class LayoutEndpointsTests : IDisposable
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-layout-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_projectsPath)) Directory.Delete(_projectsPath, recursive: true); } catch { }
    }

    private WebApplicationFactory<Program> CreateFactory(string backend = "SqLite") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Enabled"] = "true",
                ["Authentication:Jwt:Secret"] = Secret,
                ["Authentication:Jwt:Issuer"] = "KoboldLair",
                ["Authentication:Jwt:Audience"] = "KoboldLair",
                ["KoboldLair:Data:DefaultBackend"] = backend,
                ["KoboldLair:ProjectsPath"] = _projectsPath,
            }));
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });

    private static string Token(WebApplicationFactory<Program> factory, string sub, string scope = "view_own") =>
        factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = sub,
            ["scope"] = scope
        }).Token;

    private static HttpRequestMessage Authed(HttpMethod method, string uri, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body != null) request.Content = JsonContent.Create(body);
        return request;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static object Layout(string openTab = "board", int dragonWidth = 360, bool pinned = false) => new
    {
        tabs = new { workspace = new[] { "board", "runs", "spec", "git", "stats" } },
        splits = new { workspace = new { master = "1fr", detail = $"{dragonWidth}px" } },
        openTab,
        dragonPinned = pinned,
    };

    [Fact]
    public async Task A_person_keeps_one_current_layout_which_nobody_else_sees()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var alice = Token(factory, "alice");

        var empty = await Json(await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/layouts/current", alice)));
        empty.GetProperty("layout").ValueKind.Should().Be(JsonValueKind.Null);

        var put = await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/current", alice, Layout(dragonWidth: 480, pinned: true)));
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/current", alice, Layout(dragonWidth: 500)));

        var current = (await Json(await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/layouts/current", alice)))).GetProperty("layout");
        current.GetProperty("splits").GetProperty("workspace").GetProperty("detail").GetString().Should().Be("500px");
        current.GetProperty("dragonPinned").GetBoolean().Should().BeFalse();
        current.GetProperty("tabs").GetProperty("workspace")[1].GetString().Should().Be("runs");

        var bob = await Json(await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/layouts/current", Token(factory, "bob"))));
        bob.GetProperty("layout").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Named_layouts_are_saved_overwritten_by_name_listed_and_deleted_per_person()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var alice = Token(factory, "alice");

        var saved = await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/Reviewing", alice, Layout("git", 520)));
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(saved)).GetProperty("name").GetString().Should().Be("Reviewing");
        await client.SendAsync(Authed(HttpMethod.Put, $"/api/v1/layouts/{Uri.EscapeDataString("Watching runs")}", alice, Layout("runs")));
        await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/Reviewing", alice, Layout("git", 600))); // overwrites

        var list = await Json(await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/layouts", alice)));
        list.EnumerateArray().Select(l => l.GetProperty("name").GetString()).Should().Equal("Reviewing", "Watching runs");
        list[0].GetProperty("layout").GetProperty("splits").GetProperty("workspace").GetProperty("detail").GetString().Should().Be("600px");
        list[0].GetProperty("layout").GetProperty("openTab").GetString().Should().Be("git");

        (await Json(await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/layouts", Token(factory, "bob"))))).GetArrayLength().Should().Be(0);
        (await client.SendAsync(Authed(HttpMethod.Delete, "/api/v1/layouts/Reviewing", Token(factory, "bob")))).StatusCode
            .Should().Be(HttpStatusCode.NotFound); // only her own

        (await client.SendAsync(Authed(HttpMethod.Delete, "/api/v1/layouts/Reviewing", alice))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.SendAsync(Authed(HttpMethod.Delete, "/api/v1/layouts/Reviewing", alice))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var after = await Json(await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/layouts", alice)));
        after.EnumerateArray().Select(l => l.GetProperty("name").GetString()).Should().Equal("Watching runs");
    }

    [Fact]
    public async Task Layouts_outlive_a_restart()
    {
        using (var factory = CreateFactory())
        {
            var client = factory.CreateClient();
            var alice = Token(factory, "alice");
            await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/current", alice, Layout(dragonWidth: 420)));
            await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/Reviewing", alice, Layout("git")));
        }
        SqliteConnection.ClearAllPools();

        using var restarted = CreateFactory();
        var again = restarted.CreateClient();
        var token = Token(restarted, "alice");
        var current = (await Json(await again.SendAsync(Authed(HttpMethod.Get, "/api/v1/layouts/current", token)))).GetProperty("layout");
        current.GetProperty("splits").GetProperty("workspace").GetProperty("detail").GetString().Should().Be("420px");
        (await Json(await again.SendAsync(Authed(HttpMethod.Get, "/api/v1/layouts", token)))).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Bad_names_and_bodies_are_refused()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var alice = Token(factory, "alice");

        var longName = Uri.EscapeDataString(new string('x', 61));
        (await client.SendAsync(Authed(HttpMethod.Put, $"/api/v1/layouts/{longName}", alice, Layout()))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/%20%20", alice, Layout()))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/Reviewing", alice, new[] { 1, 2 }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/current", alice, "just text"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var huge = new { notes = new string('x', 17 * 1024) };
        var tooBig = await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/Reviewing", alice, huge));
        tooBig.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(tooBig)).GetProperty("error").GetString().Should().Contain("16 KB");
        (await Json(await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/layouts", alice)))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task A_person_keeps_at_most_50_named_layouts_but_may_still_overwrite_one()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var alice = Token(factory, "alice");
        for (var i = 0; i < 50; i++)
            (await client.SendAsync(Authed(HttpMethod.Put, $"/api/v1/layouts/L{i}", alice, Layout()))).StatusCode.Should().Be(HttpStatusCode.OK);

        var refused = await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/One more", alice, Layout()));
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Json(refused)).GetProperty("error").GetString().Should().Contain("50");
        (await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/L7", alice, Layout("git")))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Without_view_own_the_layouts_are_forbidden()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var noScope = Token(factory, "alice", "execute_agents");
        (await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/layouts/current", noScope))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/Reviewing", noScope, Layout()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_server_without_SQLite_says_layouts_cannot_be_saved()
    {
        using var factory = CreateFactory(backend: "JsonFile");
        var client = factory.CreateClient();
        var alice = Token(factory, "alice");
        var current = await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/layouts/current", alice));
        current.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(current)).GetProperty("layout").ValueKind.Should().Be(JsonValueKind.Null);
        (await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/layouts/Reviewing", alice, Layout()))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
