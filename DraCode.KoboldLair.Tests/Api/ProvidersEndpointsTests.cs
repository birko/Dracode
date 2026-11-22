using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Birko.Security;
using DraCode.KoboldLair.Server.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DraCode.KoboldLair.Tests.Api;

/// <summary>
/// Coverage for the admin /api/v1/providers surface (TASK-073 stage 3): the routes are mapped only in
/// provider-config DB mode (master key configured), gated by ManageConfig, and an API key is write-only —
/// set via PATCH, never returned by any GET (only a hasKey flag). Mirrors ApiV1SkeletonTests' host harness.
/// </summary>
public class ProvidersEndpointsTests
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private static readonly string User = "11111111-1111-1111-1111-111111111111";

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var id = Guid.NewGuid().ToString("N");
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["KoboldLair:Data:DefaultBackend"] = "JsonFile",
            ["KoboldLair:ProjectsPath"] = Path.Combine(Path.GetTempPath(), $"kl-prov-{id}"),
            // Absolute path → ResolveSqLitePath returns it verbatim (isolated provider-config DB per test).
            ["KoboldLair:Data:SqLitePath"] = Path.Combine(Path.GetTempPath(), $"kl-prov-{id}.db"),
            // Presence of a master key flips ProviderConfigurationService to DB mode + maps the admin routes.
            ["KoboldLair:ProviderConfig:MasterKey"] = "test-provider-master-key",
        };

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
    }

    private static string MintToken(WebApplicationFactory<Program> factory, string scope) =>
        factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = User,
            ["scope"] = scope
        }).Token;

    private static HttpRequestMessage Req(HttpMethod method, string uri, string token, object? body = null)
    {
        var r = new HttpRequestMessage(method, uri) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) r.Content = JsonContent.Create(body);
        return r;
    }

    [Fact]
    public async Task Listing_providers_requires_manage_config()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        (await client.GetAsync("/api/v1/providers")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "no token");

        var viewOwn = MintToken(factory, KoboldLairPermissionChecker.ViewOwn);
        (await client.SendAsync(Req(HttpMethod.Get, "/api/v1/providers", viewOwn))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "ViewOwn is not ManageConfig");

        var admin = MintToken(factory, KoboldLairPermissionChecker.ManageConfig);
        (await client.SendAsync(Req(HttpMethod.Get, "/api/v1/providers", admin))).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Created_provider_with_a_key_never_echoes_the_key()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var admin = MintToken(factory, KoboldLairPermissionChecker.ManageConfig);

        var create = await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers", admin, new
        {
            name = "claude", type = "claude", displayName = "Claude", enabled = true,
            requiresApiKey = true, defaultModel = "claude-sonnet-4-6", compatibleAgents = new[] { "all" }
        }));
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        var setKey = await client.SendAsync(Req(HttpMethod.Patch, "/api/v1/providers/claude/key", admin, new { apiKey = "sk-ant-SUPERSECRET" }));
        setKey.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/providers", admin));
        var body = await list.Content.ReadAsStringAsync();
        body.Should().Contain("claude");
        body.Should().Contain("\"hasKey\":true");
        body.Should().NotContain("sk-ant-SUPERSECRET", "the API key must never be returned");
        body.Should().NotContain("ApiKeyCiphertext", "the ciphertext must never be returned either");
    }

    [Fact]
    public async Task Provider_models_can_be_added_and_listed()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var admin = MintToken(factory, KoboldLairPermissionChecker.ManageConfig);

        await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers", admin, new { name = "openai", type = "openai" }));
        await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers/openai/models", admin, new { modelId = "gpt-4o" }));
        await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers/openai/models", admin, new { modelId = "gpt-4o-mini" }));

        var get = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/providers/openai", admin));
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await get.Content.ReadAsStringAsync();
        body.Should().Contain("gpt-4o");
        body.Should().Contain("gpt-4o-mini");
    }

    [Fact]
    public async Task Agent_assignment_can_be_set_and_read_back()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var admin = MintToken(factory, KoboldLairPermissionChecker.ManageConfig);

        await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers", admin, new
        {
            name = "claude", type = "claude", enabled = true, compatibleAgents = new[] { "all" }, defaultModel = "claude-sonnet-4-6"
        }));

        var put = await client.SendAsync(Req(HttpMethod.Put, "/api/v1/providers/settings/agent", admin,
            new { agentType = "dragon", provider = "claude", model = "claude-sonnet-4-6" }));
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/providers/settings", admin));
        var body = await get.Content.ReadAsStringAsync();
        body.Should().Contain("claude");
    }

    [Fact]
    public async Task Setting_an_agent_to_an_unknown_provider_is_a_400()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var admin = MintToken(factory, KoboldLairPermissionChecker.ManageConfig);

        var put = await client.SendAsync(Req(HttpMethod.Put, "/api/v1/providers/settings/agent", admin,
            new { agentType = "dragon", provider = "does-not-exist", model = "x" }));
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An OpenAI-compatible model on localhost: answers "OK" for <paramref name="goodKey"/>, 401 for any other key.</summary>
    private sealed class FakeModel : IDisposable
    {
        private readonly HttpListener _listener = new();
        public string BaseUrl { get; }
        public int Calls;

        public FakeModel(string goodKey)
        {
            var port = FreePort();
            BaseUrl = $"http://localhost:{port}";
            _listener.Prefixes.Add(BaseUrl + "/");
            _listener.Start();
            _ = Task.Run(async () =>
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await _listener.GetContextAsync(); } catch { return; }
                    Interlocked.Increment(ref Calls);
                    var ok = ctx.Request.Headers["Authorization"] == $"Bearer {goodKey}";
                    var body = ok
                        ? """{"id":"x","object":"chat.completion","choices":[{"index":0,"message":{"role":"assistant","content":"OK"},"finish_reason":"stop"}],"usage":{"prompt_tokens":5,"completion_tokens":1,"total_tokens":6}}"""
                        : """{"error":{"message":"Incorrect API key provided","type":"invalid_request_error"}}""";
                    ctx.Response.StatusCode = ok ? 200 : 401;
                    ctx.Response.ContentType = "application/json";
                    var bytes = Encoding.UTF8.GetBytes(body);
                    await ctx.Response.OutputStream.WriteAsync(bytes);
                    ctx.Response.Close();
                }
            });
        }

        private static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public void Dispose() { _listener.Stop(); _listener.Close(); }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();

    [Fact]
    public async Task Validate_reaches_the_provider_and_reports_a_wrong_key_then_a_right_one()
    {
        // TASK-161: the Settings screen's Validate is a real connection test, not only "a key is set"
        using var model = new FakeModel(goodKey: "sk-right");
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var admin = MintToken(factory, KoboldLairPermissionChecker.ManageConfig);

        (await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers", admin, new
        {
            name = "local", type = "vllm", enabled = true, requiresApiKey = true, baseUrl = model.BaseUrl,
            defaultModel = "m1", compatibleAgents = new[] { "all" }
        }))).StatusCode.Should().Be(HttpStatusCode.OK);

        // No key yet: the configuration check answers, the model is not called
        var noKey = await Json(await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers/local/validate", admin)));
        noKey.GetProperty("valid").GetBoolean().Should().BeFalse();
        noKey.GetProperty("message").GetString().Should().Contain("API key required");
        model.Calls.Should().Be(0);

        (await client.SendAsync(Req(HttpMethod.Patch, "/api/v1/providers/local/key", admin, new { apiKey = "sk-wrong" }))).EnsureSuccessStatusCode();
        var wrong = await Json(await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers/local/validate", admin)));
        wrong.GetProperty("valid").GetBoolean().Should().BeFalse("the provider refused the key");
        wrong.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        model.Calls.Should().BeGreaterThan(0, "validation talked to the provider");

        (await client.SendAsync(Req(HttpMethod.Patch, "/api/v1/providers/local/key", admin, new { apiKey = "sk-right" }))).EnsureSuccessStatusCode();
        var right = await Json(await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers/local/validate", admin)));
        right.GetProperty("valid").GetBoolean().Should().BeTrue(right.GetProperty("message").GetString());

        (await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers/nope/validate", admin))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        var viewer = MintToken(factory, KoboldLairPermissionChecker.ViewOwn);
        (await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers/local/validate", viewer))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_provider_is_edited_with_PUT_by_its_name_and_keeps_its_key()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var admin = MintToken(factory, KoboldLairPermissionChecker.ManageConfig);
        (await client.SendAsync(Req(HttpMethod.Post, "/api/v1/providers", admin, new
        {
            name = "claude", type = "claude", displayName = "Claude", requiresApiKey = true, defaultModel = "a"
        }))).EnsureSuccessStatusCode();
        (await client.SendAsync(Req(HttpMethod.Patch, "/api/v1/providers/claude/key", admin, new { apiKey = "sk-ant-x" }))).EnsureSuccessStatusCode();

        var put = await client.SendAsync(Req(HttpMethod.Put, "/api/v1/providers/claude", admin, new
        {
            name = "ignored", type = "claude", displayName = "Claude (work)", requiresApiKey = true, defaultModel = "b",
            baseUrl = "https://example.invalid"
        }));
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var view = await Json(await client.SendAsync(Req(HttpMethod.Get, "/api/v1/providers/claude", admin)));
        view.GetProperty("displayName").GetString().Should().Be("Claude (work)");
        view.GetProperty("defaultModel").GetString().Should().Be("b");
        view.GetProperty("baseUrl").GetString().Should().Be("https://example.invalid");
        view.GetProperty("hasKey").GetBoolean().Should().BeTrue("editing the metadata keeps the key");
        (await client.SendAsync(Req(HttpMethod.Get, "/api/v1/providers/ignored", admin))).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "the route's name wins");
    }

    [Fact]
    public async Task Capabilities_say_who_may_manage_providers_and_whether_they_are_on()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var admin = await Json(await client.SendAsync(Req(HttpMethod.Get, "/api/v1/settings/capabilities",
            MintToken(factory, KoboldLairPermissionChecker.ManageConfig))));
        admin.GetProperty("canManageProviders").GetBoolean().Should().BeTrue();
        admin.GetProperty("providersEnabled").GetBoolean().Should().BeTrue();

        var viewer = await Json(await client.SendAsync(Req(HttpMethod.Get, "/api/v1/settings/capabilities",
            MintToken(factory, KoboldLairPermissionChecker.ViewOwn))));
        viewer.GetProperty("canManageProviders").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Without_a_master_key_capabilities_say_providers_are_off_and_why()
    {
        using var factory = CreateFactory().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["KoboldLair:ProviderConfig:MasterKey"] = "" })));
        var client = factory.CreateClient();
        var token = MintToken(factory, KoboldLairPermissionChecker.ManageConfig);

        var caps = await Json(await client.SendAsync(Req(HttpMethod.Get, "/api/v1/settings/capabilities", token)));
        caps.GetProperty("providersEnabled").GetBoolean().Should().BeFalse();
        caps.GetProperty("providersHint").GetString().Should().Contain("MasterKey");
        (await client.SendAsync(Req(HttpMethod.Get, "/api/v1/providers", token))).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "the routes are not mapped");
    }
}
