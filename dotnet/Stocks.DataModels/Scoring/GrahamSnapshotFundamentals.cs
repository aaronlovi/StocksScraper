using System;

namespace Stocks.DataModels.Scoring;

/// <summary>
/// The price-independent score inputs of one company at one snapshot date. Two adjacent
/// snapshots with identical fundamentals can only differ because the price moved;
/// any difference means a new filing changed the inputs.
/// </summary>
public record GrahamSnapshotFundamentals(
    DateOnly AsOfDate,
    ulong CompanyId,
    int YearsOfData,
    decimal? BookValue,
    decimal? DebtToEquityRatio,
    decimal? AverageNetCashFlow,
    decimal? AverageOwnerEarnings,
    decimal? AdjustedRetainedEarnings,
    decimal? AverageRoeCF,
    decimal? AverageRoeOE,
    long? SharesOutstanding);
