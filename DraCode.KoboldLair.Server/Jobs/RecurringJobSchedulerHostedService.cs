using Birko.BackgroundJobs.Processing;

namespace DraCode.KoboldLair.Server.Jobs
{
    /// <summary>
    /// Hosted service adapter that runs the Birko.BackgroundJobs RecurringJobScheduler
    /// as a long-running background service within the ASP.NET Core host.
    /// </summary>
    public class RecurringJobSchedulerHostedService : BackgroundService
    {
        private readonly RecurringJobScheduler _scheduler;

        public RecurringJobSchedulerHostedService(RecurringJobScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _scheduler.RunAsync(stoppingToken);
        }
    }
}
