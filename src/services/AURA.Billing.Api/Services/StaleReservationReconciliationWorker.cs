namespace AURA.Billing.Api.Services;

public sealed class StaleReservationReconciliationWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<StaleReservationReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = configuration.GetValue<int>("Billing:ReconciliationIntervalSeconds", 30);
        var thresholdMinutes = configuration.GetValue<int>("Billing:StaleReservationThresholdMinutes", 30);

        logger.LogInformation(
            "StaleReservationReconciliationWorker started. Polling every {Interval}s for reservations stuck > {Threshold}m.",
            intervalSeconds, thresholdMinutes);

        // Initial delay on startup
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var reconciliationService = scope.ServiceProvider.GetRequiredService<StaleReservationReconciliationService>();

                var report = await reconciliationService.ReconcileStaleReservationsAsync(
                    thresholdMinutes, 100, stoppingToken);

                if (report.ReleasedCount > 0)
                {
                    logger.LogInformation(
                        "Reconciliation sweep completed: {Scanned} scanned, {Released} released, {Credits} credits refunded.",
                        report.TotalScanned, report.ReleasedCount, report.CreditsRefunded);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in StaleReservationReconciliationWorker loop.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("StaleReservationReconciliationWorker stopped.");
    }
}
