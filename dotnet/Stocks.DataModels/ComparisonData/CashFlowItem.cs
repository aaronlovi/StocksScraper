namespace Stocks.DataModels.ComparisonData;

internal class CashFlowItem {
    internal CashFlowItem() { }

    // Expected properties
    internal decimal? NetIncomeFromContinuingOperations { get; set; }
    internal decimal GrossCashFlow { get; set; }
    internal decimal NetIssuanceOfDebt { get; set; }
    internal decimal NetIssuanceOfStock { get; set; }
    internal decimal NetIssuanceOfPreferredStock { get; set; }
    internal decimal ChangeInWorkingCapital { get; set; }
    internal decimal Depreciation { get; set; }
    internal decimal Depletion { get; set; }
    internal decimal Amortization { get; set; }
    internal decimal DeferredTax { get; set; }
    internal decimal OtherNonCashItems { get; set; }
    internal decimal NetPPEPurchaseAndSale { get; set; }

    // Derived properties
    internal decimal NetCashFlow => GrossCashFlow - (NetIssuanceOfDebt + NetIssuanceOfStock + NetIssuanceOfPreferredStock);
    internal decimal CapEx => NetPPEPurchaseAndSale + Depreciation;
    internal decimal OwnerEarnings => (NetIncomeFromContinuingOperations ?? 0)
        + Depreciation
        + Depletion
        + Amortization
        + DeferredTax
        + OtherNonCashItems
        - CapEx
        + ChangeInWorkingCapital;
}
