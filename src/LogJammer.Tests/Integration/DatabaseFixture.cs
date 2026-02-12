using LogJammer.Infrastructure.Data;

namespace LogJammer.Tests.Integration;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly TestDatabaseProvider _provider = new();

    public string ConnectionString => _provider.ConnectionString;

    public async Task InitializeAsync()
    {
        await _provider.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    public LogJammerDbContext CreateDbContext()
    {
        return _provider.CreateDbContext();
    }
}
