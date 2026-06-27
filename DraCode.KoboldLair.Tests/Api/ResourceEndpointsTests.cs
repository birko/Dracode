using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Birko.Security;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Server.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Api;

/// <summary>
/// Coverage for the /api/v1 resource surface (TASK-043): projects, specification, features, tasks and
/// plans. Verifies per-verb behavior, owner-scoping (a caller never sees/reads another's project → 404,
/// admin <c>?scope=all</c> sees all), and that the handlers wrap the real services end-to-end. Runs on the
/// SQLite backend with a throwaway temp DB so the task/plan repositories (null under JsonFile) are wired.
/// Mirrors <see cref="RunsEndpointsTests"/>'s WebApplicationFactory harness.
/// </summary>
public class ResourceEndpointsTests : IDisposable
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private static readonly string UserA = "11111111-1111-1111-1111-111111111111";
    private static readonly string UserB = "22222222-2222-2222-2222-222222222222";
    // Owner actions need manage_projects + execute_agents (retry) alongside view_own. The permission claim
    // is comma-split (Program.cs), so a multi-permission token is comma-joined.
    private const string OwnerScopes = "view_own,manage_projects,execute_agents";

    private readonly string _projectsPath =
        Path.Combine(Path.GetTempPath(), "kl-resource-tests", Guid.NewGuid().ToString("N"));

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

    private static string MintToken(WebApplicationFactory<Program> factory, string sub, string scope) =>
        factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = sub,
            ["scope"] = scope
        }).Token;

    private static HttpRequestMessage Authed(HttpMethod method, string uri, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, uri) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    private static async Task<string> CreateProject(HttpClient client, string token, string name, string? spec = null)
    {
        var resp = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/projects", token,
            new { name, specificationContent = spec }));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Post_projects_without_token_returns_401()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/projects", new { name = "x" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_then_list_round_trips_for_the_owner()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, OwnerScopes);

        var id = await CreateProject(client, token, "Alpha");

        var list = await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/projects", token));
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        doc.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetString())
            .Should().Contain(id);
    }

    [Fact]
    public async Task Project_listing_is_owner_scoped_and_admin_sees_all()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var tokenA = MintToken(factory, UserA, OwnerScopes);
        var tokenB = MintToken(factory, UserB, OwnerScopes);
        var admin = MintToken(factory, "33333333-3333-3333-3333-333333333333", "*");

        var idA = await CreateProject(client, tokenA, "OwnedByA");

        // B's list must not contain A's project.
        var bList = await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/projects", tokenB));
        using (var doc = JsonDocument.Parse(await bList.Content.ReadAsStringAsync()))
            doc.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetString())
                .Should().NotContain(idA);

        // B cannot read A's project directly → 404 (not 403).
        var bGet = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{idA}", tokenB));
        bGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Admin ?scope=all sees A's project.
        var adminList = await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/projects?scope=all", admin));
        using (var doc = JsonDocument.Parse(await adminList.Content.ReadAsStringAsync()))
            doc.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetString())
                .Should().Contain(idA);
    }

    [Fact]
    public async Task Delete_project_removes_it()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, OwnerScopes);

        var id = await CreateProject(client, token, "ToDelete");
        var del = await client.SendAsync(Authed(HttpMethod.Delete, $"/api/v1/projects/{id}", token));
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{id}", token));
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_specification_bumps_version_and_get_round_trips()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, OwnerScopes);

        var id = await CreateProject(client, token, "Spec", spec: "v1");

        var put = await client.SendAsync(Authed(HttpMethod.Put, $"/api/v1/projects/{id}/specification", token,
            new { content = "v2 body" }));
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await put.Content.ReadAsStringAsync()))
            doc.RootElement.GetProperty("version").GetInt32().Should().BeGreaterThan(1);

        var get = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{id}/specification", token));
        using (var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync()))
            doc.RootElement.GetProperty("content").GetString().Should().Be("v2 body");
    }

    [Fact]
    public async Task Feature_create_list_delete_round_trips()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, OwnerScopes);

        var id = await CreateProject(client, token, "Feat", spec: "# spec");

        var create = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/projects/{id}/features", token,
            new { name = "Login", description = "auth", priority = "high" }));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        string featureId;
        using (var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync()))
            featureId = doc.RootElement.GetProperty("id").GetString()!;

        var list = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{id}/features", token));
        using (var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
            doc.RootElement.EnumerateArray().Should().ContainSingle()
                .Which.GetProperty("name").GetString().Should().Be("Login");

        var del = await client.SendAsync(Authed(HttpMethod.Delete, $"/api/v1/projects/{id}/features/{featureId}", token));
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{id}/features", token));
        using (var doc = JsonDocument.Parse(await after.Content.ReadAsStringAsync()))
            doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Tasks_list_is_empty_and_unknown_task_is_404()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, OwnerScopes);

        var id = await CreateProject(client, token, "Tasks");

        var list = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{id}/tasks", token));
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
            doc.RootElement.GetArrayLength().Should().Be(0);

        var unknown = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/tasks/{Guid.NewGuid()}", token));
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Retry_resets_a_failed_task_and_priority_override_persists()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, OwnerScopes);

        var projectId = await CreateProject(client, token, "Exec");

        // Seed a Failed task directly via the repository (no Drake/LLM in tests).
        var taskRepo = factory.Services.GetRequiredService<ITaskRepository>();
        var taskId = await taskRepo.AddTaskAsync(projectId, "backend",
            new TaskRecord { Task = "do thing", ProjectId = projectId, Status = TaskStatus.Failed, ErrorMessage = "boom" });

        var retry = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/tasks/{taskId}/retry", token));
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        (await taskRepo.GetByIdAsync(taskId))!.Status.Should().Be(TaskStatus.Unassigned);

        var prio = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/tasks/{taskId}/priority", token,
            new { priority = "critical" }));
        prio.StatusCode.Should().Be(HttpStatusCode.OK);
        (await taskRepo.GetByIdAsync(taskId))!.Priority.Should().Be(TaskPriority.Critical);
    }

    [Fact]
    public async Task Retry_on_non_failed_task_returns_400()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, OwnerScopes);

        var projectId = await CreateProject(client, token, "NoRetry");
        var taskRepo = factory.Services.GetRequiredService<ITaskRepository>();
        var taskId = await taskRepo.AddTaskAsync(projectId, "backend",
            new TaskRecord { Task = "ok", ProjectId = projectId, Status = TaskStatus.Unassigned });

        var retry = await client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/tasks/{taskId}/retry", token));
        retry.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Plans_list_is_empty_for_a_fresh_project()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, OwnerScopes);

        var id = await CreateProject(client, token, "Plans");

        var list = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{id}/plans", token));
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        doc.RootElement.GetArrayLength().Should().Be(0);
    }
}
