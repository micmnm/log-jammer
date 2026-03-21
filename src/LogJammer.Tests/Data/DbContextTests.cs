using LogJammer.Engine.Data.Entities;
using Xunit;

namespace LogJammer.Tests.Data;

[Collection("Database")]
public class DbContextTests(DatabaseFixture db)
{
    [Fact]
    public async Task CanCreateAndQueryDataSource()
    {
        await using var ctx = db.CreateDbContext();

        var ds = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = "test-source",
            Type = DataSourceType.Elasticsearch,
            ConnectionConfig = """{"url":"http://localhost:9200","indexPattern":"logs-*"}"""
        };

        ctx.DataSources.Add(ds);
        await ctx.SaveChangesAsync();

        await using var readCtx = db.CreateDbContext();
        var loaded = await readCtx.DataSources.FindAsync(ds.Id);
        Assert.NotNull(loaded);
        Assert.Equal("test-source", loaded.Name);
    }

    [Fact]
    public async Task CascadeDeleteRemovesPatterns()
    {
        await using var ctx = db.CreateDbContext();

        var ds = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = "cascade-test",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = "{}"
        };

        var pattern = new LogPattern
        {
            Id = Guid.NewGuid(),
            Template = "test pattern *",
            ClusterId = 1,
            FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow,
            SampleMessage = "test pattern 123",
            Severity = Severity.Error,
            DataSourceId = ds.Id
        };

        ctx.DataSources.Add(ds);
        ctx.LogPatterns.Add(pattern);
        await ctx.SaveChangesAsync();

        ctx.DataSources.Remove(ds);
        await ctx.SaveChangesAsync();

        await using var readCtx = db.CreateDbContext();
        Assert.Null(await readCtx.LogPatterns.FindAsync(pattern.Id));
    }
}
