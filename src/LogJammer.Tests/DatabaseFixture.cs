using LogJammer.Engine.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace LogJammer.Tests;

public class DatabaseFixture : IAsyncLifetime
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=logjammer_test;Username=logjammer;Password=logjammer";

    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING") ?? DefaultConnectionString;

    public LogJammerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LogJammerDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new LogJammerDbContext(options);
    }

    public async Task InitializeAsync()
    {
        // Ensure test database exists
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        var dbName = builder.Database!;
        builder.Database = "postgres";

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();

        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{dbName}'";
        var exists = await checkCmd.ExecuteScalarAsync() is not null;

        if (!exists)
        {
            await using var createCmd = conn.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
            await createCmd.ExecuteNonQueryAsync();
        }

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
