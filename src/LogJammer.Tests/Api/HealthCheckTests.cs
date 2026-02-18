using System.Net;
using FluentAssertions;

namespace LogJammer.Tests.Api;

public class HealthCheckTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly HttpClient _anonClient;

    public HealthCheckTests()
    {
        _client = _factory.CreateAuthenticatedClient();
        _anonClient = _factory.CreateClient();
    }

    public void Dispose()
    {
        _anonClient.Dispose();
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
    }

    [Fact]
    public async Task HealthzEndpoint_ReturnsOk()
    {
        var response = await _anonClient.GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
