using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Infrastructure.Data;
using LogJammer.Infrastructure.Repositories;
using LogJammer.Tests.Integration;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Tests.Unit.Pipeline;

public class SimilarityMergeTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private LogJammerDbContext _context = null!;
    private KnownErrorRepository _repo = null!;
    private DataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        Skip.IfNot(TestDatabaseProvider.IsDockerAvailable(), "Docker is not available");

        await _fixture.InitializeAsync();
        _context = _fixture.CreateDbContext();
        await _context.Database.MigrateAsync();
        _repo = new KnownErrorRepository(_context);

        _dataSource = new DataSource
        {
            Name = "Test Source",
            AdapterType = AdapterType.LogFile,
            ConnectionConfig = "{}"
        };
        _context.DataSources.Add(_dataSource);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context is not null)
            await _context.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    [SkippableFact]
    public async Task GetByFingerprintAliasAsync_ReturnsTarget_WhenAliasExists()
    {
        var target = await CreateKnownError("hash-target", "Target error message");

        _context.FingerprintAliases.Add(new FingerprintAlias
        {
            FingerprintHash = "hash-alias",
            KnownErrorId = target.Id
        });
        await _context.SaveChangesAsync();

        var result = await _repo.GetByFingerprintAliasAsync("hash-alias");

        result.Should().NotBeNull();
        result!.Id.Should().Be(target.Id);
    }

    [SkippableFact]
    public async Task GetByFingerprintAliasAsync_ReturnsNull_WhenNoAlias()
    {
        var result = await _repo.GetByFingerprintAliasAsync("nonexistent-hash");

        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task MergeIntoAsync_MovesOccurrences_CreatesAlias_DeletesSource()
    {
        var source = await CreateKnownError("hash-source-1", "Source error");
        var target = await CreateKnownError("hash-target-1", "Target error");

        // Add occurrences to source
        var window = DateTime.UtcNow;
        _context.ErrorOccurrences.Add(new ErrorOccurrence
        {
            KnownErrorId = source.Id,
            WindowStart = window,
            WindowEnd = window.AddMinutes(5),
            Count = 3
        });
        await _context.SaveChangesAsync();

        await _repo.MergeIntoAsync(source.Id, target.Id);

        // Source should be deleted
        var sourceAfter = await _context.KnownErrors.FindAsync(source.Id);
        sourceAfter.Should().BeNull();

        // Target should have updated counts
        var targetAfter = await _context.KnownErrors.FindAsync(target.Id);
        targetAfter!.TotalOccurrences.Should().Be(source.TotalOccurrences + target.TotalOccurrences);

        // Occurrence should be re-parented to target
        var occurrences = await _context.ErrorOccurrences
            .Where(o => o.KnownErrorId == target.Id)
            .ToListAsync();
        occurrences.Should().ContainSingle(o => o.WindowStart == window);

        // Alias should exist
        var alias = await _context.FingerprintAliases
            .FirstOrDefaultAsync(a => a.FingerprintHash == "hash-source-1");
        alias.Should().NotBeNull();
        alias!.KnownErrorId.Should().Be(target.Id);
    }

    [SkippableFact]
    public async Task MergeIntoAsync_IsIdempotent_WhenSourceAlreadyDeleted()
    {
        var target = await CreateKnownError("hash-target-2", "Target error");
        var fakeSourceId = Guid.NewGuid();

        // Should not throw
        var act = () => _repo.MergeIntoAsync(fakeSourceId, target.Id);
        await act.Should().NotThrowAsync();

        // Target should be unchanged
        var targetAfter = await _context.KnownErrors.FindAsync(target.Id);
        targetAfter!.TotalOccurrences.Should().Be(1);
    }

    [SkippableFact]
    public async Task MergeIntoAsync_MergesOverlappingOccurrenceWindows()
    {
        var source = await CreateKnownError("hash-source-3", "Source error");
        var target = await CreateKnownError("hash-target-3", "Target error");

        var window = DateTime.UtcNow;

        // Same window on both source and target
        _context.ErrorOccurrences.Add(new ErrorOccurrence
        {
            KnownErrorId = source.Id,
            WindowStart = window,
            WindowEnd = window.AddMinutes(5),
            Count = 7
        });
        _context.ErrorOccurrences.Add(new ErrorOccurrence
        {
            KnownErrorId = target.Id,
            WindowStart = window,
            WindowEnd = window.AddMinutes(5),
            Count = 3
        });
        await _context.SaveChangesAsync();

        await _repo.MergeIntoAsync(source.Id, target.Id);

        // Should have merged counts into one occurrence
        var occurrences = await _context.ErrorOccurrences
            .Where(o => o.KnownErrorId == target.Id)
            .ToListAsync();
        occurrences.Should().HaveCount(1);
        occurrences[0].Count.Should().Be(10); // 7 + 3
    }

    [SkippableFact]
    public async Task MergeIntoAsync_UpdatesFirstSeenAndLastSeen()
    {
        var earlier = DateTime.UtcNow.AddDays(-10);
        var later = DateTime.UtcNow.AddDays(1);

        var source = new KnownError
        {
            FingerprintHash = "hash-source-4",
            RepresentativeMessage = "Source error",
            DataSourceId = _dataSource.Id,
            FirstSeen = earlier,
            LastSeen = later,
            TotalOccurrences = 5
        };
        _context.KnownErrors.Add(source);

        var target = new KnownError
        {
            FingerprintHash = "hash-target-4",
            RepresentativeMessage = "Target error",
            DataSourceId = _dataSource.Id,
            FirstSeen = DateTime.UtcNow.AddDays(-2),
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 3
        };
        _context.KnownErrors.Add(target);
        await _context.SaveChangesAsync();

        await _repo.MergeIntoAsync(source.Id, target.Id);

        var targetAfter = await _context.KnownErrors.FindAsync(target.Id);
        targetAfter!.FirstSeen.Should().Be(earlier);
        targetAfter.LastSeen.Should().Be(later);
        targetAfter.TotalOccurrences.Should().Be(8); // 5 + 3
    }

    [SkippableFact]
    public async Task FindNearestByEmbeddingAsync_ReturnsMatch_WhenAboveThreshold()
    {
        var target = await CreateKnownError("hash-embed-target", "Target error");

        // Store a known embedding on target
        var fakeEmbedding = new float[384];
        fakeEmbedding[0] = 1.0f; // unit vector along dim 0
        target.EmbeddingVector = new Pgvector.Vector(fakeEmbedding);
        _context.KnownErrors.Update(target);
        await _context.SaveChangesAsync();

        // Search with a very similar vector
        var queryEmbedding = new float[384];
        queryEmbedding[0] = 0.99f;
        queryEmbedding[1] = 0.01f;
        // Normalize
        var norm = (float)Math.Sqrt(queryEmbedding.Sum(v => v * v));
        for (int i = 0; i < queryEmbedding.Length; i++) queryEmbedding[i] /= norm;

        var (match, similarity) = await _repo.FindNearestByEmbeddingAsync(queryEmbedding, 0.80);

        match.Should().NotBeNull();
        match!.Id.Should().Be(target.Id);
        similarity.Should().BeGreaterThan(0.80);
    }

    [SkippableFact]
    public async Task FindNearestByEmbeddingAsync_ReturnsNull_WhenBelowThreshold()
    {
        var target = await CreateKnownError("hash-embed-far", "Target error");

        var fakeEmbedding = new float[384];
        fakeEmbedding[0] = 1.0f;
        target.EmbeddingVector = new Pgvector.Vector(fakeEmbedding);
        _context.KnownErrors.Update(target);
        await _context.SaveChangesAsync();

        // Completely different vector
        var queryEmbedding = new float[384];
        queryEmbedding[383] = 1.0f;

        var (match, similarity) = await _repo.FindNearestByEmbeddingAsync(queryEmbedding, 0.80);

        match.Should().BeNull();
    }

    private async Task<KnownError> CreateKnownError(string hash, string message)
    {
        var error = new KnownError
        {
            FingerprintHash = hash,
            RepresentativeMessage = message,
            DataSourceId = _dataSource.Id,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 1
        };
        _context.KnownErrors.Add(error);
        await _context.SaveChangesAsync();
        return error;
    }
}
