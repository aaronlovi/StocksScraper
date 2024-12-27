using System;
using Utilities;

namespace DataModels;

public record DataPoint(DatePair DatePair, decimal Value, DataPointUnit Units, DateOnly FiledDate)
{
    public DateTime FiledTimeUtc => FiledDate.AsUtcTime();
}
