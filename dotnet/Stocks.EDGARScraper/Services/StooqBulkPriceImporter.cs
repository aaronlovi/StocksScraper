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

public sealed class StooqBulkPriceImporter {
    private readonly IDbmService _dbm;
    private readonly ILogger<StooqBulkPriceImporter> _logger;

    public StooqBulkPriceImporter(IDbmService dbm, ILogger<StooqBulkPriceImporter> logger) {
        _dbm = dbm;
        _logger = logger;
    }

    public async Task<Result> ImportAsync(
        string rootDir,
        string mappingDir,
        int batchSize,
        CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(rootDir))
            return Result.Failure(ErrorCodes.ValidationError, "Root directory is required.");
        if (string.IsNullOrWhiteSpace(mappingDir))
            return Result.Failure(ErrorCodes.ValidationError, "Mapping directory is required.");
        if (!Directory.Exists(rootDir))
            return Result.Failure(ErrorCodes.NotFound, $"Root directory not found: {rootDir}");

        string mappingPath = Path.Combine(mappingDir, "company_tickers.json");
        if (!File.Exists(mappingPath))
            return Result.Failure(ErrorCodes.NotFound, $"Missing mapping file: {mappingPath}");

        string exchangeMappingPath = Path.Combine(mappingDir, "company_tickers_exchange.json");
        var exchangeByCik = new Dictionary<ulong, string>();
        var exchangeByTicker = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(exchangeMappingPath))
            LoadExchangeMappings(exchangeMappingPath, exchangeByCik, exchangeByTicker);

        Dictionary<string, MappingRecord> mappingsByTicker = LoadMappings(mappingPath, exchangeByCik, exchangeByTicker);
        if (mappingsByTicker.Count == 0)
            return Result.Failure(ErrorCodes.NotFound, "No ticker mappings found.");

        int effectiveBatchSize = batchSize > 0 ? batchSize : 500;
        int successCount = 0;
        int failureCount = 0;

        foreach (string filePath in EnumerateStooqFiles(rootDir)) {
            Result result = await ImportFileAsync(filePath, mappingsByTicker, effectiveBatchSize, ct);
            if (result.IsSuccess)
                successCount++;
            else
                failureCount++;
        }

        _logger.LogInformation("Bulk Stooq import completed. Success: {SuccessCount}, Failed: {FailureCount}",
            successCount, failureCount);
        return Result.Success;
    }

    private IEnumerable<string> EnumerateStooqFiles(string rootDir) {
        foreach (string filePath in Directory.EnumerateFiles(rootDir, "*.txt", SearchOption.AllDirectories)) {
            yield return filePath;
        }
    }

    private async Task<Result> ImportFileAsync(
        string filePath,
        Dictionary<string, MappingRecord> mappingsByTicker,
        int batchSize,
        CancellationToken ct) {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure(ErrorCodes.ParsingError, $"Invalid file name: {filePath}");

        string normalizedTicker = ParseTickerFromSymbol(fileName);
        if (string.IsNullOrWhiteSpace(normalizedTicker)) {
            _logger.LogWarning("Could not determine ticker from file: {File}", filePath);
            return Result.Failure(ErrorCodes.ParsingError, $"Missing ticker in file name: {filePath}");
        }

        if (!mappingsByTicker.TryGetValue(normalizedTicker, out MappingRecord? mapping)) {
            _logger.LogWarning("Ticker not found in mappings: {Ticker}", normalizedTicker);
            return Result.Failure(ErrorCodes.NotFound, $"Ticker not found in mappings: {normalizedTicker}");
        }
        MappingRecord resolvedMapping = mapping ?? new MappingRecord(0, null);

        int totalRows = 0;
        bool deletedExisting = false;
        async Task<Result> EnsureDeletedAsync() {
            if (deletedExisting)
                return Result.Success;
            Result deleteResult = await _dbm.DeletePricesForTicker(normalizedTicker, ct);
            if (deleteResult.IsFailure)
                return deleteResult;
            deletedExisting = true;
            return Result.Success;
        }
        var pending = new List<ParsedPriceRow>(batchSize);

        try {
            using var reader = new StreamReader(filePath);
            string? header = await reader.ReadLineAsync(ct);
            if (header is null)
                return Result.Failure(ErrorCodes.ParsingError, $"Missing header in {filePath}");

            if (!header.StartsWith("<TICKER>", StringComparison.OrdinalIgnoreCase)) {
                _logger.LogWarning("Unexpected header in {File}: {Header}", filePath, header);
                return Result.Failure(ErrorCodes.ParsingError, $"Unexpected header in {filePath}");
            }

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null) {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!TryParseLine(line, out ParsedPriceRow row)) {
                    _logger.LogWarning("Failed to parse price row for {Ticker}. Line: {Line}", normalizedTicker, line);
                    return Result.Failure(ErrorCodes.ParsingError, $"Failed to parse CSV row for {normalizedTicker}");
                }

                pending.Add(row);
                if (pending.Count >= batchSize) {
                    Result deleteResult = await EnsureDeletedAsync();
                    if (deleteResult.IsFailure)
                        return deleteResult;
                    Result batchResult = await InsertBatchAsync(resolvedMapping, normalizedTicker, pending, ct);
                    if (batchResult.IsFailure)
                        return batchResult;
                    totalRows += pending.Count;
                    pending.Clear();
                }
            }

            if (pending.Count > 0) {
                Result deleteResult = await EnsureDeletedAsync();
                if (deleteResult.IsFailure)
                    return deleteResult;
                Result batchResult = await InsertBatchAsync(resolvedMapping, normalizedTicker, pending, ct);
                if (batchResult.IsFailure)
                    return batchResult;
                totalRows += pending.Count;
                pending.Clear();
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to import file {File}", filePath);
            return Result.Failure(ErrorCodes.GenericError, $"Failed to import {filePath}: {ex.Message}");
        }

        if (totalRows == 0) {
            _logger.LogWarning("No price rows found in {File}", filePath);
            return Result.Failure(ErrorCodes.NotFound, $"No price rows found for {normalizedTicker}");
        }

        DateTime nowUtc = DateTime.UtcNow;
        var importStatus = new PriceImportStatus(resolvedMapping.Cik, normalizedTicker, resolvedMapping.Exchange, nowUtc);
        Result importResult = await _dbm.UpsertPriceImport(importStatus, ct);
        if (importResult.IsFailure)
            return importResult;

        var downloadStatus = new PriceDownloadStatus(resolvedMapping.Cik, normalizedTicker, resolvedMapping.Exchange, nowUtc);
        Result downloadResult = await _dbm.UpsertPriceDownload(downloadStatus, ct);
        if (downloadResult.IsFailure)
            return downloadResult;

        return Result.Success;
    }

    private async Task<Result> InsertBatchAsync(
        MappingRecord mapping,
        string normalizedTicker,
        List<ParsedPriceRow> batch,
        CancellationToken ct) {
        if (batch.Count == 0)
            return Result.Success;

        ulong firstId = await _dbm.GetIdRange64((uint)batch.Count, ct);
        ulong currentId = firstId;
        var prices = new List<PriceRow>(batch.Count);

        foreach (ParsedPriceRow row in batch) {
            prices.Add(new PriceRow(
                currentId,
                mapping.Cik,
                normalizedTicker,
                mapping.Exchange,
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

    private static bool TryParseLine(string line, out ParsedPriceRow row) {
        row = default;
        string[] parts = line.Split(',');
        if (parts.Length < 9)
            return false;

        string symbol = parts[0].Trim();
        string period = parts[1].Trim();
        string dateValue = parts[2].Trim();
        string openValue = parts[4].Trim();
        string highValue = parts[5].Trim();
        string lowValue = parts[6].Trim();
        string closeValue = parts[7].Trim();
        string volumeValue = parts[8].Trim();

        if (!string.Equals(period, "D", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!DateOnly.TryParseExact(dateValue, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly priceDate))
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
            NormalizeSymbol(symbol),
            priceDate,
            open,
            high,
            low,
            close,
            volume);
        return true;
    }

    private static bool TryParseVolume(string value, out long volume) {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out volume))
            return true;
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalVolume)) {
            decimal rounded = decimal.Round(decimalVolume, 0, MidpointRounding.AwayFromZero);
            if (rounded is >= long.MinValue and <= long.MaxValue) {
                volume = (long)rounded;
                return true;
            }
        }
        volume = 0;
        return false;
    }

    private static string ParseTickerFromSymbol(string symbol) {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;
        string trimmed = symbol.Trim();
        int dotIndex = trimmed.IndexOf('.');
        if (dotIndex > 0)
            trimmed = trimmed[..dotIndex];
        return trimmed.ToUpperInvariant();
    }

    private static string NormalizeSymbol(string symbol) {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;
        return symbol.Trim().ToLowerInvariant();
    }

    private static Dictionary<string, MappingRecord> LoadMappings(
        string path,
        Dictionary<ulong, string> exchangeByCik,
        Dictionary<string, string> exchangeByTicker) {
        var mappings = new Dictionary<string, MappingRecord>(StringComparer.OrdinalIgnoreCase);
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
        Dictionary<string, MappingRecord> mappings) {
        if (!TryGetUInt64(element, "cik", "cik_str", out ulong cik))
            return;
        if (!TryGetString(element, out string? ticker, "ticker"))
            return;
        if (string.IsNullOrWhiteSpace(ticker))
            return;
        ticker = ticker.Trim().ToUpperInvariant();

        string? exchange = null;
        if (exchangeByCik.TryGetValue(cik, out string? byCik))
            exchange = byCik;
        else if (exchangeByTicker.TryGetValue(ticker, out string? byTicker))
            exchange = byTicker;

        if (!string.IsNullOrWhiteSpace(exchange))
            exchange = exchange.Trim().ToUpperInvariant();

        if (!mappings.ContainsKey(ticker))
            mappings.Add(ticker, new MappingRecord(cik, exchange));
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
        if (string.IsNullOrWhiteSpace(exchange))
            return;
        exchange = exchange.Trim().ToUpperInvariant();

        if (TryGetUInt64(element, "cik", "cik_str", out ulong cik)) {
            if (!exchangeByCik.ContainsKey(cik))
                exchangeByCik[cik] = exchange;
        }

        if (TryGetString(element, out string? ticker, "ticker")) {
            if (!string.IsNullOrWhiteSpace(ticker)) {
                ticker = ticker.Trim().ToUpperInvariant();
                if (!exchangeByTicker.ContainsKey(ticker))
                    exchangeByTicker[ticker] = exchange;
            }
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
            if (string.IsNullOrWhiteSpace(exchange))
                continue;
            exchange = exchange.Trim().ToUpperInvariant();

            ulong cik = GetFieldUInt64(row, fieldIndexes, "cik", "cik_str");
            if (cik > 0 && !exchangeByCik.ContainsKey(cik))
                exchangeByCik[cik] = exchange;

            string? ticker = GetFieldString(row, fieldIndexes, "ticker");
            if (!string.IsNullOrWhiteSpace(ticker)) {
                ticker = ticker.Trim().ToUpperInvariant();
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
                        ulong.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out result)) {
                        return result;
                    }
                }
            }
        }
        return 0;
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
            if (prop.ValueKind == JsonValueKind.String &&
                ulong.TryParse(prop.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out value)) {
                return true;
            }
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

    private readonly record struct ParsedPriceRow(
        string StooqSymbol,
        DateOnly PriceDate,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long Volume);

    private sealed record MappingRecord(ulong Cik, string? Exchange);
}
