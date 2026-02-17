using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.WebApi.Services;

namespace Stocks.WebApi.Tests;

public class TypeaheadTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly HttpClient _client;
    private readonly DbmInMemoryService _dbm = new();

    public TypeaheadTests(WebApplicationFactory<Program> factory) {
        DbmInMemoryService dbm = _dbm;
        WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            _ = builder.ConfigureServices(services => {
                _ = services.AddSingleton<IDbmService>(dbm);
                _ = services.AddSingleton<TypeaheadTrieService>();
                _ = services.AddHostedService(sp => sp.GetRequiredService<TypeaheadTrieService>());
            }));
        _client = customFactory.CreateClient();
    }

    [Fact]
    public async Task TypeaheadTrieService_Search_ReturnsPrefixMatches() {
        var trieDbm = new DbmInMemoryService();
        var trie = new TypeaheadTrieService(trieDbm);

        _ = await trieDbm.BulkInsertCompanies([
            new Company(1, 320193, "EDGAR"),
            new Company(2, 789019, "EDGAR")
        ], CancellationToken.None);
        _ = await trieDbm.BulkInsertCompanyNames([
            new CompanyName(100, 1, "Apple Inc"),
            new CompanyName(101, 2, "Microsoft Corporation")
        ], CancellationToken.None);
        _ = await trieDbm.BulkInsertCompanyTickers([
            new CompanyTicker(1, "AAPL", "NASDAQ"),
            new CompanyTicker(2, "MSFT", "NASDAQ")
        ], CancellationToken.None);

        await trie.StartAsync(CancellationToken.None);

        List<TypeaheadResult> results = trie.Search("app");
        Assert.NotEmpty(results);
        Assert.Equal("Apple Inc", results[0].Text);
        Assert.Equal("company", results[0].Type);
        Assert.Equal("320193", results[0].Cik);
    }

    [Fact]
    public async Task TypeaheadTrieService_Search_LimitsResults() {
        var trieDbm = new DbmInMemoryService();
        var trie = new TypeaheadTrieService(trieDbm);

        _ = await trieDbm.BulkInsertCompanies([
            new Company(1, 320193, "EDGAR"),
            new Company(2, 789019, "EDGAR"),
            new Company(3, 1018724, "EDGAR")
        ], CancellationToken.None);
        _ = await trieDbm.BulkInsertCompanyNames([
            new CompanyName(100, 1, "Aardvark Inc"),
            new CompanyName(101, 2, "Aaron Corp"),
            new CompanyName(102, 3, "Abacus LLC")
        ], CancellationToken.None);

        await trie.StartAsync(CancellationToken.None);

        List<TypeaheadResult> results = trie.Search("a", 2);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task TypeaheadEndpoint_ReturnsMatches() {
        HttpResponseMessage response = await _client.GetAsync("/api/typeahead?q=test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", body);
    }
}
