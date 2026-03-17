using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogJammer.Tests.Processing;

public class IngestionPipelineTests(DatabaseFixture db) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task ProcessEntries_CreatesPatternAndOccurrence()
    {
        // Arrange: create a DataSource
        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = "pipeline-test-source",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = "{}",
        };

        await using (var ctx = db.CreateDbContext())
        {
            ctx.DataSources.Add(dataSource);
            await ctx.SaveChangesAsync();
        }

        // Build a ServiceCollection with LogJammerDbContext
        var services = new ServiceCollection();
        services.AddDbContext<LogJammerDbContext>(options =>
            options.UseNpgsql(db.ConnectionString));

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var pipeline = new IngestionPipeline(scopeFactory);

        var entry = new RawLogEntry
        {
            Message = "Connection refused to database host",
            Timestamp = DateTimeOffset.UtcNow,
            Level = "error",
        };

        // Act
        await pipeline.ProcessEntriesAsync([entry], dataSource.Id, null);

        // Assert: LogPattern was created with IsNew=true and correct Severity
        await using var readCtx = db.CreateDbContext();

        var pattern = await readCtx.LogPatterns
            .FirstOrDefaultAsync(p => p.DataSourceId == dataSource.Id);

        Assert.NotNull(pattern);
        Assert.True(pattern.IsNew);
        Assert.Equal(Severity.Error, pattern.Severity);

        // Assert: PatternOccurrence was created with Count=1
        var occurrence = await readCtx.PatternOccurrences
            .FirstOrDefaultAsync(o => o.PatternId == pattern.Id);

        Assert.NotNull(occurrence);
        Assert.Equal(1, occurrence.Count);
    }
}
