# 15. Financing-Total Net Cash Flow for Financials

## Status
Accepted (2026-11-23). Amends ADR 0013 (net cash flow) and ADR 0014 (scoring now reads the SIC).

## Context
Net cash flow (NCF) feeds value checks 4 (average NCF), 6 and 8 (estimated return CF), 14 (ROE CF) and moat check 1 (ROE CF). ADR 0013 defines it as the change in cash minus net debt, stock and preferred issuance, each built from tag chains. Every other financing flow counts as cash generated. For a bank that includes deposit growth, and for other lenders and insurers, FHLB borrowings, repos and similar funding. This was open item E5.

The fix considered first, NCF = Δcash − financing-activities total − dividends for everyone, was measured with financials mixed in and deferred until financials could be seen separately (ADR 0014). With the flag in place it was measured per segment on the 2026-11-22 data (Obsidian note *EDGAR Scoring Bug Fix Plan*, sections 4.6, 9.25, 9.26):
- **Cash identity:** the financing total is reported in 98.6% of the 10-K years that report a change in cash (89.6% of all window years). Where the operating and investing totals are reported too, Δcash = operating + investing + financing (+ FX, or + discontinued) holds in 95.0% of financial years and 84.9% of the others.
- **What the total adds beyond the chains is only partly identified.** Financials: net inflow in 60.5% of years with a residual; deposits are at least half of it in 30.4% of years, FHLB repayments or proceeds in 8.8%, and in 55.2% no tag we looked at is. Non-financials: 53.2% inflow, 46.8% outflow, and in 85.8% no tag we looked at is half of it (share-comp withholding 6.5%, lease principal 2.4%).
- **For everyone (V1):** 4,197 value scores changed, 3,509 of them non-financial; 15/15 kept 60 names but swapped 24 (financials out, energy in). For non-financials it lowered average NCF for 6,932 companies and raised it for 4,116; why is **not established**. It may catch debt or equity proceeds the chains miss, so it can't be dismissed as a mere redefinition.
- **For financials only (V2):** 688 value scores changed, all financial, and no non-financial score moved.

## Decision
- A company is financial when its SIC is 6000–6499 (`IndustryClassification.IsFinancial`, ADR 0014). `IndustryClassification.NetCashFlowBasisFor` maps it to a `NetCashFlowBasis`.
- **Financials (`FinancingTotal`):** a year's NCF = Δcash − (`NetCashProvidedByUsedInFinancingActivities`, else `…ContinuingOperations`, + dividends paid). The total already contains dividends as an outflow, so adding them back keeps NCF after dividends, as on the chain basis. A year without either total uses the chains.
- **Everyone else (`Chains`):** unchanged (ADR 0013).
- The basis flows through `ComputeYearNetCashFlow`, the ROE (CF) numerator (NCF + dividends, E6), `ComputeDerivedMetrics` and `ComputeMoatDerivedMetrics`. Formulas that use NCF (estimated return CF, max buy price) are unchanged.
- **Where the SIC comes from:** the batch paths (live and as-of value scores, moat scores) read every SIC once (`GetCompanySics`); the single-company pages read one (`GetCompanySic`). A failed SIC read fails the scoring call rather than silently scoring a bank on the chain basis.
- The value page's NCF tooltips state both formulas.

## Consequences
- **Measured on the 2026-11-22 load** against E6: value 688 changed, all financial; 13+ 289 → 229 (financials 119 → 59); 14+ 141 → 111; 15/15 60 → 43 (financials 29 → 12; out AIG AMAL BANF BSVN CFFI CG COF CPF DCBG FBP FCCO FFIN FHB LOB MCBS NRIM OPBK PEBK RDN SBSI SPFI UMBF; in FHN JXN OSBC VABK WABC). Flips on checks 4, 6, 8 and 14 only. Moat 296 changed, all financial (check 1 only); 11+ 129 → 128.
- **Backtest** (61 month-ends 2021-10-31..2026-10-31, before = E6, SPY +60.6%): 15/15 all +66.3% → +77.4% (70 → 41 names on average), financials only +47.4% → +104.0% (35 → 6 names); 14+ all +60.9% → +53.6%, financials only +70.7% → +79.5% (94 → 21 names). Excluding financials: unchanged. The financials-only 15/15 group is too small to read as evidence.
- **Two definitions of NCF.** The scores no longer treat everyone alike: a bank's NCF and a manufacturer's NCF are computed differently. Intended, and visible in the tooltip, but comparisons of average NCF across the two segments mix definitions.
- **Today's SIC for the past:** as-of snapshots classify by the current SIC (ADR 0014), so a reclassified company uses its current basis for every backtest date.
- **SIC is coarse:** a company filing under a non-financial code keeps the chains even if it takes deposits, and the reverse.
- **Reimports:** `--import-company-sic` must run before scores are computed; without it every company scores on the chain basis.

## Alternatives Considered
- **Keep the chains for everyone (V0):** bank deposit growth keeps counting as cash generated; 29 financials at 15/15 on E6.
- **Financing total for everyone (V1):** 4,197 value / 1,323 moat scores changed. Moves 3,509 non-financial scores for reasons that are not established (see Context). Not adopted; open until the non-financial residual is explained.
- **Add deposit and funding tags to the chains for everyone:** would need a curated list per industry (deposits, FHLB, repos, policyholder deposits, …) and still miss extension tags; the total needs no such list.
- **Exclude financials, or bank-specific checks:** still rejected (ADR 0014, "exclude nothing").
