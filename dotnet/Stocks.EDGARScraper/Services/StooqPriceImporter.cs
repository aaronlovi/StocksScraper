using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace EDGARScraper.Services;

public sealed class StooqPriceImporter {
    private const string DailyLimitMessage = "Exceeded the daily hits limit";

    private readonly IDbmService _dbm;
    private readonly ILogger<StooqPriceImporter> _logger;

    public StooqPriceImporter(IDbmService dbm, ILogger<StooqPriceImporter> logger) {
        _dbm = dbm;
        _logger = logger;
    }

    public async Task<Result> ImportAsync(
        string mappingDir,
        string pricesDir,
        int maxTickers,
        int batchSize,
        CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(mappingDir))
            return Result.Failure(ErrorCodes.ValidationError, "Mapping directory is required.");
        if (string.IsNullOrWhiteSpace(pricesDir))
            return Result.Failure(ErrorCodes.ValidationError, "Prices directory is required.");

        string mappingPath = Path.Combine(mappingDir, "company_tickers.json");
        if (!File.Exists(mappingPath))
            return Result.Failure(ErrorCodes.NotFound, $"Missing mapping file: {mappingPath}");

        string exchangeMappingPath = Path.Combine(mappingDir, "company_tickers_exchange.json");
        var exchangeByCik = new Dictionary<ulong, string>();
        var exchangeByTicker = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(exchangeMappingPath))
            LoadExchangeMappings(exchangeMappingPath, exchangeByCik, exchangeByTicker);

        List<MappingRecord> mappings = LoadMappings(mappingPath, exchangeByCik, exchangeByTicker);
        if (mappings.Count == 0)
            return Result.Failure(ErrorCodes.NotFound, "No tickers found in mapping file.");

        Result<IReadOnlyCollection<PriceImportStatus>> importResult = await _dbm.GetPriceImportStatuses(ct);
        if (importResult.IsFailure)
            return Result.Failure(importResult);

        var lastImportedByKey = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (PriceImportStatus status in importResult.Value ?? []) {
            if (string.IsNullOrWhiteSpace(status.Ticker))
                continue;
            string key = BuildImportKey(status.Cik, status.Ticker, status.Exchange);
            if (!lastImportedByKey.ContainsKey(key))
                lastImportedByKey[key] = status.LastImportedUtc;
        }

        List<ImportCandidate> candidates = BuildCandidates(mappings, lastImportedByKey);
        if (candidates.Count == 0)
            return Result.Failure(ErrorCodes.NotFound, "No valid ticker candidates found.");

        candidates.Sort(CompareCandidates);

        int tickerLimit = maxTickers > 0 ? maxTickers : candidates.Count;
        int successCount = 0;
        int failureCount = 0;

        for (int i = 0; i < candidates.Count && successCount + failureCount < tickerLimit; i++) {
            ImportCandidate candidate = candidates[i];
            ImportOutcome outcome = await ImportTickerAsync(candidate, pricesDir, batchSize, ct);
            if (outcome.HitDailyLimit)
                return Result.Failure(ErrorCodes.GenericError, "Daily hits limit reached. Retry later.");
            if (outcome.Result.IsFailure) {
                failureCount++;
                continue;
            }
            successCount++;
        }

        _logger.LogInformation("Price import completed. Success: {SuccessCount}, Failed: {FailureCount}", successCount, failureCount);
        return Result.Success;
    }

    private async Task<ImportOutcome> ImportTickerAsync(ImportCandidate candidate, string pricesDir, int batchSize, CancellationToken ct) {
        string normalizedTicker = NormalizeTicker(candidate.Ticker);
        if (string.IsNullOrWhiteSpace(normalizedTicker))
            return ImportOutcome.Failure(Result.Failure(ErrorCodes.ValidationError, "Ticker is empty."));

        string csvPath = Path.Combine(pricesDir, $"{normalizedTicker}.csv");
        if (!File.Exists(csvPath)) {
            _logger.LogWarning("Missing prices file for {Ticker}: {Path}", normalizedTicker, csvPath);
            return ImportOutcome.Failure(Result.Failure(ErrorCodes.NotFound, $"Missing prices file: {csvPath}"));
        }

        int effectiveBatchSize = batchSize > 0 ? batchSize : 500;
        var pending = new List<ParsedPriceRow>(effectiveBatchSize);
        bool deletedExisting = false;
        int totalRows = 0;
        ParsedPriceRow? firstRow = null;
        async Task<Result> EnsureDeletedAsync() {
            if (deletedExisting)
                return Result.Success;
            Result deleteResult = await _dbm.DeletePricesForTicker(normalizedTicker, ct);
            if (deleteResult.IsFailure)
                return deleteResult;
            deletedExisting = true;
            return Result.Success;
        }

        try {
            using var reader = new StreamReader(csvPath);
            string? header = await reader.ReadLineAsync(ct);
            if (header is null)
                return ImportOutcome.Failure(Result.Failure(ErrorCodes.ParsingError, $"Missing header in {csvPath}."));
            if (!header.StartsWith("Cik,", StringComparison.OrdinalIgnoreCase)) {
                if (header.Contains(DailyLimitMessage, StringComparison.OrdinalIgnoreCase))
                    return ImportOutcome.DailyLimit(Result.Failure(ErrorCodes.GenericError, "Daily hits limit reached."));
                _logger.LogWarning("Unexpected header for {Ticker}: {Header}", normalizedTicker, header);
                return ImportOutcome.Failure(Result.Failure(ErrorCodes.ParsingError, $"Unexpected header in {csvPath}."));
            }

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null) {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (line.Contains(DailyLimitMessage, StringComparison.OrdinalIgnoreCase))
                    return ImportOutcome.DailyLimit(Result.Failure(ErrorCodes.GenericError, "Daily hits limit reached."));

                if (!TryParseLine(line, out ParsedPriceRow row)) {
                    _logger.LogWarning("Failed to parse price row for {Ticker}. Line: {Line}", normalizedTicker, line);
                    return ImportOutcome.Failure(Result.Failure(ErrorCodes.ParsingError, $"Failed to parse CSV row for {normalizedTicker}."));
                }

                pending.Add(row);
                if (!firstRow.HasValue)
                    firstRow = row;
                if (pending.Count >= effectiveBatchSize) {
                    Result deleteResult = await EnsureDeletedAsync();
                    if (deleteResult.IsFailure)
                        return ImportOutcome.Failure(deleteResult);
                    Result batchResult = await InsertBatchAsync(pending, normalizedTicker, ct);
                    if (batchResult.IsFailure)
                        return ImportOutcome.Failure(batchResult);
                    totalRows += pending.Count;
                    pending.Clear();
                }
            }

            if (pending.Count > 0) {
                Result deleteResult = await EnsureDeletedAsync();
                if (deleteResult.IsFailure)
                    return ImportOutcome.Failure(deleteResult);
                Result batchResult = await InsertBatchAsync(pending, normalizedTicker, ct);
                if (batchResult.IsFailure)
                    return ImportOutcome.Failure(batchResult);
                totalRows += pending.Count;
                pending.Clear();
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to import prices for {Ticker}", normalizedTicker);
            return ImportOutcome.Failure(Result.Failure(ErrorCodes.GenericError, $"Failed to import {normalizedTicker}: {ex.Message}"));
        }

        if (totalRows == 0) {
            _logger.LogWarning("No price rows found for {Ticker}", normalizedTicker);
            return ImportOutcome.Failure(Result.Failure(ErrorCodes.NotFound, $"No price rows found for {normalizedTicker}."));
        }

        ulong statusCik = firstRow?.Cik ?? candidate.Cik;
        string statusTicker = firstRow?.Ticker ?? normalizedTicker;
        string? statusExchange = firstRow?.Exchange ?? candidate.Exchange;
        var status = new PriceImportStatus(statusCik, statusTicker, statusExchange, DateTime.UtcNow);
        Result statusResult = await _dbm.UpsertPriceImport(status, ct);
        if (statusResult.IsFailure)
            return ImportOutcome.Failure(statusResult);

        return ImportOutcome.Success;
    }

    private async Task<Result> InsertBatchAsync(
        List<ParsedPriceRow> batch,
        string normalizedTicker,
        CancellationToken ct) {
        if (batch.Count == 0)
            return Result.Success;

        ulong firstId = await _dbm.GetIdRange64((uint)batch.Count, ct);
        ulong currentId = firstId;

        var prices = new List<PriceRow>(batch.Count);
        foreach (ParsedPriceRow row in batch) {
            prices.Add(new PriceRow(
                currentId,
                row.Cik,
                row.Ticker,
                row.Exchange,
                row.StooqSymbol,
                row.PriceDate,
                row.Open,
                row.High,
                row.Low,
                row.Close,
                row.Volume));
            currentId++;
        }

        Result insertResult = await _dbm.BulkInsertPrices(prices, ct);
        if (insertResult.IsFailure)
            _logger.LogWarning("BulkInsertPrices failed for {Ticker}. Error: {Error}", normalizedTicker, insertResult.ErrorMessage);
        return insertResult;
    }

    private static List<ImportCandidate> BuildCandidates(
        List<MappingRecord> mappings,
        Dictionary<string, DateTime> lastImportedByKey) {
        var results = new List<ImportCandidate>(mappings.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (MappingRecord mapping in mappings) {
            string normalizedTicker = NormalizeTicker(mapping.Ticker);
            string? normalizedExchange = NormalizeExchange(mapping.Exchange);
            string key = BuildImportKey(mapping.Cik, normalizedTicker, normalizedExchange);
            if (!seen.Add(key))
                continue;
            DateTime lastImportedUtc = DateTime.MinValue;
            if (lastImportedByKey.TryGetValue(key, out DateTime lastImported))
                lastImportedUtc = lastImported;
            results.Add(new ImportCandidate(mapping.Cik, normalizedTicker, normalizedExchange, lastImportedUtc));
        }
        return results;
    }

    private static int CompareCandidates(ImportCandidate left, ImportCandidate right) {
        int compareResult = DateTime.Compare(left.LastImportedUtc, right.LastImportedUtc);
        if (compareResult != 0)
            return compareResult;
        int tickerCompare = string.Compare(left.Ticker, right.Ticker, StringComparison.OrdinalIgnoreCase);
        if (tickerCompare != 0)
            return tickerCompare;
        int cikCompare = left.Cik.CompareTo(right.Cik);
        if (cikCompare != 0)
            return cikCompare;
        return string.Compare(left.Exchange ?? string.Empty, right.Exchange ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseLine(string line, out ParsedPriceRow row) {
        row = default;
        string[] parts = line.Split(',');
        if (parts.Length < 10)
            return false;

        string cikValue = parts[0].Trim();
        string ticker = NormalizeTicker(parts[1].Trim());
        string exchange = NormalizeExchange(parts[2].Trim()) ?? string.Empty;
        string stooqSymbol = parts[3].Trim();
        string dateValue = parts[4].Trim();
        string openValue = parts[5].Trim();
        string highValue = parts[6].Trim();
        string lowValue = parts[7].Trim();
        string closeValue = parts[8].Trim();
        string volumeValue = parts[9].Trim();

        if (!ulong.TryParse(cikValue, NumberStyles.None, CultureInfo.InvariantCulture, out ulong cik))
            return false;
        if (string.IsNullOrWhiteSpace(ticker))
            return false;
        if (string.IsNullOrWhiteSpace(stooqSymbol))
            return false;
        if (!DateOnly.TryParseExact(dateValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly priceDate))
            return false;
        if (!decimal.TryParse(openValue, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal open))
            return false;
        if (!decimal.TryParse(highValue, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal high))
            return false;
        if (!decimal.TryParse(lowValue, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal low))
            return false;
        if (!decimal.TryParse(closeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal close))
            return false;
        if (!TryParseVolume(volumeValue, out long volume))
            return false;

        row = new ParsedPriceRow(
            cik,
            ticker,
            string.IsNullOrWhiteSpace(exchange) ? null : exchange,
            stooqSymbol,
            priceDate,
            open,
            high,
            low,
            close,
            volume);
        return true;
    }

    private static List<MappingRecord> LoadMappings(
        string path,
        Dictionary<ulong, string> exchangeByCik,
        Dictionary<string, string> exchangeByTicker) {
        var mappings = new List<MappingRecord>();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.ValueKind == JsonValueKind.Object) {
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
                TryAddMapping(prop.Value, exchangeByCik, exchangeByTicker, mappings);
        } else if (doc.RootElement.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement element in doc.RootElement.EnumerateArray())
                TryAddMapping(element, exchangeByCik, exchangeByTicker, mappings);
        } else if (TryGetArrayProperty(doc.RootElement, "data", out JsonElement dataArray)) {
            foreach (JsonElement element in dataArray.EnumerateArray())
                TryAddMapping(element, exchangeByCik, exchangeByTicker, mappings);
        }
        return mappings;
    }

    private static void TryAddMapping(
        JsonElement element,
        Dictionary<ulong, string> exchangeByCik,
        Dictionary<string, string> exchangeByTicker,
        List<MappingRecord> mappings) {
        if (!TryGetUInt64(element, "cik", "cik_str", out ulong cik))
            return;
        if (!TryGetString(element, out string? ticker, "ticker"))
            return;
        if (string.IsNullOrWhiteSpace(ticker))
            return;
        ticker = NormalizeTicker(ticker);
        string? exchange = null;
        if (exchangeByCik.TryGetValue(cik, out string? byCik))
            exchange = byCik;
        else if (exchangeByTicker.TryGetValue(ticker, out string? byTicker))
            exchange = byTicker;
        exchange = NormalizeExchange(exchange);
        mappings.Add(new MappingRecord(cik, ticker, exchange));
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

    private static void LoadExchangeMappings(
        string path,
        Dictionary<ulong, string> exchangeByCik,
        Dictionary<string, string> exchangeByTicker) {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
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
        exchange = NormalizeExchange(exchange);
        if (string.IsNullOrWhiteSpace(exchange))
            return;

        if (TryGetUInt64(element, "cik", "cik_str", out ulong cik)) {
            if (!exchangeByCik.ContainsKey(cik))
                exchangeByCik[cik] = exchange;
        }

        if (TryGetString(element, out string? ticker, "ticker")) {
            ticker = NormalizeTicker(ticker);
            if (!string.IsNullOrWhiteSpace(ticker) && !exchangeByTicker.ContainsKey(ticker))
                exchangeByTicker[ticker] = exchange;
        }
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
            exchange = NormalizeExchange(exchange);
            if (string.IsNullOrWhiteSpace(exchange))
                continue;

            ulong cik = GetFieldUInt64(row, fieldIndexes, "cik", "cik_str");
            if (cik > 0 && !exchangeByCik.ContainsKey(cik))
                exchangeByCik[cik] = exchange;

            string? ticker = GetFieldString(row, fieldIndexes, "ticker");
            ticker = NormalizeTicker(ticker);
            if (!string.IsNullOrWhiteSpace(ticker) && !exchangeByTicker.ContainsKey(ticker))
                exchangeByTicker[ticker] = exchange;
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
                        ulong.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out result)) {
                        return result;
                    }
                }
            }
        }
        return 0;
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
            if (prop.ValueKind == JsonValueKind.String &&
                ulong.TryParse(prop.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out value)) {

                return true;
            }
        }
        value = 0;
        return false;
    }

    private static bool TryParseVolume(string value, out long volume) {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out volume))
            return true;
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalVolume)) {
            decimal rounded = decimal.Round(decimalVolume, 0, MidpointRounding.AwayFromZero);
            if (rounded >= long.MinValue && rounded <= long.MaxValue) {
                volume = (long)rounded;
                return true;
            }
        }
        volume = 0;
        return false;
    }

    private static string NormalizeTicker(string? ticker)
        => string.IsNullOrWhiteSpace(ticker) ? string.Empty : ticker.Trim().ToUpperInvariant();

    private static string? NormalizeExchange(string? exchange) {
        if (string.IsNullOrWhiteSpace(exchange))
            return null;
        return exchange.Trim().ToUpperInvariant();
    }

    private static string BuildImportKey(ulong cik, string ticker, string? exchange)
        => $"{cik}|{NormalizeTicker(ticker)}|{NormalizeExchange(exchange) ?? string.Empty}";

    private sealed record ImportCandidate(ulong Cik, string Ticker, string? Exchange, DateTime LastImportedUtc);

    private sealed record MappingRecord(ulong Cik, string Ticker, string? Exchange);

    private readonly record struct ParsedPriceRow(
        ulong Cik,
        string Ticker,
        string? Exchange,
        string StooqSymbol,
        DateOnly PriceDate,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long Volume);

    private readonly record struct ImportOutcome(Result Result, bool HitDailyLimit) {
        public static ImportOutcome Success => new(Result.Success, false);
        public static ImportOutcome Failure(Result result) => new(result, false);
        public static ImportOutcome DailyLimit(Result result) => new(result, true);
    }
}
