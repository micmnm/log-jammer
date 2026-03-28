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
    public async Task KibanaProxy_WithRecentPoll_ShouldBeWithinGuardThreshold()
    {
        await using var db = fixture.CreateDbContext();
        var config = JsonSerializer.Serialize(new { pollIntervalMinutes = 5.0 });
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"guard-test-{Guid.NewGuid():N}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = config,
            LastPolledAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        // Reload from DB to verify persistence
        var loaded = await db.DataSources.AsNoTracking().FirstAsync(d => d.Id == source.Id);

        // Verify the guard condition: time since last poll < 50% of poll interval
        var timeSinceLastPoll = DateTimeOffset.UtcNow - loaded.LastPolledAt!.Value;
        var pollIntervalMinutes = ExtractPollIntervalMinutes(loaded.ConnectionConfig);
        Assert.NotNull(pollIntervalMinutes);
        var threshold = TimeSpan.FromMinutes(pollIntervalMinutes.Value * 0.5);
        Assert.True(timeSinceLastPoll < threshold,
            $"Expected {timeSinceLastPoll.TotalSeconds:F0}s < {threshold.TotalSeconds:F0}s threshold");
    }

    [Fact]
    public async Task KibanaProxy_WithOldPoll_ShouldNotBeWithinGuardThreshold()
    {
        await using var db = fixture.CreateDbContext();
        var config = JsonSerializer.Serialize(new { pollIntervalMinutes = 5.0 });
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"guard-test-old-{Guid.NewGuid():N}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = config,
            LastPolledAt = DateTimeOffset.UtcNow.AddMinutes(-10),
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        var loaded = await db.DataSources.AsNoTracking().FirstAsync(d => d.Id == source.Id);

        var timeSinceLastPoll = DateTimeOffset.UtcNow - loaded.LastPolledAt!.Value;
        var pollIntervalMinutes = ExtractPollIntervalMinutes(loaded.ConnectionConfig);
        Assert.NotNull(pollIntervalMinutes);
        var threshold = TimeSpan.FromMinutes(pollIntervalMinutes.Value * 0.5);
        Assert.False(timeSinceLastPoll < threshold,
            $"Expected {timeSinceLastPoll.TotalSeconds:F0}s >= {threshold.TotalSeconds:F0}s threshold");
    }

    [Fact]
    public void ExtractPollInterval_FromValidJson_ReturnsPollInterval()
    {
        var config = JsonSerializer.Serialize(new { pollIntervalMinutes = 10.0, kibanaUrl = "https://kibana.example.com" });
        var result = ExtractPollIntervalMinutes(config);
        Assert.Equal(10.0, result);
    }

    [Fact]
    public void ExtractPollInterval_FromPlainUrl_ReturnsNull()
    {
        var result = ExtractPollIntervalMinutes("http://localhost:9200");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractPollInterval_FromJsonWithoutField_ReturnsNull()
    {
        var config = JsonSerializer.Serialize(new { kibanaUrl = "https://kibana.example.com" });
        var result = ExtractPollIntervalMinutes(config);
        Assert.Null(result);
    }

    [Fact]
    public async Task Elasticsearch_Type_HasNoPollIntervalInConfig()
    {
        await using var db = fixture.CreateDbContext();
        var source = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"guard-test-es-{Guid.NewGuid():N}",
            Type = DataSourceType.Elasticsearch,
            ConnectionConfig = "http://localhost:9200",
            LastPolledAt = DateTimeOffset.UtcNow.AddSeconds(-10),
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        // Elasticsearch ConnectionConfig is a plain URL — no poll interval to extract
        Assert.Equal(DataSourceType.Elasticsearch, source.Type);
        Assert.Null(ExtractPollIntervalMinutes(source.ConnectionConfig));
    }

    /// <summary>
    /// Mirror of IngestController.ExtractPollIntervalMinutes for testability.
    /// </summary>
    private static double? ExtractPollIntervalMinutes(string connectionConfig)
    {
        try
        {
            using var doc = JsonDocument.Parse(connectionConfig);
            if (doc.RootElement.TryGetProperty("pollIntervalMinutes", out var prop))
                return prop.GetDouble();
        }
        catch (JsonException)
        {
        }
        return null;
    }
}
