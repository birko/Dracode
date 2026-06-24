using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
}
