using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Tests.Integration.Pipeline;

public class DataRetentionTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();

    public async Task InitializeAsync()
    {
        Skip.IfNot(TestDatabaseProvider.IsDockerAvailable(), "Docker is not available");
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [SkippableFact]
    public async Task DeleteOlderThan_RemovesOldRecords()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var dataSource = new DataSource
        {
            Name = "Retention Test",
            AdapterType = AdapterType.LogFile,
            ConnectionConfig = "{}"
        };
        context.DataSources.Add(dataSource);
        await context.SaveChangesAsync();

        var knownError = new KnownError
        {
            FingerprintHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            RepresentativeMessage = "Retention test error",
            Severity = ErrorSeverity.Warning,
            Status = ErrorStatus.Active,
            FirstSeen = DateTime.UtcNow.AddDays(-60),
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 100,
            DataSourceId = dataSource.Id
        };
        context.KnownErrors.Add(knownError);
        await context.SaveChangesAsync();

        // Add old and recent occurrences
        context.ErrorOccurrences.AddRange(
            new ErrorOccurrence
            {
                KnownErrorId = knownError.Id,
                WindowStart = DateTime.UtcNow.AddDays(-45),
                WindowEnd = DateTime.UtcNow.AddDays(-45).AddMinutes(5),
                Count = 10,
                SampleRatio = 1.0
            },
            new ErrorOccurrence
            {
                KnownErrorId = knownError.Id,
                WindowStart = DateTime.UtcNow.AddDays(-1),
                WindowEnd = DateTime.UtcNow.AddDays(-1).AddMinutes(5),
                Count = 5,
                SampleRatio = 1.0
            });
        await context.SaveChangesAsync();

        var repo = new ErrorOccurrenceRepository(context);

        // Delete records older than 30 days
        var deleted = await repo.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-30));

        deleted.Should().Be(1); // Only the 45-day-old record

        var remaining = await repo.GetByKnownErrorAsync(knownError.Id);
        remaining.Should().HaveCount(1);
        remaining[0].Count.Should().Be(5); // The recent one
    }
}
