using System;

namespace Stocks.DataModels.Scoring;

/// <param name="BalanceTypeId">Taxonomy balance type: 1=credit, 2=debit, 3=not applicable.</param>
public record ScoringConceptValue(string ConceptName, decimal Value, DateOnly ReportDate, int BalanceTypeId);
