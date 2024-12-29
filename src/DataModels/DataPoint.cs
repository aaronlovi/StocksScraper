using System;
using Utilities;

namespace Stocks.DataModels;

public record DataPoint(
    ulong DataPointId,
    ulong CompanyId,
    string FactName,
    DatePair DatePair,
    decimal Value,
    DataPointUnit Units,
    DateOnly FiledDate)
{
    public DateTime FiledTimeUtc => FiledDate.AsUtcTime();
    public DateTime StartTimeUtc => DatePair.StartDate.AsUtcTime();
    public DateTime EndTimeUtc => DatePair.EndDate.AsUtcTime();
}
