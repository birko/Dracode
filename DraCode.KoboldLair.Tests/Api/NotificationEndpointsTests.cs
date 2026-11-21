using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Birko.Security;
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
using Microsoft.Extensions.Logging.Abstractions;

namespace DraCode.KoboldLair.Tests.Api;

/// <summary>
/// The caller's project notifications over REST (TASK-159, FEATURE-026 D17): the same records Dragon gets, listed unread
/// first and marked read, owner-scoped (another owner's are neither listed nor markable, 404), all for an admin with
/// <c>?scope=all</c>. Reading one keeps it, as read; Dragon's pending list no longer has it.
/// </summary>
public class NotificationEndpointsTests : IDisposable
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private const string GitHubOwner = "github:4242";
    private const string OtherOwner = "22222222-2222-2222-2222-222222222222";
    private const string OwnerScopes = "view_own,manage_projects,execute_agents";

    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-notification-tests", Guid.NewGuid().ToString("N"));

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
            ["KoboldLair:Data:DefaultBackend"] = "SqLite",
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
        var response = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/notifications{query}", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    [Fact]
    public async Task The_owner_lists_their_projects_notifications_unread_first_and_marks_one_read()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = Token(factory, GitHubOwner);
        var shopId = await CreateProject(client, token, "Shop");
        await CreateProject(client, token, "Blog");
        var notifications = factory.Services.GetRequiredService<ProjectNotificationService>();
        notifications.Notify("Shop", "feature_branch_ready", "Checkout is ready to merge");
        notifications.Notify("Blog", "project_complete", "Blog is complete");
        notifications.Notify("Shop", "escalation", "Task stalled");

        var listed = await List(client, token);
        listed.Select(n => Str(n, "message")).Should().Equal("Task stalled", "Blog is complete", "Checkout is ready to merge");
        listed.Should().OnlyContain(n => !n.GetProperty("read").GetBoolean());
        listed.First(n => Str(n, "message") == "Task stalled").GetProperty("projectId").GetString().Should().Be(shopId);

        var checkout = listed.Single(n => Str(n, "message") == "Checkout is ready to merge");
        var read = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/notifications/{Str(checkout, "id")}/read", token));
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        listed = await List(client, token);
        listed.Select(n => Str(n, "message")).Should().Equal("Task stalled", "Blog is complete", "Checkout is ready to merge");
        listed.Last().GetProperty("read").GetBoolean().Should().BeTrue("a read notification stays listed, after the unread");
        notifications.GetPendingNotifications("Shop").Select(n => n.Message).Should().Equal(new[] { "Task stalled" },
            "the same record is read for Dragon too");
    }

    [Fact]
    public async Task Another_owners_notifications_are_neither_listed_nor_markable_and_an_admin_sees_all()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var mine = Token(factory, GitHubOwner);
        var theirs = Token(factory, OtherOwner);
        await CreateProject(client, mine, "Shop");
        await CreateProject(client, theirs, "Secret");
        var notifications = factory.Services.GetRequiredService<ProjectNotificationService>();
        notifications.Notify("Shop", "escalation", "Mine");
        notifications.Notify("Secret", "escalation", "Theirs");
        var theirId = notifications.GetPendingNotifications("Secret").Single().Id;

        (await List(client, mine)).Select(n => Str(n, "message")).Should().Equal("Mine");
        (await List(client, mine, "?scope=all")).Select(n => Str(n, "message")).Should().Equal(new[] { "Mine" },
            "scope=all means nothing to a non-admin");
        var read = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/notifications/{theirId}/read", mine));
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notifications.GetPendingNotifications("Secret").Should().HaveCount(1);

        var admin = Token(factory, "admin", "*");
        (await List(client, admin, "?scope=all")).Select(n => Str(n, "message")).Should().BeEquivalentTo("Mine", "Theirs");
    }

    [Fact]
    public void A_restart_keeps_unread_notifications_when_a_new_one_arrives()
    {
        Directory.CreateDirectory(_projectsPath);
        var before = new ProjectNotificationService(NullLogger<ProjectNotificationService>.Instance, _projectsPath);
        before.Notify("Shop", "escalation", "Saved before the restart");

        var after = new ProjectNotificationService(NullLogger<ProjectNotificationService>.Instance, _projectsPath);
        after.Notify("Shop", "escalation", "Arrived after the restart");

        after.GetPendingNotifications("Shop").Select(n => n.Message)
            .Should().Equal("Saved before the restart", "Arrived after the restart");
    }

    [Fact]
    public void Only_the_newest_read_notifications_are_kept()
    {
        Directory.CreateDirectory(_projectsPath);
        var service = new ProjectNotificationService(NullLogger<ProjectNotificationService>.Instance, _projectsPath);
        for (var i = 0; i < ProjectNotificationService.KeptReadPerProject + 5; i++)
            service.Notify("Shop", "escalation", $"n{i}");
        service.Notify("Shop", "escalation", "still unread");
        var all = service.GetNotifications("Shop").Where(n => n.Message != "still unread").Select(n => n.Id).ToList();

        service.MarkAsRead("Shop", all);

        service.GetNotifications("Shop").Count(n => n.ReadAt != null).Should().Be(ProjectNotificationService.KeptReadPerProject);
        service.GetPendingNotifications("Shop").Select(n => n.Message).Should().Equal("still unread");
    }
}
