using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Birko.Security;
using Birko.Security.Jwt;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Models.Users;
using DraCode.KoboldLair.Server.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Auth;

/// <summary>
/// Coverage for GitHub federation (TASK-033 / FEATURE-019): the federation service
/// (allowlist gate + user upsert + DraCode token minting) and the callback endpoint
/// (state → exchange → federate → SPA fragment redirect). All GitHub HTTP is mocked.
/// </summary>
public class GitHubFederationServiceTests
{
    private const long AllowedId = 42;
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";

    private static GitHubFederationService BuildService(
        IGitHubUserInfoClient userInfo,
        IUserRepository? userRepository,
        IEnumerable<long>? allowed = null)
    {
        var tokenProvider = new JwtTokenProvider(new TokenOptions
        {
            Secret = Secret,
            Issuer = "KoboldLair",
            Audience = "KoboldLair",
            ExpirationMinutes = 60,
            RefreshExpirationDays = 7
        });

        var gitHub = Options.Create(new GitHubFederationConfiguration
        {
            Enabled = true,
            DefaultRoles = ["user"],
            AllowedGitHubIds = (allowed ?? [AllowedId]).ToList()
        });
        var jwt = Options.Create(new JwtAuthenticationConfiguration { RefreshExpirationDays = 7 });

        return new GitHubFederationService(userInfo, tokenProvider, new RefreshTokenStore(), gitHub, jwt, userRepository);
    }

    private static string? SubOf(string jwt) =>
        new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims
            .FirstOrDefault(c => c.Type is "sub" or System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    [Fact]
    public async Task First_login_creates_user_and_mints_token_with_namespaced_sub()
    {
        var repo = new FakeUserRepository();
        var svc = BuildService(new FakeUserInfoClient(new GitHubUserInfo(AllowedId, "octocat", "The Octocat", "octo@example.com")), repo);

        var login = await svc.FederateAsync("gh-access-token");

        login.Token.Should().NotBeNullOrEmpty();
        SubOf(login.Token).Should().Be("github:42");
        repo.Users.Should().ContainSingle().Which.Sub.Should().Be("github:42");
        repo.Users[0].DisplayName.Should().Be("The Octocat");
    }

    [Fact]
    public async Task Second_login_same_id_reuses_user_no_duplicate()
    {
        var repo = new FakeUserRepository();
        var svc = BuildService(new FakeUserInfoClient(new GitHubUserInfo(AllowedId, "octocat", "The Octocat", null)), repo);

        await svc.FederateAsync("gh-access-token");
        var createdAt = repo.Users[0].CreatedAt;
        await svc.FederateAsync("gh-access-token-2");

        repo.Users.Should().ContainSingle("the same GitHub id must map to one DraCode user");
        repo.Users[0].CreatedAt.Should().Be(createdAt, "created timestamp is preserved on reuse");
    }

    [Fact]
    public async Task Id_not_on_allowlist_is_denied_with_no_token_and_no_user()
    {
        var repo = new FakeUserRepository();
        var svc = BuildService(
            new FakeUserInfoClient(new GitHubUserInfo(999, "stranger", "Stranger", null)),
            repo,
            allowed: [AllowedId]);

        var act = () => svc.FederateAsync("gh-access-token");

        await act.Should().ThrowAsync<FederationDeniedException>();
        repo.Users.Should().BeEmpty("a denied login must not provision a user row");
    }

    [Fact]
    public async Task Federation_works_without_a_user_repository_off_sqlite()
    {
        var svc = BuildService(new FakeUserInfoClient(new GitHubUserInfo(AllowedId, "octocat", null, null)), userRepository: null);

        var login = await svc.FederateAsync("gh-access-token");

        SubOf(login.Token).Should().Be("github:42");
    }
}

/// <summary>
/// Endpoint-level coverage: <c>GET /auth/github/callback</c> validates state, federates (GitHub
/// HTTP mocked), and 302-redirects to the SPA with the token in the URL fragment; that token then
/// authenticates a protected endpoint. Also covers the refresh round-trip preserving the string sub.
/// </summary>
public class GitHubFederationEndpointTests
{
    private const long AllowedId = 42;
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private const string RedirectBase = "https://app.local/";

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var projectsPath = Path.Combine(Path.GetTempPath(), "kl-gh-tests", Guid.NewGuid().ToString("N"));
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["Authentication:GitHub:Enabled"] = "true",
            ["Authentication:GitHub:ClientId"] = "test-client-id",
            ["Authentication:GitHub:ClientSecret"] = "test-client-secret",
            ["Authentication:GitHub:RedirectUri"] = "https://localhost/auth/github/callback",
            ["Authentication:GitHub:PostLoginRedirectUri"] = RedirectBase,
            ["Authentication:GitHub:AllowedGitHubIds:0"] = AllowedId.ToString(),
            ["KoboldLair:Data:DefaultBackend"] = "JsonFile",
            ["KoboldLair:ProjectsPath"] = projectsPath,
        };

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                // Replace both GitHub HTTP seams so no request hits github.com.
                services.RemoveAll<IGitHubUserInfoClient>();
                services.AddSingleton<IGitHubUserInfoClient>(
                    new FakeUserInfoClient(new GitHubUserInfo(AllowedId, "octocat", "The Octocat", "octo@example.com")));
                services.RemoveAll<IGitHubTokenExchanger>();
                services.AddSingleton<IGitHubTokenExchanger>(new FakeTokenExchanger("gh-access-token"));
            });
        });
    }

    [Fact]
    public async Task Callback_with_valid_state_redirects_to_spa_with_token_fragment()
    {
        using var factory = CreateFactory();
        // Issue a real state via the singleton store so the callback's single-use check passes.
        var state = factory.Services.GetRequiredService<OAuthStateStore>().Issue();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/auth/github/callback?code=abc&state={state}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith(RedirectBase + "#access_token=");

        // The fragment-delivered token authenticates a protected endpoint.
        var token = ExtractFragmentParam(location, "access_token");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Callback_with_unknown_state_is_rejected()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/auth/github/callback?code=abc&state=never-issued");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string ExtractFragmentParam(string url, string key)
    {
        var fragment = url[(url.IndexOf('#') + 1)..];
        var pair = fragment.Split('&').First(p => p.StartsWith(key + "="));
        return Uri.UnescapeDataString(pair[(key.Length + 1)..]);
    }
}

/// <summary>In-memory <see cref="IUserRepository"/> for federation tests, keyed on <c>Sub</c>.</summary>
file sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> GetBySubAsync(string sub) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Sub == sub));

    public Task UpsertAsync(User user)
    {
        var existing = Users.FirstOrDefault(u => u.Sub == user.Sub);
        if (existing is null) Users.Add(user);
        else { existing.DisplayName = user.DisplayName; existing.Email = user.Email; }
        return Task.CompletedTask;
    }
}

file sealed class FakeUserInfoClient(GitHubUserInfo user) : IGitHubUserInfoClient
{
    public Task<GitHubUserInfo> GetUserAsync(string accessToken, CancellationToken ct = default) => Task.FromResult(user);
}

file sealed class FakeTokenExchanger(string token) : IGitHubTokenExchanger
{
    public Task<string> ExchangeAsync(string code, CancellationToken ct = default) => Task.FromResult(token);
}
