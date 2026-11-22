using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Saves what is still in memory when the server stops (TASK-121): Drakes' pending task-file saves, the shared
    /// planning contexts and pending notifications. It is registered before the background services, and hosted
    /// services stop in reverse order, so it runs after <see cref="DrakeExecutionService"/> has drained its runs — the
    /// host cancels that service's stopping token, the runs save their plans and end, and its cycle returns. Everything
    /// is bounded by <c>HostOptions.ShutdownTimeout</c>; nothing waits on a fixed sleep.
    /// </summary>
    public class ShutdownFlushService : IHostedService
    {
        private readonly DrakeFactory _drakeFactory;
        private readonly SharedPlanningContextService _planningContext;
        private readonly ProjectNotificationService _notifications;
        private readonly ILogger<ShutdownFlushService> _logger;

        // Taken when the host starts: by the time hosted services stop, a test host has already disposed its provider
        public ShutdownFlushService(DrakeFactory drakeFactory, SharedPlanningContextService planningContext,
            ProjectNotificationService notifications, ILogger<ShutdownFlushService> logger)
        {
            _drakeFactory = drakeFactory;
            _planningContext = planningContext;
            _notifications = notifications;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                var drakes = _drakeFactory.GetAllDrakes();
                if (drakes.Count > 0)
                {
                    _logger.LogInformation("Flushing {Count} Drake save channel(s)...", drakes.Count);
                    await Task.WhenAll(drakes.Select(d => d.FlushAndCloseAsync())).WaitAsync(cancellationToken);
                }

                _logger.LogInformation("Persisting shared planning contexts on shutdown...");
                await _planningContext.PersistAllContextsAsync().WaitAsync(cancellationToken);

                _logger.LogInformation("Persisting pending notifications on shutdown...");
                _notifications.PersistAll();

                _logger.LogInformation("Graceful shutdown complete");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Shutdown timeout reached before everything was saved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during graceful shutdown");
            }
        }
    }
}
