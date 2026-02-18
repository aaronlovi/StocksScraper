using System;

namespace Stocks.DataModels.Scoring;

public record ScoringConceptValue(string ConceptName, decimal Value, DateOnly ReportDate);
