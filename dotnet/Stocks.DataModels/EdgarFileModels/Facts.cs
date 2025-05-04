using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Stocks.DataModels.EdgarFileModels;

public record Facts {
    [JsonPropertyName("us-gaap")] public Dictionary<string, Fact> UsGaap { get; init; } = [];
}
