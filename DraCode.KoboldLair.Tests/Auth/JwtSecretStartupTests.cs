using Birko.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DraCode.KoboldLair.Tests.Auth;

/// <summary>
/// TASK-118: with JWT auth on, the public development secret is accepted only in Development; any other
/// environment without a configured secret refuses to start.
/// </summary>
public class JwtSecretStartupTests
{
    private static WebApplicationFactory<Program> CreateFactory(string environment, string secret)
    {
        var projectsPath = Path.Combine(Path.GetTempPath(), "kl-jwtsecret-tests", Guid.NewGuid().ToString("N"));
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            // An explicit empty value also overrides a secret from the developer's appsettings.local.json.
            ["Authentication:Jwt:Secret"] = secret,
            ["KoboldLair:Data:DefaultBackend"] = "JsonFile",
            ["KoboldLair:ProjectsPath"] = projectsPath,
        };

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
    }

    [Fact]
    public void Production_host_without_a_secret_fails_to_start()
    {
        using var factory = CreateFactory("Production", secret: "");

        var start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>()
            .WithMessage("*Authentication:Jwt:Secret*KOBOLDLAIR_JWT_SECRET*'Production'*");
    }

    [Fact]
    public void Production_host_with_a_secret_starts()
    {
        using var factory = CreateFactory("Production", secret: "production-secret-key-at-least-32-characters");

        factory.Services.GetRequiredService<ITokenProvider>().Should().NotBeNull();
    }

    [Fact]
    public void Development_host_without_a_secret_starts_on_the_development_secret()
    {
        using var factory = CreateFactory("Development", secret: "");

        factory.Services.GetRequiredService<ITokenProvider>().Should().NotBeNull();
    }
}
