using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Birko.Security;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Server.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Api;

/// <summary>
/// What waits on the caller (TASK-147, FEATURE-026 D10): open decisions, failed and blocked tasks and feature branches
/// ready to merge, across the caller's projects, newest first; owner-scoped (another owner's are not listed, and asking
/// for their project is 404), all for an admin with <c>?scope=all</c>. An answered decision is gone.
/// </summary>
public class NeedsYouEndpointsTests : IDisposable
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private const string GitHubOwner = "github:4242";
    private const string OtherOwner = "22222222-2222-2222-2222-222222222222";
    private const string OwnerScopes = "view_own,manage_projects,execute_agents";

    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-needs-you-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
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
        var response = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/needs-you{query}", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private static async Task AddTask(WebApplicationFactory<Program> factory, string projectId, string task, TaskStatus status, DateTime at)
    {
        var tasks = factory.Services.GetRequiredService<ITaskRepository>();
        await tasks.AddTaskAsync(projectId, "backend", new TaskRecord
        {
            Task = task, ProjectId = projectId, Status = status, CreatedAt = at, UpdatedAt = at
        });
    }

    [Fact]
    public async Task The_owner_gets_decisions_failed_and_blocked_tasks_and_ready_branches_newest_first()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = Token(factory, GitHubOwner);
        var shop = await CreateProject(client, token, "Shop");
        var blog = await CreateProject(client, token, "Blog");
        var now = DateTime.UtcNow;

        await AddTask(factory, shop, "## Build the API\nDetails", TaskStatus.Failed, now.AddMinutes(-30));
        await AddTask(factory, shop, "Wire the frontend", TaskStatus.BlockedByFailure, now.AddMinutes(-20));
        await AddTask(factory, shop, "Write the README", TaskStatus.Done, now.AddMinutes(-10));
        await AddTask(factory, blog, "Draft a post", TaskStatus.Working, now.AddMinutes(-5));
        var decisions = factory.Services.GetRequiredService<SqlPendingDecisionRepository>();
        var decision = new PendingDecision
        {
            ProjectId = blog, TaskId = "t-9", Source = PendingDecisionSource.AskHuman, Question = "Which theme?", CreatedAt = now.AddMinutes(-1)
        };
        await decisions.CreateAsync(decision);
        factory.Services.GetRequiredService<ProjectNotificationService>()
            .NotifyFeatureBranchReady("Shop", "Checkout", "feature/checkout");

        var items = await List(client, token);

        items.Select(i => (Str(i, "kind"), Str(i, "projectName"), Str(i, "title"))).Should().Equal(
            ("merge_ready", "Shop", "Feature \"Checkout\" is complete! Branch `feature/checkout` is ready for merge into main."),
            ("decision", "Blog", "Which theme?"),
            ("blocked_task", "Shop", "Wire the frontend"),
            ("failed_task", "Shop", "Build the API"));
        foreach (var item in items)
            item.GetProperty("createdAt").GetDateTime().Should().BeAfter(now.AddHours(-1));
        Str(items[1], "projectId").Should().Be(blog);

        // An answered decision is gone; so is a read ready-branch notification.
        await decisions.ResolveAsync(decision.Id, PendingDecisionResolution.Answered, "Dark", DateTime.UtcNow);
        (await List(client, token)).Select(i => Str(i, "kind")).Should().NotContain("decision");

        (await List(client, token, $"?projectId={blog}")).Should().BeEmpty();
        (await List(client, token, $"?projectId={shop}")).Should().HaveCount(3);
    }

    [Fact]
    public async Task An_answer_an_agent_gave_is_listed_until_the_owner_keeps_or_overturns_it()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = Token(factory, GitHubOwner);
        var shop = await CreateProject(client, token, "Shop");
        var decisions = factory.Services.GetRequiredService<SqlPendingDecisionRepository>();
        var decision = new PendingDecision
        {
            ProjectId = shop, TaskId = "t-1", Source = PendingDecisionSource.AskHuman,
            Question = new AskHumanQuestion("Which database?", ["SQLite", "Postgres"], false, null).ToJson(),
            Resolution = "SQLite", ResolutionKind = PendingDecisionResolution.Answered, ResolvedAt = DateTime.UtcNow,
            AnsweredBy = QuestionLevel.Supervisor, AnswerReason = "the plan says SQLite", AgentAnswer = "SQLite"
        };
        await decisions.CreateAsync(decision);

        var item = (await List(client, token)).Should().ContainSingle().Subject;
        Str(item, "kind").Should().Be("agent_answer");
        Str(item, "id").Should().Be(decision.Id);
        Str(item, "title").Should().Be("Drake (the task's supervisor) answered: Which database? → SQLite");

        await decisions.ReviewAsync(decision.Id, null, DateTime.UtcNow);
        (await List(client, token)).Should().BeEmpty("a kept answer no longer waits on the owner");
    }

    [Fact]
    public async Task Another_owners_items_are_not_listed_their_project_reads_as_404_and_an_admin_sees_all()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var mine = Token(factory, GitHubOwner);
        var theirs = Token(factory, OtherOwner);
        var shop = await CreateProject(client, mine, "Shop");
        var secret = await CreateProject(client, theirs, "Secret");
        await AddTask(factory, shop, "Mine failed", TaskStatus.Failed, DateTime.UtcNow);
        await AddTask(factory, secret, "Theirs failed", TaskStatus.Failed, DateTime.UtcNow);

        (await List(client, mine)).Select(i => Str(i, "title")).Should().Equal("Mine failed");
        (await List(client, mine, "?scope=all")).Select(i => Str(i, "title")).Should().Equal(new[] { "Mine failed" },
            "scope=all means nothing to a non-admin");
        var asked = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/needs-you?projectId={secret}", mine));
        asked.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var admin = Token(factory, "admin", "*");
        (await List(client, admin, "?scope=all")).Select(i => Str(i, "title")).Should().BeEquivalentTo("Mine failed", "Theirs failed");
        (await List(client, admin, $"?projectId={secret}")).Select(i => Str(i, "title")).Should().Equal("Theirs failed");
    }
}
