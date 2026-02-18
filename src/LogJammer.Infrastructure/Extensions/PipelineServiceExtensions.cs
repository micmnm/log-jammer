using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.ML;
using LogJammer.Infrastructure.Pipeline;
using LogJammer.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Extensions;

public static class PipelineServiceExtensions
{
    public static IServiceCollection AddPipelineServices(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IKnownErrorRepository, KnownErrorRepository>();
        services.AddScoped<IErrorOccurrenceRepository, ErrorOccurrenceRepository>();
        services.AddScoped<IClassificationConfigRepository, ClassificationConfigRepository>();
        services.AddScoped<IClassificationQueueRepository, ClassificationQueueRepository>();
        services.AddScoped<IUserOverrideRepository, UserOverrideRepository>();
        services.AddScoped<ITagRepository, TagRepository>();

        // Pipeline components
        services.AddSingleton<ISchemaMapper, SchemaMapper>();
        services.AddSingleton<IFingerprintCalculator, FingerprintCalculator>();
        services.AddScoped<ILogIngestionPipeline, LogIngestionPipeline>();

        // ML / Classification
        services.AddSingleton(sp =>
        {
            var env = sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
            var modelDir = Path.Combine(env.ContentRootPath, "models", "all-MiniLM-L6-v2");
            return new ModelDownloader(modelDir, sp.GetRequiredService<ILogger<ModelDownloader>>());
        });
        services.AddSingleton<IEmbeddingProvider, OnnxEmbeddingProvider>();
        services.AddScoped<IClassificationService, ClassificationService>();

        // Spike detection & alerting
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<ISpikeDetectionRuleRepository, SpikeDetectionRuleRepository>();
        services.AddScoped<ICorrelatedSpikeAlertRepository, CorrelatedSpikeAlertRepository>();
        services.AddScoped<ISpikeDetector, SpikeDetector>();
        services.AddScoped<IAlertManager, AlertManager>();
        services.AddScoped<ICorrelationDetector, CorrelationDetector>();

        // Background services
        services.AddHostedService<DataSourcePollingManager>();
        services.AddHostedService<DataRetentionService>();
        services.AddHostedService<ClassificationProcessor>();
        services.AddHostedService<SpikeDetectionProcessor>();

        return services;
    }
}
