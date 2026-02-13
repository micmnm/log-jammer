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

public class ErrorGroupsControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly IErrorGroupService _service;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Guid _errorId1 = Guid.NewGuid();
    private readonly Guid _errorId2 = Guid.NewGuid();
    private readonly Guid _dataSourceId = Guid.NewGuid();

    public ErrorGroupsControllerTests()
    {
        _client = _factory.CreateClient();
        _service = _factory.ErrorGroupService;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetAll_ReturnsPagedResults()
    {
        _service.GetAllAsync(
                Arg.Any<Guid?>(), Arg.Any<ErrorStatus?>(), Arg.Any<ErrorSeverity?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ErrorGroupsPagedResponse
            {
                Items = new List<ErrorGroupResponse>
                {
                    new() { Id = _errorId1, FingerprintHash = "abc", RepresentativeMessage = "Error 1", Severity = ErrorSeverity.Critical, Status = ErrorStatus.Active },
                    new() { Id = _errorId2, FingerprintHash = "def", RepresentativeMessage = "Error 2", Severity = ErrorSeverity.Warning, Status = ErrorStatus.Resolved }
                },
                TotalCount = 2,
                Page = 1,
                PageSize = 50
            });

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
        _service.GetAllAsync(
                Arg.Any<Guid?>(), ErrorStatus.Active, Arg.Any<ErrorSeverity?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ErrorGroupsPagedResponse
            {
                Items = new List<ErrorGroupResponse>
                {
                    new() { Id = _errorId1, FingerprintHash = "abc", RepresentativeMessage = "Error 1", Severity = ErrorSeverity.Critical, Status = ErrorStatus.Active }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50
            });

        var response = await _client.GetAsync("/api/errorgroups?status=Active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ErrorGroupsPagedResponse>(_jsonOptions);
        body!.Items.Should().AllSatisfy(i => i.Status.Should().Be(ErrorStatus.Active));
    }

    [Fact]
    public async Task GetAll_FilterBySeverity()
    {
        _service.GetAllAsync(
                Arg.Any<Guid?>(), Arg.Any<ErrorStatus?>(), ErrorSeverity.Critical,
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ErrorGroupsPagedResponse
            {
                Items = new List<ErrorGroupResponse>
                {
                    new() { Id = _errorId1, FingerprintHash = "abc", RepresentativeMessage = "Error 1", Severity = ErrorSeverity.Critical, Status = ErrorStatus.Active }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50
            });

        var response = await _client.GetAsync("/api/errorgroups?severity=Critical");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ErrorGroupsPagedResponse>(_jsonOptions);
        body!.Items.Should().AllSatisfy(i => i.Severity.Should().Be(ErrorSeverity.Critical));
    }

    [Fact]
    public async Task GetById_ReturnsDetailWithStackTrace()
    {
        _service.GetByIdAsync(_errorId1, Arg.Any<CancellationToken>())
            .Returns(new ErrorGroupDetailResponse
            {
                Id = _errorId1,
                FingerprintHash = "abc",
                RepresentativeMessage = "NullReferenceException",
                RepresentativeStackTrace = "at UserService.GetUser()",
                Severity = ErrorSeverity.Critical,
                Status = ErrorStatus.Active,
                DataSourceName = "Test Source"
            });

        var response = await _client.GetAsync($"/api/errorgroups/{_errorId1}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ErrorGroupDetailResponse>(_jsonOptions);
        body.Should().NotBeNull();
        body!.RepresentativeStackTrace.Should().NotBeNullOrEmpty();
        body.DataSourceName.Should().Be("Test Source");
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var id = Guid.NewGuid();
        _service.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ErrorGroupDetailResponse?)null);

        var response = await _client.GetAsync($"/api/errorgroups/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateStatus_ChangesStatus()
    {
        _service.UpdateStatusAsync(_errorId1, ErrorStatus.Ignored, Arg.Any<CancellationToken>())
            .Returns(new ErrorGroupResponse
            {
                Id = _errorId1, FingerprintHash = "abc", RepresentativeMessage = "Error 1",
                Severity = ErrorSeverity.Critical, Status = ErrorStatus.Ignored
            });

        var response = await _client.PutAsJsonAsync(
            $"/api/errorgroups/{_errorId1}/status",
            new UpdateErrorGroupStatusRequest { Status = ErrorStatus.Ignored });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ErrorGroupResponse>(_jsonOptions);
        body!.Status.Should().Be(ErrorStatus.Ignored);
    }

    [Fact]
    public async Task UpdateSeverity_ChangesSeverity()
    {
        _service.UpdateSeverityAsync(_errorId1, ErrorSeverity.Info, Arg.Any<CancellationToken>())
            .Returns(new ErrorGroupResponse
            {
                Id = _errorId1, FingerprintHash = "abc", RepresentativeMessage = "Error 1",
                Severity = ErrorSeverity.Info, Status = ErrorStatus.Active
            });

        var response = await _client.PutAsJsonAsync(
            $"/api/errorgroups/{_errorId1}/severity",
            new UpdateErrorGroupSeverityRequest { Severity = ErrorSeverity.Info });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ErrorGroupResponse>(_jsonOptions);
        body!.Severity.Should().Be(ErrorSeverity.Info);
    }

    [Fact]
    public async Task GetOccurrences_ReturnsEmptyForNewError()
    {
        _service.GetOccurrencesAsync(_errorId1, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new List<ErrorOccurrenceResponse>());

        var response = await _client.GetAsync($"/api/errorgroups/{_errorId1}/occurrences");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ErrorOccurrenceResponse>>(_jsonOptions);
        body.Should().NotBeNull();
    }
}
