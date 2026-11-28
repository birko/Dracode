using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Birko.Security;
using Birko.Security.OAuth.Server.Stores;
using DraCode.KoboldLair.Server.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DraCode.KoboldLair.Tests.Auth;

/// <summary>
/// Service-account API keys in Settings (TASK-162): any signed-in user creates their own keys, a key's permissions are a
/// fixed set narrowed to its creator's, `GET /api/v1/keys` is owner-scoped (another owner's key reads as 404, an admin
/// sees all with scope=all), and a revoked key can no longer get a token. Runs on the SQLite backend, where the owner
/// table lives in the server's database.
/// </summary>
public class KeysEndpointsTests : IDisposable
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private const string Alice = "11111111-1111-1111-1111-111111111111";
    private const string Bob = "github:4242";
    private const string UserScopes = "view_own,manage_projects,execute_agents";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kl-keys", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private WebApplicationFactory<Program> CreateFactory(bool oauth = true)
    {
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["Authentication:OAuth:Enabled"] = oauth ? "true" : "false",
            ["Authentication:OAuth:AllowDynamicRegistration"] = "true",
            ["KoboldLair:ProjectsPath"] = _dir,
            // Absolute path: each test gets its own database (and so its own keys)
            ["KoboldLair:Data:SqLitePath"] = Path.Combine(_dir, "koboldlair.db"),
        };
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                // The OAuth stores take their database when the host is built, before this test's settings apply, so
                // they would share one file across tests and runs; give them this test's database, as the keys have
                var db = Path.Combine(_dir, "koboldlair.db");
                Directory.CreateDirectory(_dir);
                services.AddKoboldLairOAuthStores(DraCode.KoboldLair.Data.DataStores.SqLite(db));
            });
        });
    }

    private static string Token(WebApplicationFactory<Program> factory, string sub, string scope) =>
        factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = sub,
            ["scope"] = scope
        }).Token;

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string uri, string token, object? body = null)
    {
        var r = new HttpRequestMessage(method, uri) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) r.Content = JsonContent.Create(body);
        return await client.SendAsync(r);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();

    private static Task<HttpResponseMessage> TokenFor(HttpClient client, string clientId, string clientSecret) =>
        client.PostAsync("/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        }));

    [Fact]
    public async Task A_user_creates_a_key_sees_only_their_own_and_its_secret_works_until_revoked()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var alice = Token(factory, Alice, UserScopes);
        var bob = Token(factory, Bob, UserScopes);

        var created = await Send(client, HttpMethod.Post, "/api/v1/keys", alice, new { name = "alice-ci" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var key = await Json(created);
        var clientId = key.GetProperty("clientId").GetString()!;
        var secret = key.GetProperty("clientSecret").GetString()!;
        secret.Should().NotBeNullOrEmpty();
        key.GetProperty("scopes").EnumerateArray().Select(s => s.GetString()).Should()
            .BeEquivalentTo(["view_own", "manage_projects", "execute_agents"]);
        (await Send(client, HttpMethod.Post, "/api/v1/keys", bob, new { name = "bob-bot" })).StatusCode.Should().Be(HttpStatusCode.Created);

        var aliceKeys = await Json(await Send(client, HttpMethod.Get, "/api/v1/keys", alice));
        aliceKeys.EnumerateArray().Select(k => k.GetProperty("name").GetString()).Should().Equal("alice-ci");
        aliceKeys[0].TryGetProperty("clientSecret", out _).Should().BeFalse("the secret is shown only when created");
        aliceKeys[0].GetProperty("disabled").GetBoolean().Should().BeFalse();
        var bobKeys = await Json(await Send(client, HttpMethod.Get, "/api/v1/keys", bob));
        bobKeys.EnumerateArray().Select(k => k.GetProperty("name").GetString()).Should().Equal("bob-bot");

        (await TokenFor(client, clientId, secret)).StatusCode.Should().Be(HttpStatusCode.OK, "a live key gets a token");

        (await Send(client, HttpMethod.Post, $"/api/v1/keys/{clientId}/revoke", bob)).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "another owner's key does not exist for them");
        var revoked = await Send(client, HttpMethod.Post, $"/api/v1/keys/{clientId}/revoke", alice);
        revoked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(revoked)).GetProperty("disabled").GetBoolean().Should().BeTrue();

        (await TokenFor(client, clientId, secret)).StatusCode.Should().NotBe(HttpStatusCode.OK, "a revoked key gets no token");
        (await Json(await Send(client, HttpMethod.Get, "/api/v1/keys", alice)))[0].GetProperty("disabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task An_admin_sees_every_key_with_scope_all_and_may_revoke_any()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var alice = Token(factory, Alice, UserScopes);
        var admin = Token(factory, "admin-1", "*");
        var created = await Json(await Send(client, HttpMethod.Post, "/api/v1/keys", alice, new { name = "alice-ci" }));

        var mine = await Json(await Send(client, HttpMethod.Get, "/api/v1/keys", admin));
        mine.GetArrayLength().Should().Be(0, "without scope=all an admin sees their own keys");
        var all = await Json(await Send(client, HttpMethod.Get, "/api/v1/keys?scope=all", admin));
        all.EnumerateArray().Select(k => k.GetProperty("ownerId").GetString()).Should().Equal(Alice);

        (await Send(client, HttpMethod.Post, $"/api/v1/keys/{created.GetProperty("clientId").GetString()}/revoke", admin))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_key_never_holds_a_permission_its_creator_lacks()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var viewer = Token(factory, Alice, "view_own");

        var key = await Json(await Send(client, HttpMethod.Post, "/api/v1/keys", viewer, new { name = "read-only" }));
        key.GetProperty("scopes").EnumerateArray().Select(s => s.GetString()).Should().Equal("view_own");
    }

    [Fact]
    public async Task Names_are_unique_and_well_formed()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var alice = Token(factory, Alice, UserScopes);
        var bob = Token(factory, Bob, UserScopes);

        (await Send(client, HttpMethod.Post, "/api/v1/keys", alice, new { name = "shared" })).StatusCode.Should().Be(HttpStatusCode.Created);
        var dup = await Send(client, HttpMethod.Post, "/api/v1/keys", bob, new { name = "Shared" });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict, "a key acts as service:<name>, so a second one would share its identity");
        (await Send(client, HttpMethod.Post, "/api/v1/keys", alice, new { name = "has space" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Send(client, HttpMethod.Post, "/api/v1/keys", alice, new { name = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_key_cannot_take_the_name_of_a_client_registered_before_owners_were_recorded()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var alice = Token(factory, Alice, UserScopes);
        // The way a server before TASK-162 registered a client: in the OAuth store only, with no service_keys row
        var name = "legacy-bot-" + Guid.NewGuid().ToString("N")[..8];
        await factory.Services.GetRequiredService<Birko.Security.OAuth.Server.OAuthServer>().ClientRegistration.RegisterAsync(
            new Birko.Security.OAuth.Server.Endpoints.ClientRegistration.ClientRegistrationRequest
            {
                Name = name, AllowedGrantTypes = ["client_credentials"], AllowedScopes = ["view_own"]
            });

        var r = await Send(client, HttpMethod.Post, "/api/v1/keys", alice, new { name = name.ToUpperInvariant() });
        r.StatusCode.Should().Be(HttpStatusCode.Conflict, "the new key would act as that client's service:<name> and see its projects");
        (await Json(await Send(client, HttpMethod.Get, "/api/v1/keys", alice))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task A_key_registered_through_register_is_listed_for_the_admin_who_registered_it()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var admin = Token(factory, "admin-1", "manage_users");
        var reg = new HttpRequestMessage(HttpMethod.Post, "/register")
        {
            Content = JsonContent.Create(new { name = "cli-key", allowedGrantTypes = new[] { "client_credentials" }, allowedScopes = new[] { "view_own" } }),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", admin) }
        };
        (await client.SendAsync(reg)).StatusCode.Should().Be(HttpStatusCode.Created);

        var keys = await Json(await Send(client, HttpMethod.Get, "/api/v1/keys", admin));
        keys.EnumerateArray().Select(k => k.GetProperty("name").GetString()).Should().Equal("cli-key");
    }

    [Fact]
    public async Task Without_OAuth_the_keys_routes_are_off_and_the_capabilities_say_why()
    {
        using var factory = CreateFactory(oauth: false);
        var client = factory.CreateClient();
        var alice = Token(factory, Alice, UserScopes);

        (await Send(client, HttpMethod.Get, "/api/v1/keys", alice)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var caps = await Json(await Send(client, HttpMethod.Get, "/api/v1/settings/capabilities", alice));
        caps.GetProperty("keysEnabled").GetBoolean().Should().BeFalse();
        caps.GetProperty("keysHint").GetString().Should().Contain("Authentication:OAuth:Enabled");
    }
}
