using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DataModels.XbrlFileModels;

public record Fact
{
    [JsonPropertyName("units")] public Dictionary<string, List<Unit>> Units { get; init; } = [];
}
