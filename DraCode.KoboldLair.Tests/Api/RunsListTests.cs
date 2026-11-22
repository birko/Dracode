using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Birko.Security;
using DraCode.KoboldLair.Services;
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
/// The run list (TASK-155): <c>GET /api/v1/runs</c> gives the caller's runs newest first, with what each is for (the
/// project and task of a project run, an ad-hoc run's prompt); <c>?projectId=</c> keeps that project's runs, with
/// <c>&amp;adhoc=true</c> the caller's ad-hoc ones too; another owner's runs are not listed and their project reads as
/// 404; an admin sees all with <c>?scope=all</c>. Runs are put in the registry directly (no Kobold, no LLM).
/// </summary>
public class RunsListTests : IDisposable
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private const string GitHubOwner = "github:4242";
    private const string OtherOwner = "22222222-2222-2222-2222-222222222222";
    private const string OwnerScopes = "view_own,manage_projects,execute_agents";

    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-runs-list-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_projectsPath)) Directory.Delete(_projectsPath, recursive: true); } catch { }
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["KoboldLair:Data:DefaultBackend"] = "JsonFile",
            ["KoboldLair:ProjectsPath"] = _projectsPath,
        };
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
    }

    private static string Token(WebApplicationFactory<Program> factory, string sub, string scope = OwnerScopes) =>
        factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = sub,
            ["scope"] = scope
        }).Token;

    private static HttpRequestMessage Authed(HttpMethod method, string uri, string token) =>
        new(method, uri) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };

    private static async Task<string> CreateProject(HttpClient client, string token, string name)
    {
        var request = Authed(HttpMethod.Post, "/api/v1/projects", token);
        request.Content = JsonContent.Create(new { name });
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<List<JsonElement>> List(HttpClient client, string token, string query = "")
    {
        var response = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/runs{query}", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static string? Str(JsonElement e, string name) =>
        e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).GetString();

    [Fact]
    public async Task The_owner_lists_their_runs_newest_first_and_a_project_with_or_without_their_ad_hoc_runs()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = Token(factory, GitHubOwner);
        var shop = await CreateProject(client, token, "Shop");
        var blog = await CreateProject(client, token, "Blog");
        var registry = factory.Services.GetRequiredService<RunRegistry>();

        registry.Register(Guid.NewGuid(), GitHubOwner, "project", shop, "task-1");
        await Task.Delay(15);
        registry.Register(Guid.NewGuid(), GitHubOwner, "adhoc", prompt: "Rename the README\nand fix its links");
        await Task.Delay(15);
        registry.Register(Guid.NewGuid(), GitHubOwner, "project", blog, "task-2");

        var all = await List(client, token);
        all.Select(r => Str(r, "mode")).Should().Equal("project", "adhoc", "project"); // newest first
        Str(all[0], "projectId").Should().Be(blog);
        Str(all[1], "projectId").Should().BeNull();
        Str(all[1], "prompt").Should().Be("Rename the README", "the first line is kept");
        Str(all[2], "taskId").Should().Be("task-1");
        Str(all[2], "status").Should().Be("pending");

        (await List(client, token, $"?projectId={shop}")).Select(r => Str(r, "taskId")).Should().Equal("task-1");
        (await List(client, token, $"?projectId={shop}&adhoc=true")).Select(r => Str(r, "mode")).Should().Equal("adhoc", "project");
    }

    [Fact]
    public async Task Another_owners_runs_are_not_listed_their_project_reads_as_404_and_an_admin_sees_all()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var mine = Token(factory, GitHubOwner);
        var theirs = Token(factory, OtherOwner);
        var shop = await CreateProject(client, mine, "Shop");
        var secret = await CreateProject(client, theirs, "Secret");
        var registry = factory.Services.GetRequiredService<RunRegistry>();
        registry.Register(Guid.NewGuid(), GitHubOwner, "project", shop, "mine");
        registry.Register(Guid.NewGuid(), OtherOwner, "project", secret, "theirs");
        registry.Register(Guid.NewGuid(), OtherOwner, "adhoc", prompt: "their ad-hoc");

        (await List(client, mine)).Select(r => Str(r, "taskId")).Should().Equal("mine");
        (await List(client, mine, "?scope=all")).Select(r => Str(r, "taskId")).Should().Equal(new[] { "mine" },
            "scope=all means nothing to a non-admin");
        (await List(client, mine, $"?projectId={shop}&adhoc=true")).Select(r => Str(r, "taskId")).Should().Equal(new[] { "mine" },
            "another owner's ad-hoc run is not the caller's");
        var asked = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/runs?projectId={secret}", mine));
        asked.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var admin = Token(factory, "admin", "*");
        (await List(client, admin, "?scope=all")).Should().HaveCount(3);
        (await List(client, admin, $"?scope=all&projectId={secret}")).Select(r => Str(r, "taskId")).Should().Equal("theirs");
    }
}
