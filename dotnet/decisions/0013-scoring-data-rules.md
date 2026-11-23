# 13. Scoring Data Rules

## Status
Accepted (2026-11-22; amended the same day for E10 and E11, and on 2026-11-23 for E6)

## Context
The value score (`ScoringService`, 15 checks) and the moat score (`MoatScoringService`, 13 checks) were ported from tsx-aggregator before tsx fixed a series of data-handling defects. A review of the 2026-08-23 load found that the ports still carried those defects and that EDGAR's XBRL tags added some of their own:

- ratios over a negative denominator passed checks (D/E, P/B, D/B, ROE, capex ratio);
- aggregate tags were used with the wrong sign, and "other, net" tags replaced the components they sit beside;
- financing and capex chains took the first tag found, though co-reported tags are often separate lines;
- a missing capex tag counted as zero;
- owner earnings never added back depreciation;
- dividends were subtracted twice in the estimated returns and the max buy price;
- a sparse latest 10-Q hid 10-K balances (CAT: $30.7B of debt read as 0);
- debt-free companies got n/a on interest coverage;
- facts tagged in two currencies were picked by query plan, not by rule;
- the adjusted retained earnings counted the oldest year's flows twice.

These change what the scores mean, so the rules are recorded here. The full analysis, measurements and per-fix specs are in the Obsidian note *EDGAR Scoring Bug Fix Plan* (Stocks Research / Edgar Scraper Notes).

## Decision

### Three-state checks
Every check is Pass, Fail or NotAvailable; the score counts Pass only, and `computable_checks` counts Pass + Fail. A missing input is `null` end to end, never a sentinel. Fail and NotAvailable score the same; they differ in `computable_checks` and in the display.

### Sign rule (non-positive denominators)
A ratio over a denominator ≤ 0 **fails** its check. The rule lives in the check, not the division, so the displayed ratio stays honest (a negative D/E still shows).
- Value checks 1 (D/E, equity ≤ 0), 3 (P/B, book value ≤ 0 with a market cap) and 10 (D/B, book value ≤ 0); moat check 9 (D/E).
- Moat check 7 (capex ratio) fails when average owner earnings ≤ 0.
- ROE (value 14/15, moat 1/2): fails when the sum of average equity is ≤ 0 or any year's average equity is ≤ 0 (see "ROE"). CF and OE carry separate flags because their year sets differ. The displayed ratio is unchanged.

### Missing versus zero, per field
| Field | Missing means |
|---|---|
| Debt (D/E, D/B, moat D/E) | long-term chain + current portion + short-term borrowings; each missing tag is 0 (see "Debt for the debt ratios") |
| Debt-free (interest coverage) | no long-term or other debt concept (current, short-term, notes, credit lines, combined) is non-zero |
| Goodwill, intangibles | 0 |
| Dividends | 0 when the year has a cash-flow statement |
| Capex | **unknown** when the year reports D&A: the year leaves the owner-earnings average, ROE (OE), the moat positive-OE count and the capex ratio. With neither capex nor D&A, capex is 0 |
| Balance sheet in a newer 10-Q | filled from the latest 10-K (below) |

### Latest-10-K fallback for balance-sheet fields
`GroupAndPartitionData` fills gaps in the most recent snapshot (usually a 10-Q) from the latest 10-K, group by group, never mixing a group across dates:
- the equity group (assets, liabilities, L&SE, equity and NCI concepts) is replaced whole when equity can't be resolved from the 10-Q;
- long-term debt (`DebtChain`) and the other debt concepts are two subgroups, each copied when none of it is present;
- goodwill, intangibles, retained earnings and current assets/liabilities are filled individually.

Shares are **not** filled: a share count up to a year old gave 152 companies, mostly SPACs, a first and stale market cap. Duration concepts are never filled. The as-of path (ADR 0012) shares the code.

### Taxonomy-weight signs
Signs follow the us-gaap calculation weights in the indirect cash-flow statement. An IncreaseDecrease concept's balance type is its cash-flow direction, not its balance-sheet side: credit (asset increase, liability decrease) reduces operating cash and is negated; debit adds.
- `IncreaseDecreaseInOperatingCapital` and `OtherNoncashIncomeExpense` (both credit, weight −1) are negated.
- `IncreaseDecreaseInOtherOperatingCapitalNet` and `IncreaseDecreaseInOtherAccountsPayableAndAccruedLiabilities` are "other" lines: components beside their siblings, not alternative totals.
- A test checks every IncreaseDecrease concept the code reads against the DB balance types. Filers who used the opposite sign (about 1 in 4 reconciling filings) stay wrong.

### Chain classification and summing
A chain is either alternatives (first found) or lines to combine:
- **Alternatives:** equity, dividends, buybacks, D&A totals, capex. The capex chain is PP&E, productive assets, oil and gas P&E, other productive assets, other PP&E, capital improvements. Real-estate development is investment, not capex, and is left out.
- **Nested, with a parent guard:** debt proceeds and repayments follow the taxonomy tree (senior and convertible under long-term, long-term under the all-debt total). A parent at least the sum of its present children (within 0.1%) contains them; a smaller parent is a separate line, so both count. Equal sibling values are one line tagged twice.
- **Siblings, summed:** stock issuance and option exercises.
- **Short-term debt:** net short-term debt, else lines of credit (net, or gross proceeds − repayments), plus net commercial paper. Notes-payable repayments count unless the net short-term figure (their parent) is present.

### Debt for the debt ratios
Value D/E and D/B and moat D/E use the first long-term chain concept found (`LongTermDebtAndCapitalLeaseObligations`, `LongTermDebt`, `LongTermDebtNoncurrent`), plus `LongTermDebtCurrent`, plus `ShortTermBorrowings` (E4, added 2026-11-22). Per the taxonomy, `LongTermDebt` includes current maturities and the other two are noncurrent. So when `LongTermDebt` is the figure used, the current portion is added only if `LongTermDebt` does not cover noncurrent + current (within 0.1%); with no noncurrent tag to compare against, `LongTermDebt` is taken as the total.

### Annual year keys
Each distinct 10-K report date gets its own year key (E7, added 2026-11-22). The newest date keys to its calendar year; each older date to min(its calendar year, the next newer key − 1). Two annual reports in one calendar year (a fiscal-year change, or 52/53-week years ending in early January) are both kept, and years of data counts both. The DEI fiscal year (`DocumentFiscalYearFocus`) would be the better key, but it is not in the database: companyfacts carries only numeric dei facts.

### Stored check results
`company_scores` and `graham_score_snapshots` store the 15 check results as `check_results` (P/F/N in check order; V021, added 2026-11-22). The Portfolio Advisor reads pass/fail from them and re-derives from the metric columns only for rows written before V021.

### Duplicate facts in several units
Some filers tag a line twice with the same concept, period and filing, in USD and in a second currency (CAD, CNY). Both scoring statements, `GetAllScoringDataPointsStmt` (batch and as-of) and `GetScoringDataPointsStmt` (one company), pick one fact per submission and concept in this order (E10, added 2026-11-22):
1. latest end date;
2. earliest start date;
3. a preferred unit: `usd` for amounts, `shares` for share counts;
4. lowest `data_point_id`.

So the pick no longer depends on the query plan. A filer that reports only in a foreign currency keeps its foreign values, now picked deterministically; converting them is out of scope. `DbmInMemoryData` uses the same order.

### Adjusted retained earnings window
Adjusted RE = current RE + dividends − net stock issuance − net preferred issuance (checks 11 and 13). The flows are summed only over annual years **after the oldest**: oldest < year ≤ current (E11, added 2026-11-22). The oldest year's flows are already inside the oldest RE it is compared against. A company with one annual year gets no add-backs, so check 13 passes only if RE actually grew.

### Interest coverage for debt-free companies
Moat check 10: with a coverage figure, the > 5x threshold applies. Without one, a debt-free company passes when its latest operating income is > 0 and fails when it is ≤ 0. Otherwise (debt but no interest tag) it is NotAvailable.

### Owner earnings
Owner earnings = net income + total D&A + deferred tax + other non-cash − capex + working-capital change.
- Total D&A is `DepreciationDepletionAndAmortization` outright; else `DepreciationAndAmortization` + depletion; else depreciation + depletion + amortization.
- Capex is gross, so depreciation does not cancel. The "depreciation cancels out" formula in `.prompts/company-scoring-system-plan/plan.md` (archived) predates this ADR.
- One shared per-year function serves both scores.

### Dividends in returns
Net cash flow starts from the change in cash, so it is already after dividends paid; owner earnings are before dividends.
- Est. return (CF) = (avg NCF + dividends) ÷ market cap.
- Est. return (OE) = avg OE ÷ market cap, on both scores.
- Max buy price = min(3 × book, 20 × (avg NCF + dividends), 20 × avg OE) ÷ shares.

### ROE (E6)
ROE = 100 × Σ numerator ÷ Σ average equity, over the annual years where both the numerator and the year's own equity resolve. It replaced the mean of yearly `numerator ÷ year-end equity`, where a small-equity year could dominate and year-end equity did not match the year's flow.
- Average equity = (opening + closing) ÷ 2. Opening is the equity of the previous annual year key (E7 keys); without one, the closing equity alone. A year outside the sums can still open the next.
- Numerators: CF = net cash flow + dividends paid (NCF is after dividends, which left payers behind buyback companies); OE = owner earnings (unchanged).
- Sign rule: the check fails when the sum, or any entering year's average equity, is ≤ 0. The ratio is still shown (null only when the sum is 0).
- The moat trend chart shows each year's numerator over that year's average equity.

## Consequences
- Measured on the 2026-08-23 load with the read-only `--snapshot-scores` command and `scripts/score_snapshot_diff.py`, baseline → all fixes:
  - **Value:** 7,750 of 14,018 scores changed; 13+ 171 → 233; 14+ 53 → 107; 15/15 16 → 30.
  - **Moat:** 11,743 changed; 11+ 64 → 102; 13/13 4 → 3.
  - Per-fix footprints are in the plan's progress log.
- **Who moves:**
  - Banks and dividend payers move up (E2).
  - Insurers, banks without capex tags, and real-estate companies lose owner-earnings checks to n/a (E3).
  - Negative-equity buyback compounders fail D/E and ROE (P1).
  - Profitable debt-free companies pass interest coverage (P4).
- **E10 and E11, measured on the same load after all the fixes above:**
  - E10: value 5 and moat 6 scores changed, no group changes. Most multi-currency filers already got USD by chance.
  - E11: value 525 changed both ways (checks 11 and 13 only); 13+ 233 → 230, 14+ 103 → 102, 15/15 30 → 29. Moat unchanged.
- **E6, measured on the 2026-11-22 load** (against the SIC baseline, split with `--split financials`): value 1,070 changed, mostly up (checks 14 and 15 only); 13+ 259 → 289; 15/15 32 → 60, split evenly (financials 14 → 29, others 18 → 31). Moat 831 changed (checks 1 and 2 only); 13/13 4 → 13, all non-financial. Backtest of the 61 month-ends (2021-10-31..2026-10-31, monthly, SPY +60.6%): 15/15 all +68.6% → +66.3% (45 → 70 names on average), excluding financials +82.7% → +76.9%, financials only +50.7% → +47.4%; 14+ all +65.0% → +60.9%. E6 is adopted as a correctness fix, not for returns. Rebuilt on 2026-11-23.
- **Stale until rebuilt:** `company_scores`, `company_moat_scores` and the 61 `graham_score_snapshots` dates keep the old scores until rebuilt (`--compute-all-scores`, `--compute-all-moat-scores`, then `--compute-score-snapshots`, about 2 hours). Backtest and Portfolio Advisor results change after the rebuild. Done on 2026-11-22 with the 2026-11-22 data reimport: every live score and all 61 snapshot dates carry every rule here, including E10 and E11.
- **Portfolio Advisor reasons:** until the rebuild fills `check_results`, `PortfolioAdvisorService` falls back to rebuilding checks from the metric columns, which lack equity and the ROE flags, so its "why it flipped" text can't reflect the sign rule for checks 1, 14 and 15. After the rebuild it reads the stored results (display only; scores are unaffected).
- **Concept-list side effect (fixed by E10):** adding concepts (P3, P4, E3, E4) made a handful of companies flip unrelated checks. Checked 2026-11-22 on the four P4 strays: their eligible dates did not move. The cause was facts tagged in two currencies with the same concept, period and filing, with the tie broken by the query plan, so a plan change could switch the currency used. Since E10 the pick is fixed (see "Duplicate facts in several units"), and adding a concept no longer moves unrelated values.

## Alternatives Considered
Footprints are value / moat scores changed, measured against the baseline in a scratch replay unless noted.
- **NotAvailable instead of Fail for non-positive denominators:** identical scores; rejected to match tsx and to count the check as computable.
- **ROE: exclude negative-equity years** (3,140 / 3,095): lifts buyback compounders (ADSK FICO FTNT IDXX ORLY) on their few positive-equity years.
- **ROE: such a year counts as 0%** (3,153 / 3,115).
- **ROE: Σ numerator ÷ Σ year-end equity** (3,874, scratch replay): the first E6 sketch; adopted instead with average equity and the dividend add-back.
- **ROE: keep the mean of yearly ratios** (status quo before E6): a small-equity year dominates the mean, and dividend payers look worse than buyback companies on ROE (CF).
- **P2: flip the aggregates but keep "other, net" as a total** (284 / 306): still drops the components in 4,201 company-years.
- **P3: plain summing** (220 value) or **NCF = Δcash − financing total − dividends** (4,409 / 2,153): the latter also removes bank deposit flows (E5) but changes what NCF means. Deferred with E5.
- **P4: debt-free on long-term debt alone** (603 moat): passes companies whose debt is current or short-term.
- **E3: two capex tags only** (measured: 1,184 / 2,485 vs 999 / 2,345): turns known capex at E&Ps and others into "unknown".
- **E9 with a shares fill** (measured: 418 / 259 vs 268 / 109): stale SPAC market caps.
- **E4 without the current-portion guard** (310 / 167 vs adopted 245 / 119): double counts the current portion inside `LongTermDebt`.
- **E7 with a single shift** (51 / 57, same as adopted on this data): the earlier of two dates moves back one year, but a run of early-January year ends could collide.
- **E10: drop non-USD monetary facts** (not measured): also ends the random pick, but a company that reports only in a foreign currency would lose every monetary value.
- **E10: keep the plan-dependent pick** (status quo): scores could move whenever the concept list or the query plan changed.
- **E11: keep all years in the window** (status quo, the tsx P8 defect): counts the oldest year's dividends and issuance twice; tsx measured 29 of 654 scores moving.
- **E5** (financing-total NCF): measured separately (plan section 4.6) and deferred until it can be measured per segment with the SIC/financials flag (ADR 0014).
