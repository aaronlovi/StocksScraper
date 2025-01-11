namespace Stocks.DataModels.Enums;

public static class EnumExtensions
{
    public static FilingType ToFilingType(this string coreType) =>
        coreType switch
        {
            "10-K" => FilingType.TenK,
            "10-Q" => FilingType.TenQ,
            "8-K" => FilingType.EightK,
            "8-K/A" => FilingType.EightK_A,
            "10-K/A" => FilingType.TenK_A,
            "10-Q/A" => FilingType.TenQ_A,
            "10-KT/A" => FilingType.TenKT_A,
            "10-QT/A" => FilingType.TenQT_A,
            "10-KT" => FilingType.TenKT,
            "10-QT" => FilingType.TenQT,
            "40-F" => FilingType.FortyF,
            "40-F/A" => FilingType.FortyF_A,
            "20-F" => FilingType.TwentyF,
            "20-F/A" => FilingType.TwentyF_A,
            "6-K" => FilingType.SixK,
            "6-K/A" => FilingType.SixK_A,
            _ => FilingType.Invalid
        };

    public static FilingCategory ToFilingCategory(this string coreType) =>
        coreType switch
        {
            "10-K" => FilingCategory.Annual,
            "10-K/A" => FilingCategory.Annual,
            "20-F" => FilingCategory.Annual,
            "20-F/A" => FilingCategory.Annual,

            "10-Q" => FilingCategory.Quarterly,
            "10-Q/A" => FilingCategory.Quarterly,

            "8-K" => FilingCategory.Other,
            "8-K/A" => FilingCategory.Other,
            "10-KT/A" => FilingCategory.Other,
            "10-QT/A" => FilingCategory.Other,
            "10-KT" => FilingCategory.Other,
            "10-QT" => FilingCategory.Other,
            "40-F" => FilingCategory.Other,
            "40-F/A" => FilingCategory.Other,
            "6-K" => FilingCategory.Other,
            "6-K/A" => FilingCategory.Other,
            _ => FilingCategory.Other
        };
}
