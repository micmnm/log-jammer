using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace LogJammer.Tests.Integration;

/// <summary>
/// Provides a PostgreSQL connection for integration tests.
/// When TEST_USE_LOCAL_DB=true, uses a local Postgres instance.
/// Otherwise, spins up a Testcontainers PostgreSQL container.
/// </summary>
public sealed class TestDatabaseProvider : IAsyncDisposable
{
    private const string DefaultLocalConnectionString =
        "Host=localhost;Port=5432;Database=logjammer_test;Username=logjammer;Password=logjammer";

    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = null!;

    public static bool UseLocalDb =>
        string.Equals(Environment.GetEnvironmentVariable("TEST_USE_LOCAL_DB"), "true", StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        if (UseLocalDb)
        {
            ConnectionString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
                               ?? DefaultLocalConnectionString;

            // Run migrations so the local DB is ready
            await using var context = CreateDbContext();
            await context.Database.MigrateAsync();
        }
        else
        {
            _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }
    }

    public LogJammerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LogJammerDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions => npgsqlOptions.UseVector())
            .Options;

        return new LogJammerDbContext(options);
    }

    /// <summary>
    /// Cleans all application tables between tests when using a local database.
    /// No-op for Testcontainers (each fixture gets a fresh container).
    /// </summary>
    public async Task CleanTablesAsync()
    {
        if (!UseLocalDb) return;

        await using var context = CreateDbContext();
        await using var conn = context.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$ DECLARE r RECORD;
            BEGIN
                FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename != '__EFMigrationsHistory')
                LOOP
                    EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' CASCADE';
                END LOOP;
            END $$;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Checks whether Docker is available on the host.
    /// Used by Elasticsearch tests to skip gracefully.
    /// </summary>
    public static bool IsDockerAvailable()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
