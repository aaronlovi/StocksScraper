using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDGARScraper.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class StooqPriceDownloaderTests {
    [Fact]
    public async Task DownloadBatchAsync_WritesBatchCsv() {
        string tempDir = Path.Combine(Path.GetTempPath(), $"stooq-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        string mappingsPath = Path.Combine(tempDir, "company_tickers.json");
        string exchangePath = Path.Combine(tempDir, "company_tickers_exchange.json");
        string outputDir = Path.Combine(tempDir, "prices", "stooq");

        File.WriteAllText(mappingsPath, "{ \"0\": { \"cik_str\": 320193, \"ticker\": \"AAPL\" }, \"1\": { \"cik_str\": 789019, \"ticker\": \"MSFT\" } }");
        File.WriteAllText(exchangePath, "{ \"fields\": [\"cik\", \"ticker\", \"exchange\"], \"data\": [[320193, \"AAPL\", \"NASDAQ\"], [789019, \"MSFT\", \"NASDAQ\"]] }");

        var responses = new Dictionary<string, string> {
            ["https://stooq.com/q/d/l/?s=aapl.us&i=d"] = "Date,Open,High,Low,Close,Volume\n2025-12-31,1,2,0.5,1.5,100\n",
            ["https://stooq.com/q/d/l/?s=msft.us&i=d"] = "Date,Open,High,Low,Close,Volume\n2025-12-31,3,4,2.5,3.5,200\n"
        };

        var handler = new FakeHandler(responses);
        using var client = new HttpClient(handler);
        var logger = NullLogger<StooqPriceDownloader>.Instance;
        var downloader = new StooqPriceDownloader(client, logger);

        try {
            Result result = await downloader.DownloadBatchAsync(tempDir, outputDir, "TestAgent/1.0", 0, 5, CancellationToken.None);
            Assert.True(result.IsSuccess);
            string aaplPath = Path.Combine(outputDir, "AAPL.csv");
            string msftPath = Path.Combine(outputDir, "MSFT.csv");
            Assert.True(File.Exists(aaplPath));
            Assert.True(File.Exists(msftPath));

            string aaplOutput = File.ReadAllText(aaplPath);
            string msftOutput = File.ReadAllText(msftPath);
            Assert.True(aaplOutput.Contains("Cik,Ticker,Exchange,StooqSymbol,Date,Open,High,Low,Close,Volume", StringComparison.Ordinal), aaplOutput);
            Assert.True(aaplOutput.Contains("320193,AAPL,NASDAQ,aapl.us,2025-12-31,1,2,0.5,1.5,100", StringComparison.Ordinal), aaplOutput);
            Assert.True(msftOutput.Contains("Cik,Ticker,Exchange,StooqSymbol,Date,Open,High,Low,Close,Volume", StringComparison.Ordinal), msftOutput);
            Assert.True(msftOutput.Contains("789019,MSFT,NASDAQ,msft.us,2025-12-31,3,4,2.5,3.5,200", StringComparison.Ordinal), msftOutput);
        } finally {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private sealed class FakeHandler : HttpMessageHandler {
        private readonly Dictionary<string, string> _responses;

        public FakeHandler(Dictionary<string, string> responses) {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            string requestUrl = request.RequestUri?.ToString() ?? string.Empty;
            if (_responses.TryGetValue(requestUrl, out string? content)) {
                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(content)
                };
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
