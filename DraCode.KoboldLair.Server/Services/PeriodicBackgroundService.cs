namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Base class for background services that run a task on a periodic interval,
    /// skipping cycles if the previous one is still running.
    /// </summary>
    public abstract class PeriodicBackgroundService : BackgroundService
    {
        private readonly TimeSpan _interval;
        private readonly TimeSpan _initialDelay;
        private int _isRunning;

        protected abstract ILogger Logger { get; }

        protected PeriodicBackgroundService(TimeSpan interval, TimeSpan? initialDelay = null)
        {
            _interval = interval;
            _initialDelay = initialDelay ?? TimeSpan.Zero;
        }

        /// <summary>
        /// The work to perform each cycle. Called only if the previous cycle has completed.
        /// </summary>
        protected abstract Task ExecuteCycleAsync(CancellationToken stoppingToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("{Service} started. Interval: {Interval}s",
                GetType().Name, _interval.TotalSeconds);

            if (_initialDelay > TimeSpan.Zero)
                await Task.Delay(_initialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_interval, stoppingToken);

                if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                {
                    Logger.LogDebug("{Service} previous cycle still running, skipping", GetType().Name);
                    continue;
                }

                try
                {
                    await ExecuteCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error in {Service} cycle", GetType().Name);
                }
                finally
                {
                    Interlocked.Exchange(ref _isRunning, 0);
                }
            }

            Logger.LogInformation("{Service} stopped", GetType().Name);
        }
    }
}
