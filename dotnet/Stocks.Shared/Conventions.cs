using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stocks.Shared;

public static class Conventions {
    public static readonly JsonSerializerOptions DefaultOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Use camelCase for property names
        WriteIndented = false,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}
