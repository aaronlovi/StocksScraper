using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stocks.Persistence.Database;

namespace Stocks.WebApi.Tests;

public class DashboardEndpointsTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly HttpClient _client;

    public DashboardEndpointsTests(WebApplicationFactory<Program> factory) {
        WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            _ = builder.ConfigureServices(services =>
                _ = services.AddSingleton<IDbmService, DbmInMemoryService>()));
        _client = customFactory.CreateClient();
    }

    [Fact]
    public async Task GetDashboardStats_ReturnsStats() {
        HttpResponseMessage response = await _client.GetAsync("/api/dashboard/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("totalCompanies", body);
        Assert.Contains("totalSubmissions", body);
    }
}
