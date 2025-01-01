namespace Stocks.DataModels.Enums;

public static class EnumExtensions
{
    public static FilingType ToFilingType(this string coreType) =>
        coreType switch
        {
            "10-K" => FilingType.TenK,
            "10-Q" => FilingType.TenQ,
            "8-K" => FilingType.EightK,
            _ => FilingType.Invalid
        };

    public static FilingCategory ToFilingCategory(this string coreType) =>
        coreType switch
        {
            "10-K" => FilingCategory.Annual,
            "10-Q" => FilingCategory.Quarterly,
            _ => FilingCategory.Other
        };
}
