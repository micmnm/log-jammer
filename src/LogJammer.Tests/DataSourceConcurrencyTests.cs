using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogJammer.Tests;

[Collection("Database")]
public class DataSourceConcurrencyTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Update_WithMatchingVersion_Succeeds()
    {
        await using var db = fixture.CreateDbContext();
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"concurrency-test-{Guid.NewGuid():N}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = "{}",
            Version = 1,
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        // Simulate update with correct version
        var loaded = await db.DataSources.FirstAsync(d => d.Id == source.Id);
        Assert.Equal(1, loaded.Version);
        loaded.Name = "updated-name";
        loaded.Version++;
        await db.SaveChangesAsync();

        var reloaded = await db.DataSources.AsNoTracking().FirstAsync(d => d.Id == source.Id);
        Assert.Equal("updated-name", reloaded.Name);
        Assert.Equal(2, reloaded.Version);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ThrowsConcurrencyException()
    {
        await using var db1 = fixture.CreateDbContext();
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"concurrency-stale-{Guid.NewGuid():N}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = "{}",
            Version = 1,
        };
        db1.DataSources.Add(source);
        await db1.SaveChangesAsync();

        // Load in two separate contexts
        await using var db2 = fixture.CreateDbContext();
        var loaded1 = await db1.DataSources.FirstAsync(d => d.Id == source.Id);
        var loaded2 = await db2.DataSources.FirstAsync(d => d.Id == source.Id);

        // First update succeeds
        loaded1.Name = "first-update";
        loaded1.Version++;
        await db1.SaveChangesAsync();

        // Second update with stale version fails
        loaded2.Name = "second-update";
        loaded2.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }
}
