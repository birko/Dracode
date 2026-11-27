using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DraCode.KoboldLair.Tests;

/// <summary>
/// The server starts in its default configuration, hosted services included. Other host tests remove the hosted
/// services, and Birko's startup validation runs as one — so a host that refuses to start went unnoticed there.
/// With no database setting it runs on SQLite with every store registered; the removed JSON backend is refused
/// (TASK-227).
/// </summary>
public class HostStartupTests : IDisposable
{
    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-startup-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_projectsPath, recursive: true); } catch { }
    }

    private WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?> settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            settings["Authentication:Jwt:Enabled"] = "false";
            settings["KoboldLair:ProjectsPath"] = _projectsPath;
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
        });

    [Fact]
    public async Task Server_with_JWT_off_starts_and_answers()
    {
        using var factory = CreateFactory(new());

        var response = await factory.CreateClient().GetAsync("/api/config-does-not-exist");

        // Any HTTP answer means the host started; the startup failure surfaced as an exception from CreateClient.
        ((int)response.StatusCode).Should().BeLessThan(500);
    }

    [Fact]
    public void With_no_database_setting_every_store_is_registered_on_SQLite()
    {
        using var factory = CreateFactory(new());
        factory.CreateClient();
        var services = factory.Services;

        services.GetRequiredService<IProjectRepository>().Should().BeOfType<SqlProjectRepository>();
        services.GetService<ITaskRepository>().Should().NotBeNull();
        services.GetService<IUserRepository>().Should().NotBeNull();
        services.GetService<SqlPlanRepository>().Should().NotBeNull();
        services.GetService<SqlPendingDecisionRepository>().Should().NotBeNull();
        services.GetService<SqlHistoryRepository>().Should().NotBeNull();
        services.GetService<SqlUsageRepository>().Should().NotBeNull();
        services.GetService<SqlBudgetRepository>().Should().NotBeNull();
        services.GetService<SqlLayoutRepository>().Should().NotBeNull();
        services.GetService<Birko.Data.EventSourcing.Events.IAsyncEventStore>().Should().NotBeNull();
        Directory.GetFiles(_projectsPath, "*.db").Should().NotBeEmpty("the stores opened the SQLite file under ProjectsPath");
    }

    [Fact]
    public void A_configuration_still_naming_the_JSON_backend_refuses_to_start_and_says_what_to_set()
    {
        using var factory = CreateFactory(new() { ["KoboldLair:Data:DefaultBackend"] = "JsonFile" });

        var start = () => factory.CreateClient();

        start.Should().Throw<Exception>().Where(e =>
            e.ToString().Contains("JsonFile") && e.ToString().Contains("KoboldLair:Data:Provider"));
    }
}
