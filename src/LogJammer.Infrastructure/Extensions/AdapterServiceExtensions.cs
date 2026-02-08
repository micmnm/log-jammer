using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Adapters;
using LogJammer.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LogJammer.Infrastructure.Extensions;

public static class AdapterServiceExtensions
{
    public static IServiceCollection AddDataSourceAdapters(this IServiceCollection services)
    {
        services.AddScoped<IDataSourceRepository, DataSourceRepository>();
        services.AddSingleton<IDataSourceAdapterFactory, DataSourceAdapterFactory>();
        return services;
    }
}
