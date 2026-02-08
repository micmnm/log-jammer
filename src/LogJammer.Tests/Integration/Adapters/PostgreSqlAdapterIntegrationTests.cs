using System.Text.Json;
using FluentAssertions;
using LogJammer.Infrastructure.Adapters.PostgreSql;
using Npgsql;

namespace LogJammer.Tests.Integration.Adapters;

public class PostgreSqlAdapterIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public PostgreSqlAdapterIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task CreateTestTable()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS test_logs;
            CREATE TABLE test_logs (
                id SERIAL PRIMARY KEY,
                timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                level VARCHAR(20),
                message TEXT,
                service VARCHAR(100)
            );
            INSERT INTO test_logs (timestamp, level, message, service) VALUES
                (NOW() - INTERVAL '5 minutes', 'error', 'Connection timeout', 'api'),
                (NOW() - INTERVAL '4 minutes', 'warn', 'Slow query detected', 'db'),
                (NOW() - INTERVAL '3 minutes', 'error', 'NullReferenceException', 'api'),
                (NOW() - INTERVAL '2 minutes', 'info', 'Request completed', 'api'),
                (NOW() - INTERVAL '1 minute', 'error', 'Out of memory', 'worker');
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private string MakeConfig(string tableName = "test_logs", string timestampColumn = "timestamp")
    {
        return JsonSerializer.Serialize(new
        {
            connectionString = _fixture.ConnectionString,
            tableName,
            timestampColumn
        });
    }

    [Fact]
    public async Task TestConnection_WithExistingTable_ReturnsSuccess()
    {
        await CreateTestTable();
        var adapter = new PostgreSqlAdapter(MakeConfig());

        var result = await adapter.TestConnectionAsync();

        result.Success.Should().BeTrue();
        result.Metadata.Should().ContainKey("tableName");
    }

    [Fact]
    public async Task TestConnection_WithNonExistentTable_ReturnsFailure()
    {
        var adapter = new PostgreSqlAdapter(MakeConfig(tableName: "nonexistent_table"));

        var result = await adapter.TestConnectionAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not exist");
    }

    [Fact]
    public async Task GetSampleRecords_ReturnsRows()
    {
        await CreateTestTable();
        var adapter = new PostgreSqlAdapter(MakeConfig());

        var records = await adapter.GetSampleRecordsAsync(3);

        records.Should().HaveCount(3);
        records.Should().AllSatisfy(r =>
        {
            r.Fields.Should().ContainKey("level");
            r.Fields.Should().ContainKey("message");
        });
    }

    [Fact]
    public async Task PollErrors_WithTimestampFilter_ReturnsResults()
    {
        await CreateTestTable();
        var adapter = new PostgreSqlAdapter(MakeConfig());

        var batch = await adapter.PollErrorsAsync(DateTime.UtcNow.AddHours(-1), 100);

        batch.Entries.Should().HaveCount(5);
        batch.TotalAvailable.Should().Be(5);
    }

    [Fact]
    public async Task GetSchema_ReturnsColumns()
    {
        await CreateTestTable();
        var adapter = new PostgreSqlAdapter(MakeConfig());

        var schema = await adapter.GetSchemaAsync();

        schema.Should().Contain(f => f.Name == "id");
        schema.Should().Contain(f => f.Name == "timestamp");
        schema.Should().Contain(f => f.Name == "level");
        schema.Should().Contain(f => f.Name == "message");
        schema.Should().Contain(f => f.Name == "service");
    }
}
