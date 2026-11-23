using System.Text.Json.Serialization;

namespace Stocks.DataModels.EdgarFileModels;

/// <summary>
/// Company fields at the top of a <c>CIK##########.json</c> file in the EDGAR submissions
/// archive. Paginated <c>-submissions-NNN.json</c> files do not have them.
/// </summary>
public record SubmissionsCompanyHeader {
    [JsonPropertyName("cik")] public ulong Cik { get; init; }
    [JsonPropertyName("sic")] public string? Sic { get; init; }
    [JsonPropertyName("sicDescription")] public string? SicDescription { get; init; }
}
