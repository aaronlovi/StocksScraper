# Research: Company Scoring System (13-Point Value Score)

## Objective
Determine which US-GAAP concepts are needed to compute the tsx-aggregator's 13-point scoring system, verify their availability in our database, and assess feasibility across the SEC company universe.

## Context
- Project guidelines: `CLAUDE.md`
- Reference scoring implementation: `tsx-aggregator/src/tsx-aggregator.models/AggregatorData/CompanyFullDetailReport.cs`
- Reference aggregation logic: `tsx-aggregator/src/tsx-aggregator/Aggregated/`
- Reference report model: `tsx-aggregator/src/tsx-aggregator.models/AggregatorData/CompanyReport.cs`
- Our data models: `dotnet/Stocks.DataModels/DataPoint.cs`, `dotnet/Stocks.DataModels/PriceRow.cs`
- Our database migrations: `dotnet/Stocks.Persistence/Database/Migrations/`
- Stock prices are in the `prices` table (daily OHLC by ticker)

## Important Notes
- The `tsx-aggregator/` directory is a read-only reference project. Do not modify any files in it.
- Database queries on `data_points` can be slow (millions of rows). Use a 60-second timeout for psql commands.

## Database Access
```bash
PGPASSWORD=postgres psql -U postgres -h localhost -d stocks-data
```
- 10-K filings have `filing_type = 1`, 10-Q have `filing_type = 2`
- Data points join to taxonomy concepts via `taxonomy_concept_id`
- ~13,490 companies have 10-K filings with data points
- The `taxonomy_concepts` table has rows per taxonomy year (e.g., us-gaap-2020 through us-gaap-2025), so the same concept name appears multiple times. Use `DISTINCT` on `tc.name` in queries, or filter by a single `taxonomy_type_id`.

## The 13 Checks (from tsx-aggregator)
| # | Check | Pass Condition |
|---|-------|---------------|
| 1 | Debt-to-Equity | < 0.5 |
| 2 | Book Value | > $150M |
| 3 | Price-to-Book | ≤ 3.0 |
| 4 | Avg Net Cash Flow Positive | 5-year average > 0 |
| 5 | Avg Owner Earnings Positive | 5-year average > 0 |
| 6 | Est. Return (Cash Flow) Big Enough | > 5% |
| 7 | Est. Return (Owner Earnings) Big Enough | > 5% |
| 8 | Est. Return (Cash Flow) Not Too Big | < 40% |
| 9 | Est. Return (Owner Earnings) Not Too Big | < 40% |
| 10 | Debt-to-Book | < 1.0 |
| 11 | Retained Earnings Positive | > 0 |
| 12 | History Long Enough | ≥ 4 years of annual reports |
| 13 | Retained Earnings Increased | Current > 5 years ago |

## Questions to Answer

### Q1: Formulas and Concept Mapping
Read the tsx-aggregator source code to extract the exact formulas for all derived metrics. Then map each input field to US-GAAP concept name(s).

**Derived metrics to document:**
- Book Value — read `CompanyReport.cs` for field definitions
- Debt-to-Equity Ratio — read `CompanyFullDetailReport.cs` for the check
- Debt-to-Book Ratio — same file
- Market Cap — same file
- Price-to-Book Ratio — same file
- Net Cash Flow (and its components) — read `CompanyReport.cs` fields like `GrossCashFlow`, `NetCashFlow`; trace how they're populated in `tsx-aggregator/src/tsx-aggregator/Aggregated/`
- Owner Earnings (and its components) — same approach, look for `OwnerEarnings` fields
- Estimated Next Year Total Return (both CF and OE variants) — `CompanyFullDetailReport.cs`

**For each input field**, find the corresponding US-GAAP concept(s) by searching the `taxonomy_concepts` table. Many fields have multiple plausible concept names (e.g., `LongTermDebt` vs `LongTermDebtNoncurrent` vs `LongTermDebtAndCapitalLeaseObligations`). List all candidates.

Present the mapping as a table:
| tsx-aggregator Field | US-GAAP Concept(s) | Period Type (instant/duration) |
|---|---|---|

### Q2: Data Availability Statistics
For each US-GAAP concept identified in Q1, query the database:

```sql
SELECT tc.name, COUNT(DISTINCT dp.company_id) as num_companies
FROM data_points dp
JOIN taxonomy_concepts tc ON dp.taxonomy_concept_id = tc.taxonomy_concept_id
JOIN submissions s ON dp.submission_id = s.submission_id AND dp.company_id = s.company_id
WHERE tc.name IN (/* concepts from Q1 */)
AND s.filing_type = 1  -- 10-K only
GROUP BY tc.name
ORDER BY num_companies DESC;
```

Present results as a table with company count and coverage percentage (out of ~13,490 companies with 10-K data points). Where a field has multiple candidate concepts, check each individually AND their union (i.e., companies that have at least one of the alternatives). Run queries in batches of related concepts to avoid timeouts on large joins.

### Q3: Multi-Year Data Depth
The scoring system needs ≥ 4 years (check 12) and ideally 5 years (for averages) of annual data.

Count distinct `EXTRACT(YEAR FROM s.report_date)` per company for 10-K filings that have data points:
- How many companies have ≥ 4 distinct report years?
- How many have ≥ 5?

Then for the key concepts (retained earnings, net income, shareholders equity), check how many companies have that concept in ≥ 4 distinct 10-K report years. This tells us whether multi-year scoring is feasible for most companies or only a subset.

### Q4: Feasibility Assessment
Based on Q2 and Q3 findings, classify each of the 13 checks:
- **Reliable** — core concepts have > 70% coverage among 10-K filers with data
- **Partial** — achievable with fallback concepts or < 70% coverage
- **Risky** — key inputs missing for most companies

Recommend how to handle each category (compute, skip, show N/A).

## Output
Write findings to `.prompts/company-scoring-system-research/research.md` with these sections:
1. Formulas (confirmed from tsx-aggregator source)
2. Concept mapping table
3. Data availability table
4. Multi-year depth analysis
5. Feasibility classification per check
6. Recommendations

Append metadata block:
```
## Metadata
### Status
[success | partial | failed]
### Dependencies
- [files or decisions this relies on]
### Open Questions
- [unresolved issues]
### Assumptions
- [what was assumed]
```
