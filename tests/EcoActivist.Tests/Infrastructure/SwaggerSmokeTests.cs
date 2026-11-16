using System.Net;

namespace EcoActivist.Tests.Infrastructure;

public class SwaggerSmokeTests : IClassFixture<NoDatabaseApiFactory>
{
    private readonly HttpClient _client;

    public SwaggerSmokeTests(NoDatabaseApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_SwaggerJson_ReturnsSuccessAndCorrectContentType()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.ToString();
        Assert.NotNull(contentType);
        Assert.Contains("application/json", contentType);

        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/events/{id}/apply", document);
        Assert.Contains("Bearer", document);
    }

    [Fact]
    public async Task Get_SwaggerUI_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/swagger/index.html");

        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
