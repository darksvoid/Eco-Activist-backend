using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EcoActivist.Tests.Infrastructure;

public class SwaggerSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SwaggerSmokeTests(WebApplicationFactory<Program> factory)
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
    }
    
    [Fact]
    public async Task Get_SwaggerUI_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/swagger/index.html");

        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}