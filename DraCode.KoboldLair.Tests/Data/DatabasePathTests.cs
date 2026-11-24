using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Data.Repositories;
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

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// Every store opens the one database under <c>KoboldLair:ProjectsPath</c>, however early it is built (TASK-163). The
/// path used to reach <c>DataStorageConfig</c> only when the project repository was built, so a store built before it
/// (the usage records, once the budgets were read at startup) opened <c>./projects/</c> instead, with production
/// setting only <c>KoboldLair:ProjectsPath</c>.
/// </summary>
public class DatabasePathTests : IDisposable
{
    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-dbpath-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_projectsPath)) Directory.Delete(_projectsPath, recursive: true); } catch { }
    }

    [Fact]
    public async Task Usage_records_go_to_the_database_under_the_projects_path_even_when_built_first()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KoboldLair:Data:DefaultBackend"] = "SqLite",
                ["KoboldLair:ProjectsPath"] = _projectsPath, // as production sets it: no KoboldLair:Data:ProjectsPath
            }));
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });

        // The usage store first, before anything has built the project repository
        await factory.Services.GetRequiredService<SqlUsageRepository>().RecordUsageAsync(new UsageRecordEntity
        {
            ProjectId = "p-1", Provider = "openai", Model = "gpt-4o", TotalTokens = 1, EstimatedCostUsd = 0.1, RecordedAt = DateTime.UtcNow
        });
        factory.Services.GetRequiredService<IProjectRepository>();

        var expected = Path.Combine(_projectsPath, "koboldlair-test.db");
        File.Exists(expected).Should().BeTrue("the database belongs under KoboldLair:ProjectsPath");
        using var connection = new SqliteConnection($"Data Source={expected};Pooling=False");
        connection.Open();
        using var count = connection.CreateCommand();
        count.CommandText = "select count(*) from usage_records where ProjectId = 'p-1'";
        Convert.ToInt32(count.ExecuteScalar()).Should().Be(1);
    }
}
