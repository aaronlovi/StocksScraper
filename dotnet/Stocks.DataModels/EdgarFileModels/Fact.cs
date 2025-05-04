using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Stocks.DataModels.EdgarFileModels;

public record Fact
{
    [JsonPropertyName("units")] public Dictionary<string, List<Unit>> Units { get; init; } = [];
}
