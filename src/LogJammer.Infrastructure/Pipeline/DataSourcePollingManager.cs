using System.Collections.Concurrent;
using LogJammer.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Pipeline;

public class DataSourcePollingManager(
    IServiceScopeFactory scopeFactory,
    ILogger<DataSourcePollingManager> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, (DataSourcePollingService Service, CancellationTokenSource Cts, Task Task)> _runningServices = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay to let the app start up
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        logger.LogInformation("Polling manager started");

        // Start initial services
        await ReconcileAsync(stoppingToken);

        // Periodic reconciliation
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during polling reconciliation");
            }
        }
    }

    private async Task ReconcileAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dataSourceRepo = scope.ServiceProvider.GetRequiredService<IDataSourceRepository>();
        var dataSources = await dataSourceRepo.GetAllAsync(stoppingToken);

        var enabledIds = dataSources
            .Where(ds => ds.Enabled && ds.AdapterType != Core.Enums.AdapterType.KibanaProxy)
            .Select(ds => ds.Id).ToHashSet();

        // Stop services for disabled/removed data sources
        foreach (var (id, entry) in _runningServices)
        {
            if (!enabledIds.Contains(id) || entry.Task.IsCompleted)
            {
                StopService(id);
            }
        }

        // Start services for new enabled data sources
        foreach (var id in enabledIds)
        {
            if (!_runningServices.ContainsKey(id))
            {
                StartService(id, stoppingToken);
            }
        }
    }

    private void StartService(Guid dataSourceId, CancellationToken parentToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        var serviceLogger = scopeFactory.CreateScope().ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger($"PollingService[{dataSourceId}]");

        var service = new DataSourcePollingService(scopeFactory, dataSourceId, serviceLogger);
        var task = Task.Run(() => service.RunAsync(cts.Token), cts.Token);

        _runningServices[dataSourceId] = (service, cts, task);
        logger.LogInformation("Started polling service for data source {DataSourceId}", dataSourceId);
    }

    private void StopService(Guid dataSourceId)
    {
        if (_runningServices.TryRemove(dataSourceId, out var entry))
        {
            entry.Cts.Cancel();
            entry.Cts.Dispose();
            logger.LogInformation("Stopped polling service for data source {DataSourceId}", dataSourceId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Polling manager stopping, cancelling all services");

        foreach (var id in _runningServices.Keys.ToList())
        {
            StopService(id);
        }

        await base.StopAsync(cancellationToken);
    }
}
