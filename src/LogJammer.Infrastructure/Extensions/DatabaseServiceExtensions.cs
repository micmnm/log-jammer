using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogJammer.Infrastructure.Extensions;

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddLogJammerDatabase(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LogJammerDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.UseVector()));

        return services;
    }
}
