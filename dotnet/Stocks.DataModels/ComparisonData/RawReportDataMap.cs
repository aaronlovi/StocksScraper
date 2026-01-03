using System;
using System.Collections.Generic;
using System.Text.Json;
using Stocks.Shared;

namespace Stocks.DataModels.ComparisonData;

public class RawReportDataMap : NormalizedStringKeysHashMap<decimal> {
    public DateOnly? ReportDate { get; init; }
    public bool IsValid { get; set; } = true;

    public bool IsEqual(RawReportDataMap other) {
        foreach (string otherKey in other.Keys) {
            if (!HasValue(otherKey))
                return false;
            if (other[otherKey] != this[otherKey])
                return false;
        }

        foreach (string key in Keys) {
            if (!other.HasValue(key))
                return false;
            if (other[key] != this[key])
                return false;
        }

        return true;
    }
}
