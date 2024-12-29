using System;
using System.Text.Json.Serialization;
using Stocks.DataModels;

namespace DataModels.XbrlFileModels;

public record Unit
{
    [JsonPropertyName("start")] public DateOnly? StartDate { get; init; }
    [JsonPropertyName("end")] public DateOnly EndDate { get; init; }
    [JsonPropertyName("val")] public decimal Value { get; init; }
    [JsonPropertyName("filed")] public DateOnly FiledDate { get; init; }

    [JsonIgnore]
    public DatePair DatePair => new(StartDate ?? EndDate, EndDate);
}
