using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using NSubstitute;

namespace LogJammer.Tests.Integration.Api;

public class ClassificationControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly IClassificationQueueService _service;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ClassificationControllerTests()
    {
        _client = _factory.CreateAuthenticatedClient();
        _service = _factory.ClassificationQueueService;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetQueue_ShouldReturnEmptyInitially()
    {
        _service.GetPendingAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ClassificationQueuePagedResponse
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 50
            });

        var response = await _client.GetAsync("/api/classification/queue");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ClassificationQueuePagedResponse>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetQueue_WithPendingItem_ShouldReturnItem()
    {
        _service.GetPendingAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ClassificationQueuePagedResponse
            {
                Items = new List<ClassificationQueueResponse>
                {
                    new() { Id = Guid.NewGuid(), KnownErrorId = Guid.NewGuid(), Message = "Test error", CreatedAt = DateTime.UtcNow }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50
            });

        var response = await _client.GetAsync("/api/classification/queue");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ClassificationQueuePagedResponse>(_jsonOptions);
        result!.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetQueueItem_NotFound_ShouldReturn404()
    {
        var id = Guid.NewGuid();
        _service.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ClassificationQueueResponse?)null);

        var response = await _client.GetAsync($"/api/classification/queue/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Approve_ShouldMarkAsReviewed()
    {
        var itemId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        _service.ApproveAsync(itemId, Arg.Any<ApproveClassificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var request = new ApproveClassificationRequest { TagIds = [tagId] };
        var response = await _client.PostAsJsonAsync($"/api/classification/queue/{itemId}/approve", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reject_ShouldCreateOverrideAndMarkReviewed()
    {
        var itemId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        _service.RejectAsync(itemId, Arg.Any<RejectClassificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var request = new RejectClassificationRequest { CorrectTagIds = [tagId], Reason = "Wrong classification" };
        var response = await _client.PostAsJsonAsync($"/api/classification/queue/{itemId}/reject", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
