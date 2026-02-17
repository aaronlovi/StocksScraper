using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace EDGARScraper.Services;

public class StooqPriceDownloader {
    private readonly HttpClient _httpClient;
    private readonly ILogger<StooqPriceDownloader> _logger;

    public StooqPriceDownloader(HttpClient httpClient, ILogger<StooqPriceDownloader> logger) {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result> DownloadBatchAsync(
        string mappingDir,
        string outputDir,
        string userAgent,
        int delayMilliseconds,
        int maxRetries,
        IReadOnlyCollection<PriceDownloadStatus> downloadStatuses,
        Func<SecTickerMapping, Task<Result>>? onDownloaded,
        CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(mappingDir))
            return Result.Failure(ErrorCodes.ValidationError, "Mapping directory is required.");
        if (string.IsNullOrWhiteSpace(outputDir))
            return Result.Failure(ErrorCodes.ValidationError, "Output directory is required.");
        if (string.IsNullOrWhiteSpace(userAgent))
            return Result.Failure(ErrorCodes.ValidationError, "User-Agent is required.");

        string baseMappingPath = Path.Combine(mappingDir, "company_tickers.json");
        string exchangeMappingPath = Path.Combine(mappingDir, "company_tickers_exchange.json");
        if (!File.Exists(baseMappingPath))
            return Result.Failure(ErrorCodes.NotFound, $"Missing mapping file: {baseMappingPath}");

        var exchangeByCik = new Dictionary<ulong, string>();
        var exchangeByTicker = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(exchangeMappingPath))
            SecTickerJsonParser.LoadExchangeMappings(exchangeMappingPath, exchangeByCik, exchangeByTicker);

        List<SecTickerMapping> mappings = SecTickerJsonParser.LoadBaseMappings(baseMappingPath, exchangeByCik, exchangeByTicker);
        if (mappings.Count == 0)
            return Result.Failure(ErrorCodes.NotFound, "No ticker mappings found.");

        var lastDownloadedByKey = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (PriceDownloadStatus status in downloadStatuses) {
            string key = BuildDownloadKey(status.Cik, status.Ticker, status.Exchange);
            if (!lastDownloadedByKey.ContainsKey(key))
                lastDownloadedByKey[key] = status.LastDownloadedUtc;
        }

        List<DownloadCandidate> candidates = BuildCandidates(mappings, lastDownloadedByKey);
        candidates.Sort(CompareCandidates);
        LogSelectionPreview(candidates, _logger);

        int successCount = 0;
        int failureCount = 0;

        foreach (DownloadCandidate candidate in candidates) {
            SecTickerMapping mapping = candidate.Mapping;
            if (string.IsNullOrWhiteSpace(mapping.Ticker))
                continue;

            string normalizedTicker = mapping.Ticker.Trim().ToUpperInvariant();
            string stooqSymbol = normalizedTicker.ToLowerInvariant() + ".us";
            string url = $"https://stooq.com/q/d/l/?s={stooqSymbol}&i=d";
            _ = Directory.CreateDirectory(outputDir);
            string tickerOutputPath = Path.Combine(outputDir, $"{normalizedTicker}.csv");
            string tempOutputPath = Path.Combine(outputDir, $"{normalizedTicker}.{Guid.NewGuid():N}.tmp");

            bool wroteFile = false;
            int attempt = 0;
            bool hitDailyLimit = false;
            while (attempt < maxRetries && !wroteFile) {
                attempt++;
                HttpResponseMessage response;
                try {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("User-Agent", userAgent);
                    response = await _httpClient.SendAsync(request, ct);
                } catch (Exception ex) {
                    _logger.LogWarning(ex, "Failed to download prices for {Ticker}", normalizedTicker);
                    failureCount++;
                    break;
                }

                if (!response.IsSuccessStatusCode) {
                    _logger.LogWarning("Failed to download prices for {Ticker}. Status: {StatusCode}", normalizedTicker, response.StatusCode);
                    failureCount++;
                    break;
                }

                string content = await response.Content.ReadAsStringAsync(ct);
                bool parseSuccess = false;
                try {
                    await using var writer = new StreamWriter(tempOutputPath);
                    await writer.WriteLineAsync("Cik,Ticker,Exchange,StooqSymbol,Date,Open,High,Low,Close,Volume");
                    parseSuccess = TryWriteStooqCsv(content, mapping, normalizedTicker, stooqSymbol, writer, out string? reason);
                    if (!parseSuccess) {
                        string snippet = content.Length > 1000 ? content[..1000] : content;
                        if (IsDailyHitLimit(reason, content)) {
                            _logger.LogWarning("Stooq daily hit limit reached while processing {Ticker}. Response: {Snippet}", normalizedTicker, snippet);
                            hitDailyLimit = true;
                            break;
                        }
                        if (!string.IsNullOrWhiteSpace(reason)) {
                            _logger.LogWarning("Failed to parse Stooq CSV for {Ticker} on attempt {Attempt}. Status: {StatusCode}. Header: {Header}. Response: {Snippet}", normalizedTicker, attempt, response.StatusCode, reason, snippet);
                        } else {
                            _logger.LogWarning("Failed to parse Stooq CSV for {Ticker} on attempt {Attempt}. Status: {StatusCode}. Response: {Snippet}", normalizedTicker, attempt, response.StatusCode, snippet);
                        }

                        if (attempt < maxRetries) {
                            int backoffMs = delayMilliseconds > 0 ? delayMilliseconds * attempt : 250 * attempt;
                            await Task.Delay(backoffMs, ct);
                        }
                        continue;
                    }
                } finally {
                    if (!parseSuccess && File.Exists(tempOutputPath))
                        File.Delete(tempOutputPath);
                }

                if (!parseSuccess) {
                    continue;
                }

                File.Move(tempOutputPath, tickerOutputPath, true);
                if (onDownloaded is not null) {
                    Result onDownloadedResult = await onDownloaded(mapping);
                    if (onDownloadedResult.IsFailure) {
                        _logger.LogWarning("Failed to record download for {Ticker}. Error: {Error}", normalizedTicker, onDownloadedResult.ErrorMessage);
                        failureCount++;
                        continue;
                    }
                }
                wroteFile = true;
            }

            if (hitDailyLimit)
                return Result.Failure(ErrorCodes.GenericError, "Stooq daily hits limit reached. Retry later.");
            if (!wroteFile) {
                failureCount++;
                continue;
            }

            successCount++;
            if (delayMilliseconds > 0)
                await Task.Delay(delayMilliseconds, ct);
        }

        _logger.LogInformation("Stooq price download completed. Success: {SuccessCount}, Failed: {FailureCount}",
            successCount, failureCount);
        return Result.Success;
    }

    private static List<DownloadCandidate> BuildCandidates(
        IReadOnlyList<SecTickerMapping> mappings,
        Dictionary<string, DateTime> lastDownloadedByKey) {
        var candidates = new List<DownloadCandidate>(mappings.Count);
        for (int i = 0; i < mappings.Count; i++) {
            SecTickerMapping mapping = mappings[i];
            string key = BuildDownloadKey(mapping.Cik, mapping.Ticker, mapping.Exchange);
            DateTime lastDownloadedUtc = DateTime.MinValue;
            if (lastDownloadedByKey.TryGetValue(key, out DateTime recorded))
                lastDownloadedUtc = recorded;
            candidates.Add(new DownloadCandidate(mapping, lastDownloadedUtc, i));
        }
        return candidates;
    }

    private static int CompareCandidates(DownloadCandidate left, DownloadCandidate right) {
        int compareResult = DateTime.Compare(left.LastDownloadedUtc, right.LastDownloadedUtc);
        if (compareResult != 0)
            return compareResult;
        return left.Index.CompareTo(right.Index);
    }

    private static string BuildDownloadKey(ulong cik, string ticker, string? exchange) {
        string normalizedTicker = string.IsNullOrWhiteSpace(ticker) ? string.Empty : ticker.Trim().ToUpperInvariant();
        string normalizedExchange = string.IsNullOrWhiteSpace(exchange) ? string.Empty : exchange.Trim().ToUpperInvariant();
        return $"{cik}|{normalizedTicker}|{normalizedExchange}";
    }

    private sealed record DownloadCandidate(SecTickerMapping Mapping, DateTime LastDownloadedUtc, int Index);

    private static void LogSelectionPreview(
        IReadOnlyList<DownloadCandidate> candidates,
        ILogger<StooqPriceDownloader> logger) {
        if (candidates.Count == 0)
            return;

        const int PreviewCount = 10;
        int count = candidates.Count < PreviewCount ? candidates.Count : PreviewCount;

        logger.LogInformation("Stooq download order preview (first {Count})", count);
        for (int i = 0; i < count; i++) {
            DownloadCandidate candidate = candidates[i];
            string normalizedTicker = candidate.Mapping.Ticker.Trim().ToUpperInvariant();
            string lastDownloaded = candidate.LastDownloadedUtc == DateTime.MinValue
                ? "never"
                : candidate.LastDownloadedUtc.ToString("O", CultureInfo.InvariantCulture);
            logger.LogInformation("  {Index}. {Ticker} (CIK {Cik}, Exchange {Exchange}, LastDownloaded {LastDownloaded})",
                i + 1,
                normalizedTicker,
                candidate.Mapping.Cik,
                candidate.Mapping.Exchange ?? string.Empty,
                lastDownloaded);
        }
    }

    private static bool TryWriteStooqCsv(
        string content,
        SecTickerMapping mapping,
        string normalizedTicker,
        string stooqSymbol,
        StreamWriter writer,
        out string? headerValue) {
        headerValue = null;
        if (string.IsNullOrWhiteSpace(content))
            return false;

        using var reader = new StringReader(content);
        headerValue = reader.ReadLine();
        if (headerValue is null || !headerValue.StartsWith("Date,", StringComparison.OrdinalIgnoreCase))
            return false;

        string? line;
        while ((line = reader.ReadLine()) != null) {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            string[] parts = line.Split(',');
            if (parts.Length < 6)
                continue;

            string date = parts[0].Trim();
            string open = parts[1].Trim();
            string high = parts[2].Trim();
            string low = parts[3].Trim();
            string close = parts[4].Trim();
            string volume = parts[5].Trim();

            writer.WriteLine(string.Join(",",
                mapping.Cik.ToString(CultureInfo.InvariantCulture),
                normalizedTicker,
                mapping.Exchange ?? string.Empty,
                stooqSymbol,
                date,
                open,
                high,
                low,
                close,
                volume));
        }

        return true;
    }

    private static bool IsDailyHitLimit(string? header, string content) {
        if (!string.IsNullOrWhiteSpace(header) && header.Contains("Exceeded the daily hits limit", StringComparison.OrdinalIgnoreCase))
            return true;
        return content.Contains("Exceeded the daily hits limit", StringComparison.OrdinalIgnoreCase);
    }
}

public record SecTickerMapping(ulong Cik, string Ticker, string? Exchange);
