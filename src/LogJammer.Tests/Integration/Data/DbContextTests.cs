using FluentAssertions;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Tests.Integration.Data;

public class DbContextTests : IAsyncLifetime
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
    public async Task MigrateAsync_CreatesAllTables()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var tables = new[]
        {
            "data_sources", "fingerprint_configs", "known_errors",
            "error_occurrences", "tags", "error_tags",
            "alerts", "user_overrides", "classification_queue",
            "fingerprint_aliases"
        };

        using var conn = context.Database.GetDbConnection();
        await conn.OpenAsync();

        foreach (var table in tables)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = @table";
            var param = cmd.CreateParameter();
            param.ParameterName = "table";
            param.Value = table;
            cmd.Parameters.Add(param);
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            count.Should().BeGreaterThan(0, $"table '{table}' should exist after migration");
        }
    }

    [SkippableFact]
    public async Task MigrateAsync_CreatesVectorExtension()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        // Verify pgvector extension is installed
        using var conn = context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector'";
        var result = await cmd.ExecuteScalarAsync();
        ((long)result!).Should().Be(1);
    }
}
