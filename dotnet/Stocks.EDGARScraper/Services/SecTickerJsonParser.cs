using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace EDGARScraper.Services;

public static class SecTickerJsonParser {
    public static List<SecTickerMapping> LoadBaseMappings(
        string path,
        Dictionary<ulong, string> exchangeByCik,
        Dictionary<string, string> exchangeByTicker) {
        var results = new List<SecTickerMapping>();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
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

    public static void LoadExchangeMappings(
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

        var fieldIndexes = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
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
                        ulong.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out result)) {
                        return result;
                    }
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
