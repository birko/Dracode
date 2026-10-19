using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace DraCode.KoboldLair.Tests;

/// <summary>
/// The server starts in its default configuration, hosted services included. Other host tests remove the hosted
/// services, and Birko's startup validation runs as one — so a host that refuses to start went unnoticed there.
/// </summary>
public class HostStartupTests
{
    [Fact]
    public async Task Server_with_JWT_off_starts_and_answers()
    {
        var projectsPath = Path.Combine(Path.GetTempPath(), "kl-startup-tests", Guid.NewGuid().ToString("N"));
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Enabled"] = "false",
                ["KoboldLair:Data:DefaultBackend"] = "JsonFile",
                ["KoboldLair:ProjectsPath"] = projectsPath,
            }));
        });

        var response = await factory.CreateClient().GetAsync("/api/config-does-not-exist");

        // Any HTTP answer means the host started; the startup failure surfaced as an exception from CreateClient.
        ((int)response.StatusCode).Should().BeLessThan(500);
    }
}
