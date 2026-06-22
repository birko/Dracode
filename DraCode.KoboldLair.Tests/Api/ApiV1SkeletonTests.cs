using System.Net;
using System.Net.Http.Headers;
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
/// Coverage for the /api/v1 minimal-API skeleton (TASK-042): the group enforces auth (401 vs 200
/// via the permission pipeline) and the built-in OpenAPI document is generated at
/// /api/v1/openapi.json. Mirrors the JwtMiddlewareTests host harness.
/// </summary>
public class ApiV1SkeletonTests
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var projectsPath = Path.Combine(Path.GetTempPath(), "kl-apiv1-tests", Guid.NewGuid().ToString("N"));
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["KoboldLair:Data:DefaultBackend"] = "JsonFile",
            ["KoboldLair:ProjectsPath"] = projectsPath,
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
            ["sub"] = "11111111-1111-1111-1111-111111111111",
            ["scope"] = scope
        }).Token;

    [Fact]
    public async Task Api_v1_endpoint_without_token_returns_401()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/agents/active");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Api_v1_endpoint_with_valid_token_returns_200()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, KoboldLairPermissionChecker.ViewOwn);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/agents/active");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApi_document_is_generated_and_anonymous()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        // No token — the spec route is anonymous so a browser can load the docs.
        var response = await client.GetAsync("/api/v1/openapi.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("/api/v1/agents/active", "the generated spec must document the skeleton endpoint");
    }
}
