using System.Collections.Generic;

namespace Stocks.DataModels.Enums;

public static class FilingTypeExtensions {
    private static readonly Dictionary<FilingType, string> DisplayNames = new() {
        { FilingType.TenK, "10-K" },
        { FilingType.TenQ, "10-Q" },
        { FilingType.EightK, "8-K" },
        { FilingType.EightK_A, "8-K/A" },
        { FilingType.TenK_A, "10-K/A" },
        { FilingType.TenQ_A, "10-Q/A" },
        { FilingType.TenKT_A, "10-KT/A" },
        { FilingType.TenQT_A, "10-QT/A" },
        { FilingType.TenKT, "10-KT" },
        { FilingType.TenQT, "10-QT" },
        { FilingType.FortyF, "40-F" },
        { FilingType.FortyF_A, "40-F/A" },
        { FilingType.TwentyF, "20-F" },
        { FilingType.TwentyF_A, "20-F/A" },
        { FilingType.SixK, "6-K" },
        { FilingType.SixK_A, "6-K/A" },
    };

    public static string ToDisplayName(this FilingType filingType) {
        if (DisplayNames.TryGetValue(filingType, out string? name))
            return name;
        return filingType.ToString();
    }
}
