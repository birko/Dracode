namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Coordinates graceful shutdown of the KoboldLair server.
    /// Provides a CancellationToken that is signaled when the server is stopping,
    /// allowing active Kobolds to save their state before the process exits.
    /// </summary>
    public class GracefulShutdownCoordinator
    {
        private readonly CancellationTokenSource _shutdownCts = new();
        private readonly ILogger<GracefulShutdownCoordinator> _logger;
        private readonly TimeSpan _gracePeriod;

        public GracefulShutdownCoordinator(
            ILogger<GracefulShutdownCoordinator> logger,
            TimeSpan? gracePeriod = null)
        {
            _logger = logger;
            _gracePeriod = gracePeriod ?? TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Token that is cancelled when shutdown is initiated.
        /// Pass this to long-running operations so they can exit gracefully.
        /// </summary>
        public CancellationToken ShutdownToken => _shutdownCts.Token;

        /// <summary>
        /// The configured grace period for active operations to complete
        /// </summary>
        public TimeSpan GracePeriod => _gracePeriod;

        /// <summary>
        /// Whether shutdown has been initiated
        /// </summary>
        public bool IsShuttingDown => _shutdownCts.IsCancellationRequested;

        /// <summary>
        /// Signals all listeners that the server is shutting down.
        /// Active Kobolds should save their plan state and conversation checkpoint.
        /// </summary>
        public void InitiateShutdown()
        {
            if (_shutdownCts.IsCancellationRequested)
            {
                _logger.LogDebug("Shutdown already initiated, ignoring duplicate call");
                return;
            }

            _logger.LogInformation("Initiating graceful shutdown (grace period: {GracePeriod}s)", _gracePeriod.TotalSeconds);
            _shutdownCts.Cancel();
        }
    }
}
