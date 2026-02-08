using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using LogJammer.Infrastructure.Adapters.Elasticsearch;
using Testcontainers.Elasticsearch;

namespace LogJammer.Tests.Integration.Adapters;

public class ElasticsearchAdapterIntegrationTests : IAsyncLifetime
{
    private readonly ElasticsearchContainer _container = new ElasticsearchBuilder("docker.elastic.co/elasticsearch/elasticsearch:8.17.0")
        .WithEnvironment("xpack.security.enabled", "false")
        .WithEnvironment("discovery.type", "single-node")
        .Build();

    private string _baseUrl = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _baseUrl = _container.GetConnectionString();

        // Index some test documents
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(_baseUrl);

        for (var i = 0; i < 5; i++)
        {
            var doc = JsonSerializer.Serialize(new
            {
                @timestamp = DateTime.UtcNow.AddMinutes(-i).ToString("o"),
                level = i % 2 == 0 ? "error" : "warn",
                message = $"Test message {i}",
                service = "test-service"
            });

            var content = new StringContent(doc, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"logs-test/_doc/{i}", content);
            response.EnsureSuccessStatusCode();
        }

        // Refresh index to make docs searchable
        await httpClient.PostAsync("logs-test/_refresh", null);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync().AsTask();
    }

    private string MakeConfig(string? indexPattern = null)
    {
        return JsonSerializer.Serialize(new ElasticsearchConnectionConfig
        {
            Url = _baseUrl,
            IndexPattern = indexPattern ?? "logs-test"
        });
    }

    [Fact]
    public async Task TestConnection_ReturnsSuccess()
    {
        var adapter = new ElasticsearchAdapter(MakeConfig());

        var result = await adapter.TestConnectionAsync();

        result.Success.Should().BeTrue();
        result.Latency.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task GetSampleRecords_ReturnsDocuments()
    {
        var adapter = new ElasticsearchAdapter(MakeConfig());

        var records = await adapter.GetSampleRecordsAsync(3);

        records.Should().HaveCount(3);
        records.Should().AllSatisfy(r =>
        {
            r.Fields.Should().ContainKey("level");
            r.Fields.Should().ContainKey("message");
        });
    }

    [Fact]
    public async Task PollErrors_WithTimestampFilter_ReturnsFilteredResults()
    {
        var adapter = new ElasticsearchAdapter(MakeConfig());

        var batch = await adapter.PollErrorsAsync(DateTime.UtcNow.AddHours(-1), 100);

        batch.Entries.Should().HaveCount(5);
        batch.TotalAvailable.Should().Be(5);
    }

    [Fact]
    public async Task GetSchema_ReturnsMappingFields()
    {
        var adapter = new ElasticsearchAdapter(MakeConfig());

        var schema = await adapter.GetSchemaAsync();

        schema.Should().Contain(f => f.Name == "level");
        schema.Should().Contain(f => f.Name == "message");
        schema.Should().Contain(f => f.Name == "service");
    }
}
