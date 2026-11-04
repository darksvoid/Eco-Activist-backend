using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EcoActivist.Tests.Infrastructure;

public class HealthCheckSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthCheckSmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_HealthCheck_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<HealthStatusResponse>();
        Assert.NotNull(result);
        Assert.Equal("Healthy", result.Status);
        Assert.NotEqual(default, result.Timestamp);
    }

    private class HealthStatusResponse
    {
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
    }
}