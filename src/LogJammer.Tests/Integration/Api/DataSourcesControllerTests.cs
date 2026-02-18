using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using LogJammer.Core.Enums;
using NSubstitute;

namespace LogJammer.Tests.Integration.Api;

public class DataSourcesControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly IDataSourceService _service;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public DataSourcesControllerTests()
    {
        _client = _factory.CreateAuthenticatedClient();
        _service = _factory.DataSourceService;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Create_ReturnsCreated()
    {
        var response = new DataSourceResponse
        {
            Id = Guid.NewGuid(),
            Name = "Test LogFile Source",
            AdapterType = AdapterType.LogFile,
            ConnectionConfig = "{}",
            Enabled = true,
            PollIntervalSeconds = 30,
            SamplingBudget = 500,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _service.CreateAsync(Arg.Any<CreateDataSourceRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var request = new CreateDataSourceRequest
        {
            Name = "Test LogFile Source",
            AdapterType = AdapterType.LogFile,
            ConnectionConfig = "{}"
        };
        var httpResponse = await _client.PostAsJsonAsync("/api/datasources", request);

        httpResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await httpResponse.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Name.Should().Be("Test LogFile Source");
        body.AdapterType.Should().Be(AdapterType.LogFile);
        body.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_ReturnsCreatedSources()
    {
        _service.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DataSourceResponse>
            {
                new() { Id = Guid.NewGuid(), Name = "Source A", AdapterType = AdapterType.LogFile, ConnectionConfig = "{}" },
                new() { Id = Guid.NewGuid(), Name = "Source B", AdapterType = AdapterType.LogFile, ConnectionConfig = "{}" }
            });

        var response = await _client.GetAsync("/api/datasources");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<DataSourceResponse>>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetById_ReturnsCorrectSource()
    {
        var id = Guid.NewGuid();
        _service.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(new DataSourceResponse { Id = id, Name = "Test Source", AdapterType = AdapterType.LogFile, ConnectionConfig = "{}" });

        var response = await _client.GetAsync($"/api/datasources/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);
        body!.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404WithProblemDetails()
    {
        var id = Guid.NewGuid();
        _service.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((DataSourceResponse?)null);

        var response = await _client.GetAsync($"/api/datasources/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("detail").GetString().Should().Be("Data source not found.");
    }

    [Fact]
    public async Task GetById_WhenServiceThrows_Returns500WithProblemDetails()
    {
        var id = Guid.NewGuid();
        _service.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns<DataSourceResponse?>(_ => throw new InvalidOperationException("Boom"));

        var response = await _client.GetAsync($"/api/datasources/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Update_ReturnsUpdatedSource()
    {
        var id = Guid.NewGuid();
        _service.UpdateAsync(id, Arg.Any<UpdateDataSourceRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DataSourceResponse { Id = id, Name = "Updated Name", AdapterType = AdapterType.LogFile, ConnectionConfig = "{}", Enabled = false });

        var updateRequest = new UpdateDataSourceRequest { Name = "Updated Name", Enabled = false };
        var response = await _client.PutAsJsonAsync($"/api/datasources/{id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);
        body!.Name.Should().Be("Updated Name");
        body.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _service.DeleteAsync(id, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync($"/api/datasources/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TestConnection_WithLogFileSource_ReturnsResult()
    {
        var id = Guid.NewGuid();
        _service.TestConnectionAsync(id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionTestResponse { Success = true, LatencyMs = 5.0 });

        var response = await _client.PostAsync($"/api/datasources/{id}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConnectionTestResponse>(_jsonOptions);
        body!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetSchema_WithLogFileSource_ReturnsFields()
    {
        var id = Guid.NewGuid();
        _service.GetSchemaAsync(id, Arg.Any<CancellationToken>())
            .Returns(new SchemaResponse
            {
                Fields = [new FieldDefinitionDto { Name = "message", Type = "string" }]
            });

        var response = await _client.GetAsync($"/api/datasources/{id}/schema");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SchemaResponse>(_jsonOptions);
        body!.Fields.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSampleRecords_WithLogFileSource_ReturnsRecords()
    {
        var id = Guid.NewGuid();
        _service.GetSampleRecordsAsync(id, 5, Arg.Any<CancellationToken>())
            .Returns(new SampleRecordsResponse
            {
                Records = [new RawLogEntryDto { Timestamp = DateTime.UtcNow, Fields = new() { ["message"] = "test" } }]
            });

        var response = await _client.GetAsync($"/api/datasources/{id}/sample?count=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SampleRecordsResponse>(_jsonOptions);
        body!.Records.Should().NotBeEmpty();
    }
}
