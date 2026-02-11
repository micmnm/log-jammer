using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Pipeline;
using LogJammer.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Tests.Integration.Pipeline;

public class PipelineIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public PipelineIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FullPipeline_MapFingerprintStoreOccurrence()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        // Create data source
        var dataSource = new DataSource
        {
            Name = "Pipeline Test Source",
            AdapterType = AdapterType.LogFile,
            ConnectionConfig = "{}",
            FingerprintConfigs =
            {
                new FingerprintConfig { FieldName = "message", Order = 0, NormalizeBeforeHash = true }
            }
        };
        context.DataSources.Add(dataSource);
        await context.SaveChangesAsync();

        // Map
        var mapper = new SchemaMapper();
        var rawEntry = new RawLogEntry(
            DateTime.UtcNow,
            new Dictionary<string, object?>
            {
                ["message"] = "NullReferenceException in UserService",
                ["timestamp"] = DateTime.UtcNow.ToString("O")
            });

        var mapped = mapper.Map(rawEntry, null);
        mapped.Message.Should().Be("NullReferenceException in UserService");

        // Fingerprint
        var calculator = new FingerprintCalculator();
        var fingerprint = calculator.ComputeFingerprint(mapped, dataSource.FingerprintConfigs.ToList());
        fingerprint.Should().HaveLength(64);

        // Store KnownError
        var knownErrorRepo = new KnownErrorRepository(context);
        var knownError = await knownErrorRepo.AddAsync(new KnownError
        {
            FingerprintHash = fingerprint,
            RepresentativeMessage = mapped.Message,
            Severity = mapped.Severity ?? ErrorSeverity.Warning,
            Status = ErrorStatus.Active,
            FirstSeen = mapped.Timestamp,
            LastSeen = mapped.Timestamp,
            TotalOccurrences = 1,
            DataSourceId = dataSource.Id
        });
        knownError.Id.Should().NotBeEmpty();

        // Lookup by fingerprint
        var found = await knownErrorRepo.GetByFingerprintHashAsync(fingerprint);
        found.Should().NotBeNull();
        found!.Id.Should().Be(knownError.Id);

        // Upsert occurrence
        var occurrenceRepo = new ErrorOccurrenceRepository(context);
        await occurrenceRepo.UpsertWindowAsync(knownError.Id, mapped.Timestamp, mapped.Timestamp.AddMinutes(5), 1.0);

        // Verify occurrence
        var occurrences = await occurrenceRepo.GetByKnownErrorAsync(knownError.Id);
        occurrences.Should().HaveCount(1);
        occurrences[0].Count.Should().Be(1);

        // Upsert same window again (increment)
        await occurrenceRepo.UpsertWindowAsync(knownError.Id, mapped.Timestamp, mapped.Timestamp.AddMinutes(5), 1.0);
        var updated = await occurrenceRepo.GetByKnownErrorAsync(knownError.Id);
        updated[0].Count.Should().Be(2);
    }

    [Fact]
    public async Task KnownErrorRepository_GetAllWithFilters()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var dataSource = new DataSource
        {
            Name = "Filter Test Source",
            AdapterType = AdapterType.LogFile,
            ConnectionConfig = "{}"
        };
        context.DataSources.Add(dataSource);
        await context.SaveChangesAsync();

        context.KnownErrors.AddRange(
            new KnownError
            {
                FingerprintHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                RepresentativeMessage = "Error A",
                Severity = ErrorSeverity.Critical,
                Status = ErrorStatus.Active,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                TotalOccurrences = 5,
                DataSourceId = dataSource.Id
            },
            new KnownError
            {
                FingerprintHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                RepresentativeMessage = "Error B",
                Severity = ErrorSeverity.Info,
                Status = ErrorStatus.Resolved,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                TotalOccurrences = 1,
                DataSourceId = dataSource.Id
            });
        await context.SaveChangesAsync();

        var repo = new KnownErrorRepository(context);

        var criticalOnly = await repo.GetAllAsync(severity: ErrorSeverity.Critical);
        criticalOnly.Should().AllSatisfy(e => e.Severity.Should().Be(ErrorSeverity.Critical));

        var activeOnly = await repo.GetAllAsync(status: ErrorStatus.Active);
        activeOnly.Should().AllSatisfy(e => e.Status.Should().Be(ErrorStatus.Active));

        var count = await repo.GetCountAsync(dataSourceId: dataSource.Id);
        count.Should().BeGreaterThanOrEqualTo(2);
    }
}
