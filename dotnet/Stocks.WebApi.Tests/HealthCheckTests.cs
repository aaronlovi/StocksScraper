using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stocks.Persistence.Database;

namespace Stocks.WebApi.Tests;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory) {
        _factory = factory.WithWebHostBuilder(builder =>
            _ = builder.ConfigureServices(services =>
                 _ = services.AddSingleton<IDbmService, DbmInMemoryService>()));
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk() {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", body);
    }

    [Fact]
    public async Task CorsHeaders_AllowAngularOrigin() {
        HttpClient client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", "http://localhost:4201");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Contains("http://localhost:4201",
            response.Headers.GetValues("Access-Control-Allow-Origin"));
    }
}
