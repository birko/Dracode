using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Birko.AI.Resilience.Services;
using Birko.Security;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Data.Repositories.Sql;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DraCode.KoboldLair.Tests.Api;

/// <summary>
/// Settings → Budgets (TASK-163): an admin reads the budgets with this period's spend and changes them; a change is
/// stored, so it outlives a restart and wins over appsettings, and budget enforcement uses it at once; anyone without
/// <c>manage_config</c> gets 403; values out of range are refused.
/// </summary>
public class BudgetEndpointsTests : IDisposable
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-budget-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_projectsPath)) Directory.Delete(_projectsPath, recursive: true); } catch { }
    }

    private WebApplicationFactory<Program> CreateFactory(double daily = 10, double monthly = 200)
    {
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["KoboldLair:Data:DefaultBackend"] = "SqLite",
            ["KoboldLair:ProjectsPath"] = _projectsPath,
            ["KoboldLair:CostTracking:Budget:DailyBudgetUsd"] = daily.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["KoboldLair:CostTracking:Budget:MonthlyBudgetUsd"] = monthly.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["KoboldLair:CostTracking:Budget:ProjectBudgetUsd"] = "0",
            ["KoboldLair:CostTracking:Budget:WarningThresholdPercent"] = "80",
        };
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
    }

    private static string Token(WebApplicationFactory<Program> factory, string sub, string scope) =>
        factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = sub,
            ["scope"] = scope
        }).Token;

    private static string Admin(WebApplicationFactory<Program> factory) => Token(factory, "admin", "*");

    private static HttpRequestMessage Authed(HttpMethod method, string uri, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body != null) request.Content = JsonContent.Create(body);
        return request;
    }

    private static async Task<JsonElement> Budgets(HttpClient client, string token)
    {
        var response = await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/budgets", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static Task AddUsage(WebApplicationFactory<Program> factory, string projectId, double cost, DateTime at) =>
        factory.Services.GetRequiredService<SqlUsageRepository>().RecordUsageAsync(new UsageRecordEntity
        {
            ProjectId = projectId, Provider = "openai", Model = "gpt-4o", PromptTokens = 10, CompletionTokens = 10, TotalTokens = 20,
            EstimatedCostUsd = cost, RecordedAt = at
        });

    [Fact]
    public async Task An_admin_reads_the_budgets_from_appsettings_with_todays_and_this_months_spend()
    {
        using var factory = CreateFactory(daily: 10, monthly: 200);
        var client = factory.CreateClient();
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        await AddUsage(factory, "p-1", 1.50, now);
        await AddUsage(factory, "p-2", 0.50, now);
        await AddUsage(factory, "p-1", 3.00, monthStart.AddDays(-1)); // last month: neither today nor this month

        var budgets = await Budgets(client, Admin(factory));

        budgets.GetProperty("dailyBudgetUsd").GetDouble().Should().Be(10);
        budgets.GetProperty("monthlyBudgetUsd").GetDouble().Should().Be(200);
        budgets.GetProperty("projectBudgetUsd").GetDouble().Should().Be(0);
        budgets.GetProperty("warningThresholdPercent").GetDouble().Should().Be(80);
        budgets.GetProperty("editable").GetBoolean().Should().BeTrue();
        var spend = budgets.GetProperty("spend");
        spend.GetProperty("todayUsd").GetDouble().Should().BeApproximately(2.0, 1e-9);
        spend.GetProperty("thisMonthUsd").GetDouble().Should().BeApproximately(2.0, 1e-9);
        spend.GetProperty("topProject").GetProperty("projectId").GetString().Should().Be("p-1");
        spend.GetProperty("topProject").GetProperty("projectName").ValueKind.Should().Be(JsonValueKind.Null, "no project p-1 is registered");
        spend.GetProperty("topProject").GetProperty("spentUsd").GetDouble().Should().BeApproximately(4.5, 1e-9, "a project's budget counts all its spend");
    }

    [Fact]
    public async Task A_change_is_enforced_at_once_and_still_holds_after_a_restart_over_appsettings()
    {
        using (var factory = CreateFactory(daily: 10))
        {
            var client = factory.CreateClient();
            await AddUsage(factory, "p-1", 2.0, DateTime.UtcNow);
            (await factory.Services.GetRequiredService<CostTrackingService>().CheckBudgetAsync()).IsWithinBudget.Should().BeTrue();

            var put = await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/budgets", Admin(factory),
                new { dailyBudgetUsd = 1.0, monthlyBudgetUsd = 50.0, projectBudgetUsd = 5.0, warningThresholdPercent = 70.0 }));
            put.StatusCode.Should().Be(HttpStatusCode.OK);

            var status = await factory.Services.GetRequiredService<CostTrackingService>().CheckBudgetAsync();
            status.IsWithinBudget.Should().BeFalse("today's $2 is over the new $1 daily budget");
            status.BudgetType.Should().Be("daily");
            (await Budgets(client, Admin(factory))).GetProperty("dailyBudgetUsd").GetDouble().Should().Be(1);
        }
        SqliteConnection.ClearAllPools();

        using var restarted = CreateFactory(daily: 10); // appsettings still says 10
        var budgets = await Budgets(restarted.CreateClient(), Admin(restarted));
        budgets.GetProperty("dailyBudgetUsd").GetDouble().Should().Be(1);
        budgets.GetProperty("monthlyBudgetUsd").GetDouble().Should().Be(50);
        budgets.GetProperty("projectBudgetUsd").GetDouble().Should().Be(5);
        budgets.GetProperty("warningThresholdPercent").GetDouble().Should().Be(70);
        (await restarted.Services.GetRequiredService<CostTrackingService>().CheckBudgetAsync()).IsWithinBudget.Should().BeFalse();
    }

    [Fact]
    public async Task Without_manage_config_reading_and_changing_are_forbidden()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var owner = Token(factory, "github:4242", "view_own,manage_projects,execute_agents");

        (await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/budgets", owner))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/budgets", owner,
            new { dailyBudgetUsd = 0.0, monthlyBudgetUsd = 0.0, projectBudgetUsd = 0.0, warningThresholdPercent = 80.0 })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Budgets(client, Admin(factory))).GetProperty("dailyBudgetUsd").GetDouble().Should().Be(10, "nothing changed");
    }

    [Theory]
    [InlineData(-1, 0, 0, 80)]
    [InlineData(0, -5, 0, 80)]
    [InlineData(0, 0, -0.5, 80)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(0, 0, 0, 101)]
    public async Task Values_out_of_range_are_refused(double daily, double monthly, double project, double threshold)
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var put = await client.SendAsync(Authed(HttpMethod.Put, "/api/v1/budgets", Admin(factory),
            new { dailyBudgetUsd = daily, monthlyBudgetUsd = monthly, projectBudgetUsd = project, warningThresholdPercent = threshold }));

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await put.Content.ReadAsStringAsync()).Should().Contain("error");
        (await Budgets(client, Admin(factory))).GetProperty("dailyBudgetUsd").GetDouble().Should().Be(10);
    }

    [Fact]
    public async Task The_settings_capabilities_offer_budgets_to_an_admin_only()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        async Task<JsonElement> Caps(string token)
        {
            var r = await client.SendAsync(Authed(HttpMethod.Get, "/api/v1/settings/capabilities", token));
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            return doc.RootElement.Clone();
        }

        (await Caps(Admin(factory))).GetProperty("canManageBudgets").GetBoolean().Should().BeTrue();
        (await Caps(Token(factory, "github:4242", "view_own"))).GetProperty("canManageBudgets").GetBoolean().Should().BeFalse();
    }
}
