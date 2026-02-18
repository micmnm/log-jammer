using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using NSubstitute;

namespace LogJammer.Tests.Integration.Api;

public class TagsControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly ITagService _service;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public TagsControllerTests()
    {
        _client = _factory.CreateAuthenticatedClient();
        _service = _factory.TagService;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetAll_ShouldReturnTags()
    {
        _service.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<TagResponse>
            {
                new() { Id = Guid.NewGuid(), Name = "database", TagType = "auto" },
                new() { Id = Guid.NewGuid(), Name = "network", TagType = "auto" }
            });

        var response = await _client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await response.Content.ReadFromJsonAsync<List<TagResponse>>(_jsonOptions);
        tags.Should().NotBeNull();
        tags!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedTag()
    {
        var tagId = Guid.NewGuid();
        _service.CreateAsync(Arg.Any<CreateTagRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TagResponse { Id = tagId, Name = "custom-test-tag", TagType = "user", Color = "#ff0000" });

        var request = new CreateTagRequest { Name = "custom-test-tag", TagType = "user", Color = "#ff0000" };
        var response = await _client.PostAsJsonAsync("/api/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag = await response.Content.ReadFromJsonAsync<TagResponse>(_jsonOptions);
        tag.Should().NotBeNull();
        tag!.Name.Should().Be("custom-test-tag");
        tag.TagType.Should().Be("user");
        tag.Color.Should().Be("#ff0000");
    }

    [Fact]
    public async Task GetById_ShouldReturnTag()
    {
        var tagId = Guid.NewGuid();
        _service.GetByIdAsync(tagId, Arg.Any<CancellationToken>())
            .Returns(new TagResponse { Id = tagId, Name = "get-by-id-tag", TagType = "user" });

        var response = await _client.GetAsync($"/api/tags/{tagId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tag = await response.Content.ReadFromJsonAsync<TagResponse>(_jsonOptions);
        tag!.Name.Should().Be("get-by-id-tag");
    }

    [Fact]
    public async Task Update_ShouldModifyTag()
    {
        var tagId = Guid.NewGuid();
        _service.UpdateAsync(tagId, Arg.Any<UpdateTagRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TagResponse { Id = tagId, Name = "updated-tag", TagType = "user", Color = "#ffffff" });

        var updateRequest = new UpdateTagRequest { Name = "updated-tag", Color = "#ffffff" };
        var response = await _client.PutAsJsonAsync($"/api/tags/{tagId}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tag = await response.Content.ReadFromJsonAsync<TagResponse>(_jsonOptions);
        tag!.Name.Should().Be("updated-tag");
        tag.Color.Should().Be("#ffffff");
    }

    [Fact]
    public async Task Delete_ShouldRemoveTag()
    {
        var tagId = Guid.NewGuid();
        _service.DeleteAsync(tagId, Arg.Any<CancellationToken>())
            .Returns(true);

        var deleteResponse = await _client.DeleteAsync($"/api/tags/{tagId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetById_NotFound_ShouldReturn404()
    {
        var tagId = Guid.NewGuid();
        _service.GetByIdAsync(tagId, Arg.Any<CancellationToken>())
            .Returns((TagResponse?)null);

        var response = await _client.GetAsync($"/api/tags/{tagId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
