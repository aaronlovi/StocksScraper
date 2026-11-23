namespace Stocks.DataModels.Scoring;

/// <summary>
/// How a year's net cash flow is derived (ADR 0013, E5). <see cref="Chains"/>: the change in
/// cash minus net debt, stock and preferred issuance. <see cref="FinancingTotal"/> (financials):
/// the change in cash minus the financing-activities total and dividends, so deposit and other
/// funding flows are not counted as cash generated; years without the total use the chains.
/// </summary>
public enum NetCashFlowBasis {
    Chains,
    FinancingTotal,
}
