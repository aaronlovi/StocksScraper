using System.Text.Json.Serialization;

namespace Stocks.DataModels.EdgarFileModels;

public record XbrlJson {
    [JsonPropertyName("cik")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public ulong Cik { get; init; }

    [JsonPropertyName("entityName")] public string EntityName { get; init; } = "";
    [JsonPropertyName("facts")] public Facts Facts { get; init; } = new();

    public string CikString => Cik.ToString("D10");
}
