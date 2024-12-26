using System;

namespace EDGARScraper;

internal record DatePair(DateOnly Start, DateOnly End)
{
    internal static readonly DatePair Empty = new(DateOnly.MinValue, DateOnly.MinValue);

    internal bool IsEmpty => Start == DateOnly.MinValue && End == DateOnly.MinValue;
    internal bool IsInstant => Start == End;
    internal bool IsPeriod => Start != End;
    internal DateTime StartTimeUtc => Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    internal DateTime EndTimeUtc => End.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
