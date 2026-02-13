using LogJammer.Api.Services;
using LogJammer.Infrastructure.Data;
using LogJammer.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace LogJammer.Tests;

/// <summary>
/// WebApplicationFactory that replaces real services with NSubstitute mocks.
/// Sets environment to "Testing" so Program.cs skips migration/seeding.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public IDataSourceService DataSourceService { get; } = Substitute.For<IDataSourceService>();
    public IErrorGroupService ErrorGroupService { get; } = Substitute.For<IErrorGroupService>();
    public ITagService TagService { get; } = Substitute.For<ITagService>();
    public IConfigurationService ConfigurationService { get; } = Substitute.For<IConfigurationService>();
    public IClassificationQueueService ClassificationQueueService { get; } = Substitute.For<IClassificationQueueService>();
    public IAlertService AlertService { get; } = Substitute.For<IAlertService>();
    public ISpikeDetectionRuleService SpikeDetectionRuleService { get; } = Substitute.For<ISpikeDetectionRuleService>();
    public IFingerprintConfigService FingerprintConfigService { get; } = Substitute.For<IFingerprintConfigService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove real DbContext and replace with in-memory (unused, but prevents DI failures)
            services.RemoveAll<DbContextOptions<LogJammerDbContext>>();
            services.RemoveAll<LogJammerDbContext>();
            services.AddDbContext<LogJammerDbContext>(options =>
                options.UseInMemoryDatabase("TestDb-" + Guid.NewGuid()));

            // Replace real services with mocks
            services.RemoveAll<IDataSourceService>();
            services.AddSingleton(DataSourceService);

            services.RemoveAll<IErrorGroupService>();
            services.AddSingleton(ErrorGroupService);

            services.RemoveAll<ITagService>();
            services.AddSingleton(TagService);

            services.RemoveAll<IConfigurationService>();
            services.AddSingleton(ConfigurationService);

            services.RemoveAll<IClassificationQueueService>();
            services.AddSingleton(ClassificationQueueService);

            services.RemoveAll<IAlertService>();
            services.AddSingleton(AlertService);

            services.RemoveAll<ISpikeDetectionRuleService>();
            services.AddSingleton(SpikeDetectionRuleService);

            services.RemoveAll<IFingerprintConfigService>();
            services.AddSingleton(FingerprintConfigService);

            // Remove background hosted services (they depend on real repositories)
            services.RemoveAll<IHostedService>();

            // Clear existing health check registrations (NpgSql) — PostConfigure
            // ensures this runs after all Configure actions (including AddNpgSql's)
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
            });
        });
    }
}
