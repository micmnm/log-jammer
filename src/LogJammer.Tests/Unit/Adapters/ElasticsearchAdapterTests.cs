using System.Text.Json;
using FluentAssertions;
using LogJammer.Infrastructure.Adapters.Elasticsearch;

namespace LogJammer.Tests.Unit.Adapters;

public class ElasticsearchAdapterTests
{
    private static string MakeConfig(string url = "http://localhost:9200",
        string indexPattern = "logs-*", ElasticsearchAuthConfig? auth = null)
    {
        return JsonSerializer.Serialize(new ElasticsearchConnectionConfig
        {
            Url = url,
            IndexPattern = indexPattern,
            Auth = auth
        });
    }

    [Fact]
    public void Constructor_WithValidConfig_Succeeds()
    {
        var config = MakeConfig();
        var adapter = new ElasticsearchAdapter(config);
        adapter.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithBasicAuth_Succeeds()
    {
        var config = MakeConfig(auth: new ElasticsearchAuthConfig
        {
            Type = "basic",
            Username = "user",
            Password = "pass"
        });

        var adapter = new ElasticsearchAdapter(config);

        adapter.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithApiKeyAuth_Succeeds()
    {
        var config = MakeConfig(auth: new ElasticsearchAuthConfig
        {
            Type = "apiKey",
            ApiKey = "test-api-key"
        });

        var adapter = new ElasticsearchAdapter(config);

        adapter.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidJson_Throws()
    {
        var act = () => new ElasticsearchAdapter("not json");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public async Task TestConnection_WithUnreachableHost_ReturnsFailure()
    {
        var config = MakeConfig(url: "http://localhost:19999");
        var adapter = new ElasticsearchAdapter(config);

        var result = await adapter.TestConnectionAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.Latency.Should().BeGreaterThan(TimeSpan.Zero);
    }
}
