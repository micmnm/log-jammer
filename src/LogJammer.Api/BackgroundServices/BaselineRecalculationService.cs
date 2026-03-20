using LogJammer.Engine;

namespace LogJammer.Api.BackgroundServices;

public class BaselineRecalculationService(IServiceScopeFactory scopeFactory, ILogger<BaselineRecalculationService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var calculator = scope.ServiceProvider.GetRequiredService<BaselineCalculator>();
                await calculator.RecalculateBaselinesAsync();
                logger.LogInformation("Baseline recalculation completed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during baseline recalculation");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
