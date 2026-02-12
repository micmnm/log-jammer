using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using LogJammer.Infrastructure.Adapters.Elasticsearch;
using Testcontainers.Elasticsearch;

namespace LogJammer.Tests.Integration.Adapters;

[Trait("Category", "Docker")]
public class ElasticsearchAdapterIntegrationTests : IAsyncLifetime
{
    private ElasticsearchContainer? _container;
    private string _baseUrl = null!;

    private static bool ShouldSkip =>
        TestDatabaseProvider.UseLocalDb && !TestDatabaseProvider.IsDockerAvailable();

    public async Task InitializeAsync()
    {
        if (ShouldSkip) return;

        _container = new ElasticsearchBuilder("docker.elastic.co/elasticsearch/elasticsearch:8.17.0")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithEnvironment("discovery.type", "single-node")
            .Build();

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
        if (_container is not null)
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

    [SkippableFact]
    public async Task TestConnection_ReturnsSuccess()
    {
        Skip.If(ShouldSkip, "Docker is not available; skipping Elasticsearch tests");

        var adapter = new ElasticsearchAdapter(MakeConfig());

        var result = await adapter.TestConnectionAsync();

        result.Success.Should().BeTrue();
        result.Latency.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [SkippableFact]
    public async Task GetSampleRecords_ReturnsDocuments()
    {
        Skip.If(ShouldSkip, "Docker is not available; skipping Elasticsearch tests");

        var adapter = new ElasticsearchAdapter(MakeConfig());

        var records = await adapter.GetSampleRecordsAsync(3);

        records.Should().HaveCount(3);
        records.Should().AllSatisfy(r =>
        {
            r.Fields.Should().ContainKey("level");
            r.Fields.Should().ContainKey("message");
        });
    }

    [SkippableFact]
    public async Task PollErrors_WithTimestampFilter_ReturnsFilteredResults()
    {
        Skip.If(ShouldSkip, "Docker is not available; skipping Elasticsearch tests");

        var adapter = new ElasticsearchAdapter(MakeConfig());

        var batch = await adapter.PollErrorsAsync(DateTime.UtcNow.AddHours(-1), 100);

        batch.Entries.Should().HaveCount(5);
        batch.TotalAvailable.Should().Be(5);
    }

    [SkippableFact]
    public async Task GetSchema_ReturnsMappingFields()
    {
        Skip.If(ShouldSkip, "Docker is not available; skipping Elasticsearch tests");

        var adapter = new ElasticsearchAdapter(MakeConfig());

        var schema = await adapter.GetSchemaAsync();

        schema.Should().Contain(f => f.Name == "level");
        schema.Should().Contain(f => f.Name == "message");
        schema.Should().Contain(f => f.Name == "service");
    }
}
