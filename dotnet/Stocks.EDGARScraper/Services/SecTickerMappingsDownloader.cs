using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace EDGARScraper.Services;

public class SecTickerMappingsDownloader {
    private static readonly IReadOnlyDictionary<string, string> Urls = new Dictionary<string, string> {
        ["company_tickers.json"] = "https://www.sec.gov/files/company_tickers.json",
        ["company_tickers_exchange.json"] = "https://www.sec.gov/files/company_tickers_exchange.json"
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<SecTickerMappingsDownloader> _logger;

    public SecTickerMappingsDownloader(HttpClient httpClient, ILogger<SecTickerMappingsDownloader> logger) {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result> DownloadAsync(string outputDir, string userAgent, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(outputDir))
            return Result.Failure(ErrorCodes.GenericError, "Sec ticker mappings output directory is required.");
        if (string.IsNullOrWhiteSpace(userAgent))
            return Result.Failure(ErrorCodes.GenericError, "Sec ticker mappings User-Agent is required.");

        _ = Directory.CreateDirectory(outputDir);

        foreach ((string fileName, string url) in Urls) {
            HttpResponseMessage response;
            try {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", userAgent);
                response = await _httpClient.SendAsync(request, ct);
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to download {Url}", url);
                return Result.Failure(ErrorCodes.GenericError, $"Failed to download {url}: {ex.Message}");
            }

            if (!response.IsSuccessStatusCode) {
                _logger.LogWarning("Failed to download {Url}. Status: {StatusCode}", url, response.StatusCode);
                return Result.Failure(ErrorCodes.GenericError, $"Failed to download {url}: {response.StatusCode}");
            }

            string content = await response.Content.ReadAsStringAsync(ct);
            string outputPath = Path.Combine(outputDir, fileName);
            await File.WriteAllTextAsync(outputPath, content, ct);
        }

        return Result.Success;
    }
}
