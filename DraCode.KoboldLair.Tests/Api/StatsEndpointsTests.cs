using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Birko.AI.Orchestration.Models;
using Birko.Security;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
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
/// A project's numbers (TASK-158): tasks by status and agent type, success rate, average duration from the plans, cost
/// by provider and model and by day from the usage records, spend against the project budget; empty data reads as
/// zeros and nulls; owner-scoped (another owner's project is 404, an admin may read it).
/// </summary>
public class StatsEndpointsTests : IDisposable
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private const string GitHubOwner = "github:4242";
    private const string OtherOwner = "22222222-2222-2222-2222-222222222222";
    private const string OwnerScopes = "view_own,manage_projects,execute_agents";

    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-stats-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_projectsPath)) Directory.Delete(_projectsPath, recursive: true); } catch { }
    }

    private WebApplicationFactory<Program> CreateFactory(double projectBudget = 0, double monthlyBudget = 0)
    {
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["KoboldLair:Data:DefaultBackend"] = "SqLite",
            ["KoboldLair:ProjectsPath"] = _projectsPath,
            ["KoboldLair:CostTracking:Budget:ProjectBudgetUsd"] = projectBudget.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["KoboldLair:CostTracking:Budget:MonthlyBudgetUsd"] = monthlyBudget.ToString(System.Globalization.CultureInfo.InvariantCulture),
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

    private static async Task<JsonElement> Stats(HttpClient client, string token, string id)
    {
        var response = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{id}/stats", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static async Task<string> AddTask(WebApplicationFactory<Program> factory, string projectId, TaskStatus status, string agent,
        DateTime? updatedAt = null) =>
        await factory.Services.GetRequiredService<ITaskRepository>().AddTaskAsync(projectId, "backend",
            new TaskRecord { Task = $"{status} by {agent}", ProjectId = projectId, Status = status, AssignedAgent = agent, UpdatedAt = updatedAt });

    private static async Task AddPlan(WebApplicationFactory<Program> factory, string projectId, string taskId, DateTime start, TimeSpan took)
    {
        var plan = new KoboldImplementationPlan
        {
            ProjectId = projectId, TaskId = taskId, TaskDescription = "work", Status = PlanStatus.Completed,
            CreatedAt = start.AddMinutes(-5), CompletedAt = start + took,
            Steps = { new ImplementationStep { Index = 1, Title = "do it", Status = StepStatus.Completed, StartedAt = start, CompletedAt = start + took } }
        };
        await factory.Services.GetRequiredService<KoboldPlanService>().SavePlanAsync(plan);
    }

    private static Task AddUsage(WebApplicationFactory<Program> factory, string projectId, string provider, string model, int tokens, double cost, DateTime at) =>
        factory.Services.GetRequiredService<SqlUsageRepository>().RecordUsageAsync(new UsageRecordEntity
        {
            ProjectId = projectId, Provider = provider, Model = model, PromptTokens = tokens / 2, CompletionTokens = tokens - tokens / 2,
            TotalTokens = tokens, EstimatedCostUsd = cost, RecordedAt = at
        });

    [Fact]
    public async Task The_owner_gets_tasks_by_status_and_agent_success_rate_duration_cost_by_model_and_day_and_the_budget()
    {
        using var factory = CreateFactory(projectBudget: 2.0);
        var client = factory.CreateClient();
        var token = Token(factory, GitHubOwner);
        var shop = await CreateProject(client, token, "Shop");
        var other = await CreateProject(client, token, "Other");

        var done1 = await AddTask(factory, shop, TaskStatus.Done, "csharp");
        var done2 = await AddTask(factory, shop, TaskStatus.Done, "csharp");
        await AddTask(factory, shop, TaskStatus.Done, "typescript");
        await AddTask(factory, shop, TaskStatus.Failed, "typescript");
        await AddTask(factory, shop, TaskStatus.Unassigned, "");
        await AddTask(factory, other, TaskStatus.Done, "python"); // another project's: not counted
        var t0 = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        await AddPlan(factory, shop, done1, t0, TimeSpan.FromSeconds(60));
        await AddPlan(factory, shop, done2, t0, TimeSpan.FromSeconds(180)); // the third Done task has no plan: not timed
        await AddUsage(factory, shop, "openai", "gpt-4o", 1000, 0.10, t0);
        await AddUsage(factory, shop, "openai", "gpt-4o", 3000, 0.30, t0.AddHours(2));
        await AddUsage(factory, shop, "claude", "sonnet", 2000, 0.60, t0.AddDays(1));
        await AddUsage(factory, other, "openai", "gpt-4o", 9999, 9.00, t0);

        var stats = await Stats(client, token, shop);

        var tasks = stats.GetProperty("tasks");
        tasks.GetProperty("total").GetInt32().Should().Be(5);
        tasks.GetProperty("byStatus").GetProperty("Done").GetInt32().Should().Be(3);
        tasks.GetProperty("byStatus").GetProperty("Failed").GetInt32().Should().Be(1);
        tasks.GetProperty("byStatus").GetProperty("Working").GetInt32().Should().Be(0, "every status is listed");
        tasks.GetProperty("successRate").GetDouble().Should().Be(75.0); // 3 done of 4 finished
        tasks.GetProperty("averageDurationSeconds").GetDouble().Should().Be(120.0); // (60 + 180) / 2, from the first step's start
        tasks.GetProperty("timedTasks").GetInt32().Should().Be(2);
        var agents = tasks.GetProperty("byAgent").EnumerateArray()
            .Select(a => (a.GetProperty("agentType").GetString(), a.GetProperty("total").GetInt32(), a.GetProperty("done").GetInt32(), a.GetProperty("failed").GetInt32()))
            .ToList();
        agents.Should().Equal(("csharp", 2, 2, 0), ("typescript", 2, 1, 1), ("unassigned", 1, 0, 0));

        var cost = stats.GetProperty("cost");
        cost.GetProperty("totalUsd").GetDouble().Should().BeApproximately(1.0, 1e-9);
        cost.GetProperty("totalTokens").GetInt64().Should().Be(6000);
        cost.GetProperty("requests").GetInt32().Should().Be(3);
        cost.GetProperty("byModel").EnumerateArray().Select(m => (m.GetProperty("provider").GetString(), m.GetProperty("model").GetString(),
            m.GetProperty("requests").GetInt32(), Math.Round(m.GetProperty("costUsd").GetDouble(), 2)))
            .Should().Equal(("claude", "sonnet", 1, 0.60), ("openai", "gpt-4o", 2, 0.40));
        cost.GetProperty("byDay").EnumerateArray().Select(d => (d.GetProperty("date").GetString(), Math.Round(d.GetProperty("costUsd").GetDouble(), 2)))
            .Should().Equal(("2026-11-20", 0.40), ("2026-11-21", 0.60));

        var budget = stats.GetProperty("budget");
        budget.GetProperty("projectBudgetUsd").GetDouble().Should().Be(2.0);
        budget.GetProperty("spentUsd").GetDouble().Should().BeApproximately(1.0, 1e-9);
        budget.GetProperty("percentUsed").GetDouble().Should().Be(50.0);
    }

    [Fact]
    public async Task A_project_with_nothing_yet_reads_as_zeros_and_no_budget()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = Token(factory, GitHubOwner);
        var id = await CreateProject(client, token, "Empty");

        var stats = await Stats(client, token, id);

        stats.GetProperty("tasks").GetProperty("total").GetInt32().Should().Be(0);
        stats.GetProperty("tasks").GetProperty("successRate").ValueKind.Should().Be(JsonValueKind.Null);
        stats.GetProperty("tasks").GetProperty("averageDurationSeconds").ValueKind.Should().Be(JsonValueKind.Null);
        stats.GetProperty("tasks").GetProperty("byAgent").GetArrayLength().Should().Be(0);
        stats.GetProperty("cost").GetProperty("totalUsd").GetDouble().Should().Be(0);
        stats.GetProperty("cost").GetProperty("byDay").GetArrayLength().Should().Be(0);
        stats.GetProperty("budget").GetProperty("projectBudgetUsd").ValueKind.Should().Be(JsonValueKind.Null);
        stats.GetProperty("budget").GetProperty("percentUsed").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Another_owners_project_is_not_found_and_an_admin_may_read_it()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var theirs = Token(factory, OtherOwner);
        var secret = await CreateProject(client, theirs, "Secret");
        await AddTask(factory, secret, TaskStatus.Done, "csharp");

        var asked = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/projects/{secret}/stats", Token(factory, GitHubOwner)));
        asked.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/projects/nope/stats", theirs))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await Stats(client, Token(factory, "admin", "*"), secret)).GetProperty("tasks").GetProperty("total").GetInt32().Should().Be(1);
    }

    // The homepage's numbers across the caller's projects (TASK-148)

    private static async Task<JsonElement> Overview(HttpClient client, string token, string query = "")
    {
        var response = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/stats{query}", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static void SetState(WebApplicationFactory<Program> factory, string projectId,
        Models.Projects.ProjectStatus? status = null, Models.Projects.ProjectExecutionState? execution = null)
    {
        var repo = factory.Services.GetRequiredService<IProjectRepository>();
        var project = repo.GetById(projectId)!;
        if (status is { } s) project.Status = s;
        if (execution is { } e) project.ExecutionState = e;
        repo.Update(project);
    }

    [Fact]
    public async Task The_overview_counts_the_callers_projects_tasks_this_week_and_spend_this_month_by_day()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = Token(factory, GitHubOwner);
        var shop = await CreateProject(client, token, "Shop");
        var blog = await CreateProject(client, token, "Blog");
        var done = await CreateProject(client, token, "Done");
        var paused = await CreateProject(client, token, "Paused");
        SetState(factory, shop, Models.Projects.ProjectStatus.InProgress);
        SetState(factory, done, Models.Projects.ProjectStatus.Completed);
        SetState(factory, paused, Models.Projects.ProjectStatus.InProgress, Models.Projects.ProjectExecutionState.Paused);
        var theirs = await CreateProject(client, Token(factory, OtherOwner), "Theirs");

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        await AddTask(factory, shop, TaskStatus.Done, "csharp", now.AddDays(-1));
        await AddTask(factory, shop, TaskStatus.Done, "csharp", now.AddDays(-10)); // done, but not this week
        await AddTask(factory, shop, TaskStatus.Failed, "csharp", now.AddDays(-2));
        await AddTask(factory, shop, TaskStatus.Working, "csharp", now);
        await AddTask(factory, blog, TaskStatus.Unassigned, "");
        await AddTask(factory, blog, TaskStatus.Done, "python", now.AddHours(-3));
        await AddTask(factory, theirs, TaskStatus.Done, "python", now); // another owner's: never counted
        await AddUsage(factory, shop, "openai", "gpt-4o", 1000, 0.25, now);
        await AddUsage(factory, blog, "openai", "gpt-4o", 3000, 0.50, now);
        await AddUsage(factory, shop, "openai", "gpt-4o", 500, 4.00, monthStart.AddDays(-1)); // last month: in the 30 days only if recent
        await AddUsage(factory, shop, "openai", "gpt-4o", 9999, 9.00, now.AddDays(-40)); // older than 30 days
        await AddUsage(factory, theirs, "openai", "gpt-4o", 9999, 7.00, now);

        var stats = await Overview(client, token);

        stats.GetProperty("projectIds").EnumerateArray().Select(p => p.GetString()).Should().BeEquivalentTo([shop, blog, done, paused]);
        stats.GetProperty("projects").GetProperty("total").GetInt32().Should().Be(4);
        stats.GetProperty("projects").GetProperty("active").GetInt32().Should().Be(2, "Completed and Paused are not active");

        var tasks = stats.GetProperty("tasks");
        tasks.GetProperty("total").GetInt32().Should().Be(6);
        tasks.GetProperty("byStatus").GetProperty("Done").GetInt32().Should().Be(3);
        tasks.GetProperty("successRate").GetDouble().Should().Be(75.0); // 3 done of 4 finished
        tasks.GetProperty("doneThisWeek").GetInt32().Should().Be(2);
        tasks.GetProperty("failedThisWeek").GetInt32().Should().Be(1);

        var cost = stats.GetProperty("cost");
        cost.GetProperty("thisMonthUsd").GetDouble().Should().BeApproximately(0.75, 1e-9);
        cost.GetProperty("thisMonthTokens").GetInt64().Should().Be(4000);
        cost.GetProperty("thisMonthRequests").GetInt32().Should().Be(2);
        cost.GetProperty("monthlyBudgetUsd").ValueKind.Should().Be(JsonValueKind.Null);
        var days = cost.GetProperty("byDay").EnumerateArray().ToList();
        days.Should().HaveCount(30, "every one of the last 30 days, zeros included");
        days[^1].GetProperty("date").GetString().Should().Be(now.ToString("yyyy-MM-dd"));
        days[0].GetProperty("date").GetString().Should().Be(now.AddDays(-29).ToString("yyyy-MM-dd"));
        days[^1].GetProperty("costUsd").GetDouble().Should().BeApproximately(0.75, 1e-9);
        days.Sum(d => d.GetProperty("costUsd").GetDouble()).Should().BeApproximately(now.Day <= 29 ? 4.75 : 0.75, 1e-9); // last month's last day is in the window up to the 29th

        var byProject = stats.GetProperty("byProject").EnumerateArray()
            .ToDictionary(p => p.GetProperty("name").GetString()!, p => p);
        byProject.Keys.Should().BeEquivalentTo(["Shop", "Blog", "Done", "Paused"]);
        byProject["Shop"].GetProperty("projectId").GetString().Should().Be(shop);
        byProject["Shop"].GetProperty("total").GetInt32().Should().Be(4);
        byProject["Shop"].GetProperty("byStatus").GetProperty("Failed").GetInt32().Should().Be(1);
        byProject["Shop"].GetProperty("byStatus").GetProperty("Working").GetInt32().Should().Be(1);
        byProject["Blog"].GetProperty("byStatus").GetProperty("Unassigned").GetInt32().Should().Be(1);
        byProject["Done"].GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task An_owner_with_nothing_gets_zeros_and_thirty_empty_days()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var stats = await Overview(client, Token(factory, GitHubOwner));

        stats.GetProperty("projects").GetProperty("total").GetInt32().Should().Be(0);
        stats.GetProperty("tasks").GetProperty("total").GetInt32().Should().Be(0);
        stats.GetProperty("tasks").GetProperty("successRate").ValueKind.Should().Be(JsonValueKind.Null);
        stats.GetProperty("cost").GetProperty("thisMonthUsd").GetDouble().Should().Be(0);
        stats.GetProperty("cost").GetProperty("byDay").EnumerateArray().Should().HaveCount(30)
            .And.OnlyContain(d => d.GetProperty("costUsd").GetDouble() == 0);
        stats.GetProperty("byProject").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Another_owners_projects_count_only_for_an_admin_asking_for_all()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var mine = Token(factory, GitHubOwner);
        await CreateProject(client, mine, "Mine");
        var secret = await CreateProject(client, Token(factory, OtherOwner), "Secret");
        await AddTask(factory, secret, TaskStatus.Done, "csharp", DateTime.UtcNow);

        foreach (var query in new[] { "", "?scope=all" })
        {
            var stats = await Overview(client, mine, query);
            stats.GetProperty("projects").GetProperty("total").GetInt32().Should().Be(1, $"an owner never sees another's ({query})");
            stats.GetProperty("tasks").GetProperty("total").GetInt32().Should().Be(0);
        }

        var admin = Token(factory, "admin", "*");
        (await Overview(client, admin)).GetProperty("projects").GetProperty("total").GetInt32().Should().Be(0, "an admin's own projects");
        var all = await Overview(client, admin, "?scope=all");
        all.GetProperty("projects").GetProperty("total").GetInt32().Should().Be(2);
        all.GetProperty("tasks").GetProperty("doneThisWeek").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task The_overview_shows_the_monthly_budget_when_one_is_set()
    {
        using var factory = CreateFactory(monthlyBudget: 500);
        var client = factory.CreateClient();

        var stats = await Overview(client, Token(factory, GitHubOwner));

        stats.GetProperty("cost").GetProperty("monthlyBudgetUsd").GetDouble().Should().Be(500);
    }
}
