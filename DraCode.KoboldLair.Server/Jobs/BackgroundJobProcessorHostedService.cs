using Birko.BackgroundJobs.Processing;

namespace DraCode.KoboldLair.Server.Jobs
{
    /// <summary>
    /// Hosted service adapter that runs the Birko.BackgroundJobs BackgroundJobProcessor
    /// as a long-running background service within the ASP.NET Core host.
    /// </summary>
    public class BackgroundJobProcessorHostedService : BackgroundService
    {
        private readonly BackgroundJobProcessor _processor;

        public BackgroundJobProcessorHostedService(BackgroundJobProcessor processor)
        {
            _processor = processor;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _processor.RunAsync(stoppingToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _processor.Stop();
            return base.StopAsync(cancellationToken);
        }
    }
}
