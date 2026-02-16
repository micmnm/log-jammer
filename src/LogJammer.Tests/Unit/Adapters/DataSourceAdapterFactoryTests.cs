using System.Text.Json;
using FluentAssertions;
using LogJammer.Core.Enums;
using LogJammer.Infrastructure.Adapters;
using LogJammer.Infrastructure.Adapters.Elasticsearch;
using LogJammer.Infrastructure.Adapters.LogFile;
using LogJammer.Infrastructure.Adapters.PostgreSql;

namespace LogJammer.Tests.Unit.Adapters;

public class DataSourceAdapterFactoryTests
{
    private readonly DataSourceAdapterFactory _factory = new();

    [Fact]
    public void CreateAdapter_Elasticsearch_ReturnsElasticsearchAdapter()
    {
        var config = JsonSerializer.Serialize(new { url = "http://localhost:9200", indexPattern = "logs-*" });

        var adapter = _factory.CreateAdapter(AdapterType.Elasticsearch, config);

        adapter.Should().BeOfType<ElasticsearchAdapter>();
    }

    [Fact]
    public void CreateAdapter_PostgreSql_ReturnsPostgreSqlAdapter()
    {
        var config = JsonSerializer.Serialize(new { connectionString = "Host=localhost", tableName = "logs", timestampColumn = "ts" });

        var adapter = _factory.CreateAdapter(AdapterType.PostgreSql, config);

        adapter.Should().BeOfType<PostgreSqlAdapter>();
    }

    [Fact]
    public void CreateAdapter_LogFile_ReturnsLogFileAdapter()
    {
        var config = JsonSerializer.Serialize(new { filePath = "/tmp/test.log", parseMode = "jsonlines" });

        var adapter = _factory.CreateAdapter(AdapterType.LogFile, config);

        adapter.Should().BeOfType<LogFileAdapter>();
    }

    [Fact]
    public void CreateAdapter_InvalidType_ThrowsArgumentOutOfRange()
    {
        var act = () => _factory.CreateAdapter((AdapterType)999, "{}");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
