using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LogJammer.Api.Dtos;
using LogJammer.Api.Services;
using NSubstitute;

namespace LogJammer.Tests.Integration.Api;

public class ConfigurationControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly IConfigurationService _service;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ConfigurationControllerTests()
    {
        _client = _factory.CreateClient();
        _service = _factory.ConfigurationService;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Get_ShouldReturnConfigs()
    {
        _service.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ConfigurationResponse>
            {
                new() { Key = "SimilarityThreshold", Value = "0.85" },
                new() { Key = "AutoTagConfidenceThreshold", Value = "0.7" },
                new() { Key = "MaxSuggestedTags", Value = "3" }
            });

        var response = await _client.GetAsync("/api/configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var configs = await response.Content.ReadFromJsonAsync<List<ConfigurationResponse>>(_jsonOptions);
        configs.Should().NotBeNull();
        configs!.Count.Should().BeGreaterThanOrEqualTo(3);
        configs.Should().Contain(c => c.Key == "SimilarityThreshold");
        configs.Should().Contain(c => c.Key == "AutoTagConfidenceThreshold");
        configs.Should().Contain(c => c.Key == "MaxSuggestedTags");
    }

    [Fact]
    public async Task Update_ShouldModifyConfigValue()
    {
        _service.UpdateAsync(Arg.Any<UpdateConfigurationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ConfigurationResponse { Key = "SimilarityThreshold", Value = "0.90" });

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
        _service.UpdateAsync(Arg.Any<UpdateConfigurationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ConfigurationResponse { Key = "CustomSetting", Value = "42" });

        var request = new UpdateConfigurationRequest { Key = "CustomSetting", Value = "42" };
        var response = await _client.PutAsJsonAsync("/api/configuration", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<ConfigurationResponse>(_jsonOptions);
        config!.Key.Should().Be("CustomSetting");
        config.Value.Should().Be("42");
    }
}
