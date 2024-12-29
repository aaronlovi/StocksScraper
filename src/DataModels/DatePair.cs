using System;
using Utilities;

namespace Stocks.DataModels;

public record DatePair(DateOnly StartDate, DateOnly EndDate)
{
    public static readonly DatePair Empty = new(DateOnly.MinValue, DateOnly.MinValue);

    public bool IsEmpty => StartDate == DateOnly.MinValue && EndDate == DateOnly.MinValue;
    public bool IsInstant => StartDate == EndDate;
    public bool IsPeriod => StartDate != EndDate;
    public DateTime StartTimeUtc => StartDate.AsUtcTime();
    public DateTime EndTimeUtc => EndDate.AsUtcTime();
}
