using LogJammer.Engine.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace LogJammer.Tests;

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;

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
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
