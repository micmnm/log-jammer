using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Pipeline;
using LogJammer.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LogJammer.Infrastructure.Extensions;

public static class PipelineServiceExtensions
{
    public static IServiceCollection AddPipelineServices(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IKnownErrorRepository, KnownErrorRepository>();
        services.AddScoped<IErrorOccurrenceRepository, ErrorOccurrenceRepository>();

        // Pipeline components
        services.AddSingleton<ISchemaMapper, SchemaMapper>();
        services.AddSingleton<IFingerprintCalculator, FingerprintCalculator>();

        // Background services
        services.AddHostedService<DataSourcePollingManager>();
        services.AddHostedService<DataRetentionService>();

        return services;
    }
}
