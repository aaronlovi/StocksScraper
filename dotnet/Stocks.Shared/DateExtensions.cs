using System;

namespace Stocks.Shared;

public static class DateExtensions {
    public static DateTime AsUtcTime(this DateOnly d) => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
