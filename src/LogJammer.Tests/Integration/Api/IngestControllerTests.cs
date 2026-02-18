using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LogJammer.Tests.Integration.Api;

public class IngestControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly IIngestService _ingestService;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IngestControllerTests()
    {
        _client = _factory.CreateAuthenticatedClient();
        _ingestService = _factory.IngestService;
    }

    [Fact]
    public async Task Ingest_ValidRequest_ReturnsOkWithCounts()
    {
        var dataSourceId = Guid.NewGuid();
        _ingestService.IngestAsync(
            dataSourceId,
            Arg.Any<IReadOnlyList<(DateTime, Dictionary<string, object?>)>>(),
            Arg.Any<CancellationToken>())
            .Returns((5, 2, 0));

        var request = new IngestRequest
        {
            Entries =
            [
                new IngestEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Fields = new Dictionary<string, object?> { ["message"] = "test error" }
                }
            ]
        };

        var response = await _client.PostAsJsonAsync($"/api/ingest/{dataSourceId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IngestResponse>(JsonOptions);
        body!.Accepted.Should().Be(5);
        body.Duplicates.Should().Be(2);
        body.Failed.Should().Be(0);
    }

    [Fact]
    public async Task Ingest_DataSourceNotFound_Returns404()
    {
        var dataSourceId = Guid.NewGuid();
        _ingestService.IngestAsync(
            dataSourceId,
            Arg.Any<IReadOnlyList<(DateTime, Dictionary<string, object?>)>>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new KeyNotFoundException("Not found"));

        var request = new IngestRequest
        {
            Entries =
            [
                new IngestEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Fields = new Dictionary<string, object?> { ["message"] = "test" }
                }
            ]
        };

        var response = await _client.PostAsJsonAsync($"/api/ingest/{dataSourceId}", request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ingest_NonKibanaProxySource_Returns400()
    {
        var dataSourceId = Guid.NewGuid();
        _ingestService.IngestAsync(
            dataSourceId,
            Arg.Any<IReadOnlyList<(DateTime, Dictionary<string, object?>)>>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Not a KibanaProxy source"));

        var request = new IngestRequest
        {
            Entries =
            [
                new IngestEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Fields = new Dictionary<string, object?> { ["message"] = "test" }
                }
            ]
        };

        var response = await _client.PostAsJsonAsync($"/api/ingest/{dataSourceId}", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
