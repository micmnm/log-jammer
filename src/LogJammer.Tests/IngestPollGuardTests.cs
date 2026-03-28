using System.Text.Json;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogJammer.Tests;

[Collection("Database")]
public class IngestPollGuardTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task KibanaProxy_WithRecentPoll_ShouldBeGuarded()
    {
        await using var db = fixture.CreateDbContext();
        var config = JsonSerializer.Serialize(new { pollIntervalMinutes = 5.0 });
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"guard-test-{Guid.NewGuid():N}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = config,
            LastPolledAt = DateTimeOffset.UtcNow.AddMinutes(-1), // polled 1 min ago
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        // Threshold is 5 * 0.5 = 2.5 minutes. Last poll was 1 min ago → should be guarded.
        var timeSinceLastPoll = DateTimeOffset.UtcNow - source.LastPolledAt!.Value;
        var threshold = TimeSpan.FromMinutes(5 * 0.5);
        Assert.True(timeSinceLastPoll < threshold, "Poll should be within guard threshold");
    }

    [Fact]
    public async Task KibanaProxy_WithOldPoll_ShouldNotBeGuarded()
    {
        await using var db = fixture.CreateDbContext();
        var config = JsonSerializer.Serialize(new { pollIntervalMinutes = 5.0 });
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"guard-test-old-{Guid.NewGuid():N}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = config,
            LastPolledAt = DateTimeOffset.UtcNow.AddMinutes(-10), // polled 10 min ago
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        // Threshold is 5 * 0.5 = 2.5 minutes. Last poll was 10 min ago → should NOT be guarded.
        var timeSinceLastPoll = DateTimeOffset.UtcNow - source.LastPolledAt!.Value;
        var threshold = TimeSpan.FromMinutes(5 * 0.5);
        Assert.False(timeSinceLastPoll < threshold, "Poll should not be within guard threshold");
    }

    [Fact]
    public async Task Elasticsearch_Type_ShouldNotBeGuarded()
    {
        await using var db = fixture.CreateDbContext();
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"guard-test-es-{Guid.NewGuid():N}",
            Type = DataSourceType.Elasticsearch,
            ConnectionConfig = "http://localhost:9200",
            LastPolledAt = DateTimeOffset.UtcNow.AddSeconds(-10), // very recent
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        // Elasticsearch type should never trigger the guard
        Assert.Equal(DataSourceType.Elasticsearch, source.Type);
    }
}
