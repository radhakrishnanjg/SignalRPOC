using BNB.UsecaseServices.Interfaces;
using BNP_Usecase1.Hubs;
using Cronos;

namespace BNP_Usecase1
{
    // Picks up flat files from FlatFilePath on the FlatFileCronExpression schedule,
    // persists them via IPaymentBusiness and moves each processed file to a "Processed" sub-folder.
    public class FlatFileBackgroundService(IServiceScopeFactory scopeFactory,
        IConfiguration config,
        PaymentNotifier notifier,
        ILogger<FlatFileBackgroundService> logger) : BackgroundService
    {
        private readonly string? _path = config.GetConnectionString("FlatFilePath");
        private readonly string? _cron = config.GetConnectionString("FlatFileCronExpression");

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_path) || string.IsNullOrWhiteSpace(_cron))
            {
                logger.LogWarning("FlatFilePath / FlatFileCronExpression not configured; flat file service disabled.");
                return;
            }

            var expression = CronExpression.Parse(_cron);

            while (!stoppingToken.IsCancellationRequested)
            {
                var next = expression.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Local);
                if (next == null) return;

                var delay = next.Value - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    try { await Task.Delay(delay, stoppingToken); }
                    catch (OperationCanceledException) { return; }
                }

                ProcessFiles();
            }
        }

        private void ProcessFiles()
        {
            try
            {
                if (!Directory.Exists(_path))
                {
                    logger.LogWarning("Flat file folder '{Path}' does not exist.", _path);
                    return;
                }

                var processedDir = Path.Combine(_path!, "Processed");
                Directory.CreateDirectory(processedDir);

                foreach (var file in Directory.GetFiles(_path!, "*.txt"))
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var business = scope.ServiceProvider.GetRequiredService<IPaymentBusiness>();

                        var fileName = Path.GetFileName(file);

                        if (!business.FeedExist(file))
                        {
                            logger.LogWarning("Feed {File} already loaded; skipping.", fileName);
                        }
                        else
                        {
                            var result = business.InsertBatch(business.GetPaymentFromFeed(file));
                            notifier.PublishAsync(business).GetAwaiter().GetResult();
                            logger.LogInformation(
                                "Processed {File}: total {Total}, valid {Valid}, invalid {Invalid}, persisted {Persisted}",
                                fileName, result.TotalRecords, result.ValidRecords, result.InvalidRecords, result.Persisted);
                        }

                        File.Move(file, Path.Combine(processedDir, fileName), overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to process flat file {File}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Flat file run failed.");
            }
        }
    }
}
