using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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

        Dictionary<ulong, string> exchangeByCik = new Dictionary<ulong, string>();
        Dictionary<string, string> exchangeByTicker = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(exchangeMappingPath))
            LoadExchangeMappings(exchangeMappingPath, exchangeByCik, exchangeByTicker);

        List<SecTickerMapping> mappings = LoadBaseMappings(baseMappingPath, exchangeByCik, exchangeByTicker);
        if (mappings.Count == 0)
            return Result.Failure(ErrorCodes.NotFound, "No ticker mappings found.");

        int successCount = 0;
        int failureCount = 0;

        foreach (SecTickerMapping mapping in mappings) {
            if (string.IsNullOrWhiteSpace(mapping.Ticker))
                continue;

            string normalizedTicker = mapping.Ticker.Trim().ToUpperInvariant();
            string stooqSymbol = normalizedTicker.ToLowerInvariant() + ".us";
            string url = $"https://stooq.com/q/d/l/?s={stooqSymbol}&i=d";
            Directory.CreateDirectory(outputDir);
            string tickerOutputPath = Path.Combine(outputDir, $"{normalizedTicker}.csv");

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
                await using var writer = new StreamWriter(tickerOutputPath);
                await writer.WriteLineAsync("Cik,Ticker,Exchange,StooqSymbol,Date,Open,High,Low,Close,Volume");
                if (!TryWriteStooqCsv(content, mapping, normalizedTicker, stooqSymbol, writer, out string? reason)) {
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

        _logger.LogInformation("Stooq price download completed. Success: {SuccessCount}, Failed: {FailureCount}", successCount, failureCount);
        return Result.Success;
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

    private static List<SecTickerMapping> LoadBaseMappings(
        string path,
        Dictionary<ulong, string> exchangeByCik,
        Dictionary<string, string> exchangeByTicker) {
        var results = new List<SecTickerMapping>();
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.ValueKind == JsonValueKind.Object) {
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
                TryAddMapping(prop.Value, exchangeByCik, exchangeByTicker, results);
        } else if (doc.RootElement.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement element in doc.RootElement.EnumerateArray())
                TryAddMapping(element, exchangeByCik, exchangeByTicker, results);
        } else if (TryGetArrayProperty(doc.RootElement, "data", out JsonElement dataArray)) {
            foreach (JsonElement element in dataArray.EnumerateArray())
                TryAddMapping(element, exchangeByCik, exchangeByTicker, results);
        }
        return results;
    }

    private static void TryAddMapping(
        JsonElement element,
        Dictionary<ulong, string> exchangeByCik,
        Dictionary<string, string> exchangeByTicker,
        List<SecTickerMapping> results) {
        if (!TryGetUInt64(element, "cik", "cik_str", out ulong cik))
            return;
        if (!TryGetString(element, out string? ticker, "ticker"))
            return;
        if (string.IsNullOrWhiteSpace(ticker))
            return;
        ticker = ticker.Trim();

        string? exchange = null;
        if (exchangeByCik.TryGetValue(cik, out string? byCik))
            exchange = byCik;
        else if (exchangeByTicker.TryGetValue(ticker, out string? byTicker))
            exchange = byTicker;

        if (!string.IsNullOrWhiteSpace(exchange))
            exchange = NormalizeExchange(exchange);

        results.Add(new SecTickerMapping(cik, ticker, exchange));
    }

    private static void LoadExchangeMappings(
        string path,
        Dictionary<ulong, string> exchangeByCik,
        Dictionary<string, string> exchangeByTicker) {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        if (TryLoadExchangeMappingsFromFields(doc.RootElement, exchangeByCik, exchangeByTicker))
            return;

        if (doc.RootElement.ValueKind == JsonValueKind.Object) {
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
                TryAddExchangeMapping(prop.Value, exchangeByCik, exchangeByTicker);
        } else if (doc.RootElement.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement element in doc.RootElement.EnumerateArray())
                TryAddExchangeMapping(element, exchangeByCik, exchangeByTicker);
        } else if (TryGetArrayProperty(doc.RootElement, "data", out JsonElement dataArray)) {
            foreach (JsonElement element in dataArray.EnumerateArray())
                TryAddExchangeMapping(element, exchangeByCik, exchangeByTicker);
        }
    }

    private static void TryAddExchangeMapping(
        JsonElement element,
        Dictionary<ulong, string> exchangeByCik,
        Dictionary<string, string> exchangeByTicker) {
        if (!TryGetString(element, out string? exchange, "exchange", "exch", "exchangeName", "market"))
            return;
        if (string.IsNullOrWhiteSpace(exchange))
            return;
        exchange = NormalizeExchange(exchange);

        if (TryGetUInt64(element, "cik", "cik_str", out ulong cik)) {
            if (!exchangeByCik.ContainsKey(cik))
                exchangeByCik[cik] = exchange;
        }

        if (TryGetString(element, out string? ticker, "ticker")) {
            if (!string.IsNullOrWhiteSpace(ticker)) {
                ticker = ticker.Trim();
                if (!exchangeByTicker.ContainsKey(ticker))
                    exchangeByTicker[ticker] = exchange;
            }
        }
    }

    private static bool TryGetString(JsonElement element, out string? value, params string[] names) {
        if (element.ValueKind != JsonValueKind.Object) {
            value = null;
            return false;
        }
        foreach (string name in names) {
            if (element.TryGetProperty(name, out JsonElement prop) && prop.ValueKind == JsonValueKind.String) {
                value = prop.GetString();
                return true;
            }
        }
        value = null;
        return false;
    }

    private static bool TryGetUInt64(JsonElement element, string name, string altName, out ulong value) {
        if (TryGetUInt64(element, name, out value))
            return true;
        if (TryGetUInt64(element, altName, out value))
            return true;
        value = 0;
        return false;
    }

    private static bool TryGetUInt64(JsonElement element, string name, out ulong value) {
        if (element.ValueKind != JsonValueKind.Object) {
            value = 0;
            return false;
        }
        if (element.TryGetProperty(name, out JsonElement prop)) {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetUInt64(out value))
                return true;
            if (prop.ValueKind == JsonValueKind.String && ulong.TryParse(prop.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out value))
                return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetArrayProperty(JsonElement element, string propertyName, out JsonElement arrayElement) {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out JsonElement prop) &&
            prop.ValueKind == JsonValueKind.Array) {
            arrayElement = prop;
            return true;
        }
        arrayElement = default;
        return false;
    }

    private static bool TryLoadExchangeMappingsFromFields(
        JsonElement root,
        Dictionary<ulong, string> exchangeByCik,
        Dictionary<string, string> exchangeByTicker) {
        if (!TryGetArrayProperty(root, "fields", out JsonElement fieldsElement))
            return false;
        if (!TryGetArrayProperty(root, "data", out JsonElement dataElement))
            return false;

        var fieldIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int index = 0;
        foreach (JsonElement field in fieldsElement.EnumerateArray()) {
            if (field.ValueKind == JsonValueKind.String) {
                string? name = field.GetString();
                if (!string.IsNullOrWhiteSpace(name) && !fieldIndexes.ContainsKey(name))
                    fieldIndexes[name] = index;
            }
            index++;
        }

        if (fieldIndexes.Count == 0)
            return false;

        int initialCikCount = exchangeByCik.Count;
        int initialTickerCount = exchangeByTicker.Count;

        foreach (JsonElement row in dataElement.EnumerateArray()) {
            if (row.ValueKind != JsonValueKind.Array)
                continue;

            string? exchange = GetFieldString(row, fieldIndexes, "exchange", "exch", "exchangeName", "market");
            if (string.IsNullOrWhiteSpace(exchange))
                continue;
            exchange = NormalizeExchange(exchange);

            ulong cik = GetFieldUInt64(row, fieldIndexes, "cik", "cik_str");
            if (cik > 0 && !exchangeByCik.ContainsKey(cik))
                exchangeByCik[cik] = exchange;

            string? ticker = GetFieldString(row, fieldIndexes, "ticker");
            if (!string.IsNullOrWhiteSpace(ticker)) {
                ticker = ticker.Trim();
                if (!exchangeByTicker.ContainsKey(ticker))
                    exchangeByTicker[ticker] = exchange;
            }
        }

        return exchangeByCik.Count > initialCikCount || exchangeByTicker.Count > initialTickerCount;
    }

    private static string? GetFieldString(JsonElement row, Dictionary<string, int> fieldIndexes, params string[] names) {
        foreach (string name in names) {
            if (fieldIndexes.TryGetValue(name, out int index)) {
                if (index < row.GetArrayLength()) {
                    JsonElement value = row[index];
                    if (value.ValueKind == JsonValueKind.String)
                        return value.GetString();
                }
            }
        }
        return null;
    }

    private static ulong GetFieldUInt64(JsonElement row, Dictionary<string, int> fieldIndexes, params string[] names) {
        foreach (string name in names) {
            if (fieldIndexes.TryGetValue(name, out int index)) {
                if (index < row.GetArrayLength()) {
                    JsonElement value = row[index];
                    if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out ulong result))
                        return result;
                    if (value.ValueKind == JsonValueKind.String &&
                        ulong.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out result))
                        return result;
                }
            }
        }
        return 0;
    }

    private static string NormalizeExchange(string exchange) {
        string trimmed = exchange.Trim();
        if (trimmed.Length == 0)
            return trimmed;
        return trimmed.ToUpperInvariant();
    }
}

public record SecTickerMapping(ulong Cik, string Ticker, string? Exchange);
