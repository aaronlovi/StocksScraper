using System;

namespace Stocks.DataModels.Scoring;

public record BatchScoringConceptValue(
    ulong CompanyId,
    string ConceptName,
    decimal Value,
    DateOnly ReportDate,
    int BalanceTypeId);
