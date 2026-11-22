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
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Endpoint;

/// <summary>
/// Graceful shutdown without a blocking wait (TASK-121). The host cancels each background service's stopping token
/// and waits for it, up to <see cref="HostOptions.ShutdownTimeout"/>; Drake runs take that token, and
/// <see cref="ShutdownFlushService"/> — registered before the background services, so stopped after them — saves what
/// is left. Before, an <c>ApplicationStopping</c> callback slept for 10 s on the host's stopping thread.
/// </summary>
public class GracefulShutdownTests : IDisposable
{
    private readonly string _projectsPath = Path.Combine(Path.GetTempPath(), "kl-shutdown", Guid.NewGuid().ToString("N"));
    private readonly List<Type> _hostedServices = new();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_projectsPath)) Directory.Delete(_projectsPath, recursive: true); } catch { }
    }

    private WebApplicationFactory<Program> CreateFactory(string? timeoutSeconds = null)
    {
        var config = new Dictionary<string, string?>
        {
            ["KoboldLair:Data:DefaultBackend"] = "SqLite",
            ["KoboldLair:ProjectsPath"] = _projectsPath,
        };
        if (timeoutSeconds != null)
            config["KoboldLair:Shutdown:TimeoutSeconds"] = timeoutSeconds;
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services =>
            {
                // Record the order the server registered its hosted services in, then keep them from running here
                _hostedServices.AddRange(services.Where(d => d.ServiceType == typeof(IHostedService))
                    .Select(d => d.ImplementationType ?? d.ImplementationFactory?.Method.ReturnType ?? typeof(object)));
                services.RemoveAll<IHostedService>();
            });
        });
    }

    private static void SafeDispose(WebApplicationFactory<Program> factory)
    {
        try { factory.Dispose(); }
        catch (AggregateException) { }
        catch (OperationCanceledException) { }
    }

    [Fact]
    public void The_flush_service_stops_after_the_Drake_services_have_drained()
    {
        var factory = CreateFactory();
        try
        {
            _ = factory.Services; // builds the host, which runs the registrations
            var flush = _hostedServices.IndexOf(typeof(ShutdownFlushService));
            flush.Should().BeGreaterThanOrEqualTo(0, "the flush runs as a hosted service's StopAsync, not a lifetime callback");
            // Hosted services stop in reverse registration order
            flush.Should().BeLessThan(_hostedServices.IndexOf(typeof(DrakeExecutionService)));
            flush.Should().BeLessThan(_hostedServices.IndexOf(typeof(DrakeMonitoringService)));
            flush.Should().BeLessThan(_hostedServices.IndexOf(typeof(ReasoningMonitorService)));
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public void The_shutdown_timeout_comes_from_configuration_and_defaults_to_30_seconds()
    {
        var configured = CreateFactory(timeoutSeconds: "45");
        try
        {
            configured.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout
                .Should().Be(TimeSpan.FromSeconds(45));
        }
        finally { SafeDispose(configured); }

        var defaulted = CreateFactory();
        try
        {
            defaulted.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout
                .Should().Be(TimeSpan.FromSeconds(30));
        }
        finally { SafeDispose(defaulted); }
    }

    [Fact]
    public async Task The_flush_saves_without_blocking_and_gives_up_when_the_timeout_is_spent()
    {
        var factory = CreateFactory();
        try
        {
            var flush = ActivatorUtilities.CreateInstance<ShutdownFlushService>(factory.Services);

            var stop = flush.StopAsync(CancellationToken.None);
            (await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(stop, "nothing sleeps");

            using var spent = new CancellationTokenSource();
            spent.Cancel();
            await flush.Invoking(f => f.StopAsync(spent.Token)).Should().NotThrowAsync("a spent timeout is logged, not thrown");
        }
        finally { SafeDispose(factory); }
    }
}
