using System.Net;
using System.Net.Http.Json;

namespace EcoActivist.Tests.Infrastructure;

public class HealthCheckSmokeTests : IClassFixture<NoDatabaseApiFactory>
{
    private readonly HttpClient _client;

    public HealthCheckSmokeTests(NoDatabaseApiFactory factory)
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

    [Theory]
    [InlineData("/api/v1/applications/my")]
    [InlineData("/api/v1/volunteer/profile")]
    [InlineData("/api/v1/admin/articles")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private class HealthStatusResponse
    {
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
    }
}
