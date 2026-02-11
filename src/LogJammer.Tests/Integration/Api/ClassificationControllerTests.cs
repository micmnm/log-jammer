using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LogJammer.Api.Dtos;
using LogJammer.Core.Entities;
using LogJammer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace LogJammer.Tests.Integration.Api;

public class ClassificationControllerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
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

    [Fact]
    public async Task GetQueue_ShouldReturnEmptyInitially()
    {
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
        // Seed a queue item via direct DB access
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
            var dataSource = new DataSource
            {
                Name = "Test", AdapterType = Core.Enums.AdapterType.LogFile, ConnectionConfig = "{}"
            };
            db.DataSources.Add(dataSource);
            await db.SaveChangesAsync();

            var knownError = new KnownError
            {
                FingerprintHash = "test-fp",
                RepresentativeMessage = "Test error",
                DataSourceId = dataSource.Id,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                TotalOccurrences = 1
            };
            db.KnownErrors.Add(knownError);
            await db.SaveChangesAsync();

            db.ClassificationQueue.Add(new ClassificationQueueItem
            {
                KnownErrorId = knownError.Id
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/classification/queue");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ClassificationQueuePagedResponse>(_jsonOptions);
        result!.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetQueueItem_NotFound_ShouldReturn404()
    {
        var response = await _client.GetAsync($"/api/classification/queue/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Approve_ShouldMarkAsReviewed()
    {
        Guid itemId;
        Guid tagId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
            var dataSource = new DataSource
            {
                Name = "Test2", AdapterType = Core.Enums.AdapterType.LogFile, ConnectionConfig = "{}"
            };
            db.DataSources.Add(dataSource);
            await db.SaveChangesAsync();

            var knownError = new KnownError
            {
                FingerprintHash = "test-fp-2",
                RepresentativeMessage = "Approval test error",
                DataSourceId = dataSource.Id,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                TotalOccurrences = 1
            };
            db.KnownErrors.Add(knownError);
            await db.SaveChangesAsync();

            var item = new ClassificationQueueItem { KnownErrorId = knownError.Id };
            db.ClassificationQueue.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;

            // Get a seeded tag
            var tag = await db.Tags.FirstAsync();
            tagId = tag.Id;
        }

        var request = new ApproveClassificationRequest { TagIds = [tagId] };
        var response = await _client.PostAsJsonAsync($"/api/classification/queue/{itemId}/approve", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify item is now reviewed
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
            var item = await db.ClassificationQueue.FirstAsync(q => q.Id == itemId);
            item.Reviewed.Should().BeTrue();
            item.ReviewedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Reject_ShouldCreateOverrideAndMarkReviewed()
    {
        Guid itemId;
        Guid tagId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
            var dataSource = new DataSource
            {
                Name = "Test3", AdapterType = Core.Enums.AdapterType.LogFile, ConnectionConfig = "{}"
            };
            db.DataSources.Add(dataSource);
            await db.SaveChangesAsync();

            var knownError = new KnownError
            {
                FingerprintHash = "test-fp-3",
                RepresentativeMessage = "Reject test error",
                DataSourceId = dataSource.Id,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                TotalOccurrences = 1
            };
            db.KnownErrors.Add(knownError);
            await db.SaveChangesAsync();

            var item = new ClassificationQueueItem { KnownErrorId = knownError.Id };
            db.ClassificationQueue.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;

            var tag = await db.Tags.FirstAsync();
            tagId = tag.Id;
        }

        var request = new RejectClassificationRequest { CorrectTagIds = [tagId], Reason = "Wrong classification" };
        var response = await _client.PostAsJsonAsync($"/api/classification/queue/{itemId}/reject", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
            var item = await db.ClassificationQueue.FirstAsync(q => q.Id == itemId);
            item.Reviewed.Should().BeTrue();

            var overrides = await db.UserOverrides.Where(o => o.OverrideType == "classification").ToListAsync();
            overrides.Should().NotBeEmpty();
        }
    }
}
