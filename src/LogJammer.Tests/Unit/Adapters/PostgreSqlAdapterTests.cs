using System.Text.Json;
using FluentAssertions;
using LogJammer.Infrastructure.Adapters.PostgreSql;

namespace LogJammer.Tests.Unit.Adapters;

public class PostgreSqlAdapterTests
{
    private static string MakeConfig(string tableName = "logs", string timestampColumn = "timestamp",
        string connectionString = "Host=localhost;Database=test")
    {
        return JsonSerializer.Serialize(new
        {
            connectionString,
            tableName,
            timestampColumn
        });
    }

    [Fact]
    public void Constructor_WithValidConfig_Succeeds()
    {
        var config = MakeConfig();
        var adapter = new PostgreSqlAdapter(config);
        adapter.Should().NotBeNull();
    }

    [Theory]
    [InlineData("logs; DROP TABLE users", "Table name")]
    [InlineData("123invalid", "Table name")]
    [InlineData("table-with-dash", "Table name")]
    public void Constructor_WithInvalidTableName_Throws(string tableName, string expectedLabel)
    {
        var config = MakeConfig(tableName: tableName);

        var act = () => new PostgreSqlAdapter(config);

        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{expectedLabel}*invalid*");
    }

    [Theory]
    [InlineData("valid_table")]
    [InlineData("_private")]
    [InlineData("Table123")]
    [InlineData("a")]
    public void Constructor_WithValidTableName_Succeeds(string tableName)
    {
        var config = MakeConfig(tableName: tableName);

        var adapter = new PostgreSqlAdapter(config);

        adapter.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidTimestampColumn_Throws()
    {
        var config = MakeConfig(timestampColumn: "col; DROP TABLE");

        var act = () => new PostgreSqlAdapter(config);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Timestamp column*invalid*");
    }

    [Fact]
    public void Constructor_WithInvalidJson_Throws()
    {
        var act = () => new PostgreSqlAdapter("not json");

        act.Should().Throw<JsonException>();
    }
}
