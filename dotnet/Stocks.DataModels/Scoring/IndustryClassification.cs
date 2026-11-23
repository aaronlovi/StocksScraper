namespace Stocks.DataModels.Scoring;

/// <summary>
/// Classifies companies by SEC SIC code. Financial = SIC 6000-6499: banks and savings
/// institutions, non-bank lenders, brokers and dealers, insurance carriers and agents
/// (docs/sic-financials-flag/requirements.md, section 4.2). REITs (6798), real estate
/// (65xx) and blank checks (6770) are not financials. This is the only copy of the range:
/// SQL statements bind <see cref="FinancialSicMin"/> and <see cref="FinancialSicMax"/>.
/// </summary>
public static class IndustryClassification {
    public const int FinancialSicMin = 6000;
    public const int FinancialSicMax = 6499;

    public static bool IsFinancial(int? sicCode) =>
        sicCode is int sic && sic >= FinancialSicMin && sic <= FinancialSicMax;

    /// <summary>Net cash flow basis for a company: the financing total for financials, else the chains.</summary>
    public static NetCashFlowBasis NetCashFlowBasisFor(int? sicCode) =>
        IsFinancial(sicCode) ? NetCashFlowBasis.FinancingTotal : NetCashFlowBasis.Chains;

    /// <summary>Parses a SIC string of 1-4 digits (surrounding spaces allowed); anything else is null.</summary>
    public static int? ParseSic(string? raw) {
        if (raw is null)
            return null;
        string trimmed = raw.Trim();
        if (trimmed.Length == 0 || trimmed.Length > 4)
            return null;
        int value = 0;
        foreach (char ch in trimmed) {
            if (ch < '0' || ch > '9')
                return null;
            value = (value * 10) + (ch - '0');
        }
        return value;
    }
}
