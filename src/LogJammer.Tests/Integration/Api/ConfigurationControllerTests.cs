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
using Testcontainers.PostgreSql;

namespace LogJammer.Tests.Integration.Api;

public class ConfigurationControllerTests : IAsyncLifetime
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
    public async Task Get_ShouldReturnSeededConfigs()
    {
        var response = await _client.GetAsync("/api/configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var configs = await response.Content.ReadFromJsonAsync<List<ConfigurationResponse>>(_jsonOptions);
        configs.Should().NotBeNull();
        configs!.Count.Should().BeGreaterThanOrEqualTo(3, "default classification configs are seeded");
        configs.Should().Contain(c => c.Key == "SimilarityThreshold");
        configs.Should().Contain(c => c.Key == "AutoTagConfidenceThreshold");
        configs.Should().Contain(c => c.Key == "MaxSuggestedTags");
    }

    [Fact]
    public async Task Update_ShouldModifyConfigValue()
    {
        var request = new UpdateConfigurationRequest { Key = "SimilarityThreshold", Value = "0.90" };

        var response = await _client.PutAsJsonAsync("/api/configuration", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<ConfigurationResponse>(_jsonOptions);
        config.Should().NotBeNull();
        config!.Key.Should().Be("SimilarityThreshold");
        config.Value.Should().Be("0.90");
    }

    [Fact]
    public async Task Update_ShouldCreateNewConfigIfNotExists()
    {
        var request = new UpdateConfigurationRequest { Key = "CustomSetting", Value = "42" };

        var response = await _client.PutAsJsonAsync("/api/configuration", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<ConfigurationResponse>(_jsonOptions);
        config!.Key.Should().Be("CustomSetting");
        config.Value.Should().Be("42");
    }
}
