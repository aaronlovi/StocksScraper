using System;
using Stocks.Shared;

namespace Stocks.DataModels;

public record DataPoint(
    ulong DataPointId,
    ulong CompanyId,
    string FactName,
    string FilingReference,
    DatePair DatePair,
    decimal Value,
    DataPointUnit Units,
    DateOnly FiledDate,
    ulong SubmissionId,
    long TaxonomyConceptId // NEW: taxonomy_concept_id
) {
    public DateTime FiledTimeUtc => FiledDate.AsUtcTime();
    public DateTime StartTimeUtc => DatePair.StartDate.AsUtcTime();
    public DateTime EndTimeUtc => DatePair.EndDate.AsUtcTime();
}
