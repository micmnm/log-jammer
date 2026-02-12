using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LogJammer.Api.Dtos;
using LogJammer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogJammer.Tests.Integration.Api;

public class TagsControllerTests : IAsyncLifetime
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
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetAll_ShouldReturnSeededTags()
    {
        var response = await _client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await response.Content.ReadFromJsonAsync<List<TagResponse>>(_jsonOptions);
        tags.Should().NotBeNull();
        tags!.Count.Should().BeGreaterThanOrEqualTo(12, "default tags are seeded");
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedTag()
    {
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
        // Create a tag first
        var createRequest = new CreateTagRequest { Name = "get-by-id-tag", TagType = "user" };
        var createResponse = await _client.PostAsJsonAsync("/api/tags", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TagResponse>(_jsonOptions);

        var response = await _client.GetAsync($"/api/tags/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tag = await response.Content.ReadFromJsonAsync<TagResponse>(_jsonOptions);
        tag!.Name.Should().Be("get-by-id-tag");
    }

    [Fact]
    public async Task Update_ShouldModifyTag()
    {
        var createRequest = new CreateTagRequest { Name = "update-test-tag", TagType = "user", Color = "#000000" };
        var createResponse = await _client.PostAsJsonAsync("/api/tags", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TagResponse>(_jsonOptions);

        var updateRequest = new UpdateTagRequest { Name = "updated-tag", Color = "#ffffff" };
        var response = await _client.PutAsJsonAsync($"/api/tags/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tag = await response.Content.ReadFromJsonAsync<TagResponse>(_jsonOptions);
        tag!.Name.Should().Be("updated-tag");
        tag.Color.Should().Be("#ffffff");
    }

    [Fact]
    public async Task Delete_ShouldRemoveTag()
    {
        var createRequest = new CreateTagRequest { Name = "delete-test-tag", TagType = "user" };
        var createResponse = await _client.PostAsJsonAsync("/api/tags", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TagResponse>(_jsonOptions);

        var deleteResponse = await _client.DeleteAsync($"/api/tags/{created!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/tags/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NotFound_ShouldReturn404()
    {
        var response = await _client.GetAsync($"/api/tags/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
