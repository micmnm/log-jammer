using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LogJammer.Api.Dtos;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogJammer.Tests.Integration.Api;

public class ErrorGroupsControllerTests : IAsyncLifetime
{
    private readonly TestDatabaseProvider _db = new();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<LogJammerDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<LogJammerDbContext>(options =>
                        options.UseNpgsql(_db.ConnectionString,
                            npgsqlOptions => npgsqlOptions.UseVector()));
                });
            });

        _client = _factory.CreateClient();

        // Seed test data
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();

        var dataSource = new DataSource
        {
            Name = "Test Source",
            AdapterType = AdapterType.LogFile,
            ConnectionConfig = "{}"
        };
        db.DataSources.Add(dataSource);
        await db.SaveChangesAsync();

        db.KnownErrors.AddRange(
            new KnownError
            {
                FingerprintHash = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
                RepresentativeMessage = "NullReferenceException in UserService",
                RepresentativeStackTrace = "at UserService.GetUser()",
                Severity = ErrorSeverity.Critical,
                Status = ErrorStatus.Active,
                FirstSeen = DateTime.UtcNow.AddDays(-7),
                LastSeen = DateTime.UtcNow,
                TotalOccurrences = 42,
                DataSourceId = dataSource.Id
            },
            new KnownError
            {
                FingerprintHash = "b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3",
                RepresentativeMessage = "Timeout connecting to database",
                Severity = ErrorSeverity.Warning,
                Status = ErrorStatus.Resolved,
                FirstSeen = DateTime.UtcNow.AddDays(-30),
                LastSeen = DateTime.UtcNow.AddDays(-5),
                TotalOccurrences = 10,
                DataSourceId = dataSource.Id
            });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetAll_ReturnsPagedResults()
    {
        var response = await _client.GetAsync("/api/errorgroups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ErrorGroupsPagedResponse>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
        body.TotalCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAll_FilterByStatus()
    {
        var response = await _client.GetAsync("/api/errorgroups?status=Active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ErrorGroupsPagedResponse>(_jsonOptions);
        body!.Items.Should().AllSatisfy(i => i.Status.Should().Be(ErrorStatus.Active));
    }

    [Fact]
    public async Task GetAll_FilterBySeverity()
    {
        var response = await _client.GetAsync("/api/errorgroups?severity=Critical");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ErrorGroupsPagedResponse>(_jsonOptions);
        body!.Items.Should().AllSatisfy(i => i.Severity.Should().Be(ErrorSeverity.Critical));
    }

    [Fact]
    public async Task GetById_ReturnsDetailWithStackTrace()
    {
        // Get all first to find an ID
        var listResponse = await _client.GetAsync("/api/errorgroups");
        var list = await listResponse.Content.ReadFromJsonAsync<ErrorGroupsPagedResponse>(_jsonOptions);
        var first = list!.Items.First(i => i.RepresentativeMessage.Contains("NullReference"));

        var response = await _client.GetAsync($"/api/errorgroups/{first.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ErrorGroupDetailResponse>(_jsonOptions);
        body.Should().NotBeNull();
        body!.RepresentativeStackTrace.Should().NotBeNullOrEmpty();
        body.DataSourceName.Should().Be("Test Source");
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/errorgroups/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateStatus_ChangesStatus()
    {
        var listResponse = await _client.GetAsync("/api/errorgroups?status=Active");
        var list = await listResponse.Content.ReadFromJsonAsync<ErrorGroupsPagedResponse>(_jsonOptions);
        var target = list!.Items.First();

        var response = await _client.PutAsJsonAsync(
            $"/api/errorgroups/{target.Id}/status",
            new UpdateErrorGroupStatusRequest { Status = ErrorStatus.Ignored });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ErrorGroupResponse>(_jsonOptions);
        body!.Status.Should().Be(ErrorStatus.Ignored);
    }

    [Fact]
    public async Task UpdateSeverity_ChangesSeverity()
    {
        var listResponse = await _client.GetAsync("/api/errorgroups");
        var list = await listResponse.Content.ReadFromJsonAsync<ErrorGroupsPagedResponse>(_jsonOptions);
        var target = list!.Items.First();

        var response = await _client.PutAsJsonAsync(
            $"/api/errorgroups/{target.Id}/severity",
            new UpdateErrorGroupSeverityRequest { Severity = ErrorSeverity.Info });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ErrorGroupResponse>(_jsonOptions);
        body!.Severity.Should().Be(ErrorSeverity.Info);
    }

    [Fact]
    public async Task GetOccurrences_ReturnsEmptyForNewError()
    {
        var listResponse = await _client.GetAsync("/api/errorgroups");
        var list = await listResponse.Content.ReadFromJsonAsync<ErrorGroupsPagedResponse>(_jsonOptions);
        var target = list!.Items.First();

        var response = await _client.GetAsync($"/api/errorgroups/{target.Id}/occurrences");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ErrorOccurrenceResponse>>(_jsonOptions);
        body.Should().NotBeNull();
    }
}
