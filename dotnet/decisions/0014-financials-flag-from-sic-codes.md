# 14. Financials Flag from SEC SIC Codes

## Status
Accepted (2026-11-22). Amended by ADR 0015: scoring reads the SIC to pick the net cash flow basis of financials.

## Context
The value score (15 checks) and the moat score (13 checks) treat every company alike (ADR 0013). For banks, insurers and other financials several inputs mean something different: deposits are financing flows that the net cash flow counts as cash generated (open item E5), debt and book value are the business itself, and capex is usually untagged. Two scoring changes, E5 (financing-total net cash flow) and E6 (Σ ÷ Σ ROE with average equity), were measured with financials mixed in and deferred until financials could be seen separately. On the 2026-11-22 data, 108 of the 259 companies at value 13+ and 14 of the 32 at 15/15 are financials.

The project had no industry field. The SEC publishes one per registrant, the Standard Industrial Classification (SIC) code, in the submissions bulk file we already download. The requirements are in `docs/sic-financials-flag/requirements.md`; the measurements and specs are in the Obsidian note *EDGAR Scoring Bug Fix Plan* (sections 4.6, 9.22, 9.23).

## Decision

### Source and storage
- `sic` and `sicDescription` come from each `CIK##########.json` in `submissions.zip`. Paginated `-submissions-NNN.json` files have no company fields and are skipped. `sic` is a 4-digit string, empty when the SEC lists none.
- Stored on `companies` (`sic_code int`, `sic_description varchar(200)`, both nullable; V023).
- Written by its own command, `--import-company-sic` (`CompanySicImporter`). It writes every known company, so an empty SIC clears an old one, and it can be re-run without reimporting submissions. Reimport order: right after `--parse-bulk-edgar-submissions-list`.

### Financial = SIC 6000–6499
- Covered: depository institutions (60xx), non-depository credit (61xx), brokers and dealers (62xx), insurance carriers (63xx) and insurance agents (64xx).
- Not financial: real estate (65xx), REITs (6798), blank checks (6770), other 67xx, a missing SIC.
- The range is defined once, in `IndustryClassification` (`FinancialSicMin`, `FinancialSicMax`, `IsFinancial`). SQL binds the two constants as parameters, and the frontend uses the API's `isFinancial`.

### Read-time flag, nothing excluded
- Scoring ignores the SIC: no check, score, `check_results` or stored score row changes.
- The value and moat score reads (`GetCompanyScoresStmt`, `GetCompanyMoatScoresStmt`) `LEFT JOIN companies` and return `sicCode`, `sicDescription` and `isFinancial`.
- `financials=only|exclude` (any other value = all) filters `/api/reports/scores`, `/api/reports/moat-scores` and `/api/reports/graham-backtest`. Exclude keeps companies without a SIC.
- The value, moat and backtest pages have a Financials select. The two score pages show a "Financial" badge whose tooltip gives the SIC code and description.
- `--snapshot-scores` writes `sic_code` and `is_financial` columns, and `scripts/score_snapshot_diff.py --split financials` reports financials and non-financials separately. These are the tools for measuring E6 and E5.

## Consequences
- **No score change:** a snapshot of the finished code equals the stored `company_scores` (score, computable checks, years, check results) and `company_moat_scores` for all 14,041 companies.
- **Measured 2026-11-22:** 74,208 of 994,026 SEC registrants have a SIC. Of the 14,041 scored companies, 13,841 have one and 1,836 are financial. Value 13+: 108 financial, 151 other; 15/15: 14 of 32; moat 11+: 9 of 107. Spot checks: AAPL 3571, BAC 6021, BRK-B 6331. The import takes about 80 s.
- **Today's SIC for the past:** the backtest filter classifies each snapshot date's constituents by their current SIC, so a company reclassified since then is filtered by its new code. Accepted, so the SIC doesn't have to be stored per snapshot.
- **SIC is coarse:** Berkshire Hathaway files as an insurer (6331), and some fintechs file under non-financial codes. The badge tooltip shows the code, so the reader can judge.
- **Reimports:** `companies` is truncated and refilled by `--get-full-cik-list`, so `--import-company-sic` must run again after every reimport.
- **The Graham snapshot page** (`/api/reports/graham-snapshot`) does not take the filter.
- **Next:** E6, then E5, each measured with `--split financials` and backtested per segment (requirements doc, section 6). The owner expects net cash flow may need a different definition for financials (E5).

## Alternatives Considered
- **Include REITs (6798) and/or real estate (65xx):** another 801 scored companies, 8 of them at value 13+. Rejected: their issues (development spend, leverage on real assets) are not deposit or float issues, and the common sector schemes have treated real estate as its own sector since 2016. A separate "Real estate" flag would be cleaner if it's ever needed.
- **Flag blank checks (6770):** 1,100 scored companies, none at 13+, so the groups that matter don't change. Left for later.
- **The SEC's `ownerOrg` ("02 Finance"):** it names the SEC review office, not the industry, and the office split changes over time.
- **Parse the SIC inside `--parse-bulk-edgar-submissions-list`:** that step inserts submissions and can't be re-run, so an existing DB couldn't be filled without a full reimport.
- **Copy the SIC into the score and snapshot tables:** this would need a rebuild of every score table (about 2 h for the 61 backtest dates) to add or change a classification. The read-time join needs none.
- **Exclude financials from scoring, or use bank-specific checks:** rejected by the owner for now ("exclude nothing"). The flag is the first step that makes those options measurable.
