using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LogJammer.Api.Dtos;
using LogJammer.Core.Enums;
using LogJammer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace LogJammer.Tests.Integration.Api;

public class DataSourcesControllerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<LogJammerDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<LogJammerDbContext>(options =>
                        options.UseNpgsql(_container.GetConnectionString(),
                            npgsqlOptions => npgsqlOptions.UseVector()));
                });
            });

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _container.DisposeAsync().AsTask();
    }

    private CreateDataSourceRequest MakeLogFileRequest(string name = "Test LogFile Source")
    {
        // Create a temp file for LogFile adapter
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "{\"timestamp\":\"2024-01-01T00:00:00Z\",\"level\":\"error\",\"message\":\"test\"}\n");

        return new CreateDataSourceRequest
        {
            Name = name,
            AdapterType = AdapterType.LogFile,
            ConnectionConfig = JsonSerializer.Serialize(new
            {
                filePaths = new[] { tempFile },
                parseMode = "jsonlines",
                timestampField = "timestamp"
            })
        };
    }

    [Fact]
    public async Task Create_ReturnsCreated()
    {
        var request = MakeLogFileRequest();

        var response = await _client.PostAsJsonAsync("/api/datasources", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Name.Should().Be("Test LogFile Source");
        body.AdapterType.Should().Be(AdapterType.LogFile);
        body.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_ReturnsCreatedSources()
    {
        await _client.PostAsJsonAsync("/api/datasources", MakeLogFileRequest("Source A"));
        await _client.PostAsJsonAsync("/api/datasources", MakeLogFileRequest("Source B"));

        var response = await _client.GetAsync("/api/datasources");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<DataSourceResponse>>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetById_ReturnsCorrectSource()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/datasources", MakeLogFileRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);

        var response = await _client.GetAsync($"/api/datasources/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);
        body!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync($"/api/datasources/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ReturnsUpdatedSource()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/datasources", MakeLogFileRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);

        var updateRequest = new UpdateDataSourceRequest { Name = "Updated Name", Enabled = false };
        var response = await _client.PutAsJsonAsync($"/api/datasources/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);
        body!.Name.Should().Be("Updated Name");
        body.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/datasources", MakeLogFileRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);

        var response = await _client.DeleteAsync($"/api/datasources/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/api/datasources/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestConnection_WithLogFileSource_ReturnsResult()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/datasources", MakeLogFileRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);

        var response = await _client.PostAsync($"/api/datasources/{created!.Id}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConnectionTestResponse>(_jsonOptions);
        body!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetSchema_WithLogFileSource_ReturnsFields()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/datasources", MakeLogFileRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);

        var response = await _client.GetAsync($"/api/datasources/{created!.Id}/schema");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SchemaResponse>(_jsonOptions);
        body!.Fields.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSampleRecords_WithLogFileSource_ReturnsRecords()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/datasources", MakeLogFileRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<DataSourceResponse>(_jsonOptions);

        var response = await _client.GetAsync($"/api/datasources/{created!.Id}/sample?count=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SampleRecordsResponse>(_jsonOptions);
        body!.Records.Should().NotBeEmpty();
    }
}
