using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

namespace DraCode.KoboldLair.Tests.Auth;

/// <summary>
/// End-to-end coverage for service-account registration + client_credentials issuance (TASK-035 /
/// FEATURE-019 D14, D15): register a confidential client, exchange client_credentials for a token
/// carrying <c>sub: "service:&lt;name&gt;"</c> + permission-constant scopes, enforce scopes at endpoints
/// (in-scope 200 / out-of-scope 403), and revoke the client.
/// </summary>
public class ServiceAccountTests
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var projectsPath = Path.Combine(Path.GetTempPath(), "kl-svc-tests", Guid.NewGuid().ToString("N"));
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["Authentication:OAuth:Enabled"] = "true",
            ["Authentication:OAuth:AllowDynamicRegistration"] = "true",
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

    private static string AdminToken(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = "11111111-1111-1111-1111-111111111111",
            ["scope"] = KoboldLairPermissionChecker.ManageUsers
        }).Token;

    /// <summary>Registers a confidential client_credentials client; returns (clientId, clientSecret).</summary>
    private static async Task<(string clientId, string clientSecret)> RegisterAsync(
        HttpClient client, string adminToken, string name, params string[] scopes)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/register")
        {
            Content = JsonContent.Create(new
            {
                name,
                allowedGrantTypes = new[] { "client_credentials" },
                allowedScopes = scopes
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        return (root.GetProperty("client_id").GetString()!, root.GetProperty("client_secret").GetString()!);
    }

    private static async Task<HttpResponseMessage> RequestClientCredentialsAsync(
        HttpClient client, string clientId, string clientSecret) =>
        await client.PostAsync("/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        }));

    private static string? SubOf(string jwt) =>
        new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims
            .FirstOrDefault(c => c.Type is "sub" or System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    [Fact]
    public async Task Register_returns_client_secret_once()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var (clientId, clientSecret) = await RegisterAsync(client, AdminToken(factory), "discord-bot", KoboldLairPermissionChecker.ViewOwn);

        clientId.Should().NotBeNullOrEmpty();
        clientSecret.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Client_credentials_token_carries_service_sub_and_scope()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var (clientId, clientSecret) = await RegisterAsync(client, AdminToken(factory), "discord-bot", KoboldLairPermissionChecker.ViewOwn);

        var response = await RequestClientCredentialsAsync(client, clientId, clientSecret);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("access_token").GetString()!;
        SubOf(token).Should().Be("service:discord-bot");
        doc.RootElement.GetProperty("scope").GetString().Should().Contain(KoboldLairPermissionChecker.ViewOwn);
    }

    [Fact]
    public async Task Scoped_token_allows_in_scope_and_rejects_out_of_scope()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        // Granted view_own only — NOT manage_users (which /register requires).
        var (clientId, clientSecret) = await RegisterAsync(client, AdminToken(factory), "ci-runner", KoboldLairPermissionChecker.ViewOwn);
        var tokenDoc = JsonDocument.Parse(await (await RequestClientCredentialsAsync(client, clientId, clientSecret)).Content.ReadAsStringAsync());
        var serviceToken = tokenDoc.RootElement.GetProperty("access_token").GetString()!;

        // In scope: whoami requires view_own.
        var inScope = new HttpRequestMessage(HttpMethod.Get, "/api/v1/whoami");
        inScope.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        (await client.SendAsync(inScope)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Out of scope: /register requires manage_users, which this token lacks.
        var outOfScope = new HttpRequestMessage(HttpMethod.Post, "/register")
        {
            Content = JsonContent.Create(new { name = "x", allowedGrantTypes = new[] { "client_credentials" }, allowedScopes = new[] { "view_own" } })
        };
        outOfScope.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        (await client.SendAsync(outOfScope)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Disabled_client_is_rejected_at_token_endpoint()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var admin = AdminToken(factory);
        var (clientId, clientSecret) = await RegisterAsync(client, admin, "discord-bot", KoboldLairPermissionChecker.ViewOwn);

        // Works before disabling.
        (await RequestClientCredentialsAsync(client, clientId, clientSecret)).StatusCode.Should().Be(HttpStatusCode.OK);

        var disable = new HttpRequestMessage(HttpMethod.Post, $"/register/{clientId}/disable");
        disable.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin);
        (await client.SendAsync(disable)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Rejected after disabling (invalid_client → 400).
        (await RequestClientCredentialsAsync(client, clientId, clientSecret)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Wrong_secret_is_rejected()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var (clientId, _) = await RegisterAsync(client, AdminToken(factory), "discord-bot", KoboldLairPermissionChecker.ViewOwn);

        var response = await RequestClientCredentialsAsync(client, clientId, "not-the-secret");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
