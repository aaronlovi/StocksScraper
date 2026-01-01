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

public class SecTickerMappingsDownloaderTests {
    [Fact]
    public async Task DownloadAsync_WritesFilesWithExpectedContent() {
        string outputDir = Path.Combine(Path.GetTempPath(), $"sec-ticker-test-{Guid.NewGuid():N}");
        const string userAgent = "TestAgent/1.0 (contact: test@example.com)";

        var responses = new Dictionary<string, string> {
            ["https://www.sec.gov/files/company_tickers.json"] = "{\"ok\":true}",
            ["https://www.sec.gov/files/company_tickers_exchange.json"] = "{\"exchange\":true}"
        };

        var handler = new FakeHandler(responses, userAgent);
        using var client = new HttpClient(handler);
        var logger = NullLogger<SecTickerMappingsDownloader>.Instance;
        var downloader = new SecTickerMappingsDownloader(client, logger);

        try {
            Result result = await downloader.DownloadAsync(outputDir, userAgent, CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.True(handler.SawExpectedUserAgent);

            string tickersPath = Path.Combine(outputDir, "company_tickers.json");
            string exchangePath = Path.Combine(outputDir, "company_tickers_exchange.json");

            Assert.True(File.Exists(tickersPath));
            Assert.True(File.Exists(exchangePath));
            Assert.Equal("{\"ok\":true}", File.ReadAllText(tickersPath));
            Assert.Equal("{\"exchange\":true}", File.ReadAllText(exchangePath));
        } finally {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    private sealed class FakeHandler : HttpMessageHandler {
        private readonly Dictionary<string, string> _responses;
        private readonly string _expectedUserAgent;

        public bool SawExpectedUserAgent { get; private set; }

        public FakeHandler(Dictionary<string, string> responses, string expectedUserAgent) {
            _responses = responses;
            _expectedUserAgent = expectedUserAgent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.Headers.TryGetValues("User-Agent", out IEnumerable<string>? values)) {
                foreach (string value in values) {
                    if (string.Equals(value, _expectedUserAgent, StringComparison.Ordinal)) {
                        SawExpectedUserAgent = true;
                        break;
                    }
                }
            }

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
