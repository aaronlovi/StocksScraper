# Research: Investment Return Report Pages (New Sidebar-Linked Pages)

## Objective
Understand what's needed to create two new standalone report pages — one for Graham scores and one for Buffett scores — that show top-scored companies with investment return data (total return %, annualized return %, $1,000 invested). These are new sidebar links, NOT modifications to existing pages. Also understand what must be removed: the `return1y` column added to the existing list pages.

## Context
- Guidelines: `CLAUDE.md`
- Previous (incorrect) implementation: commit `2ac0fe8` ("Investment Return Backtesting Reporting") added `return1y` columns to existing list pages and inline investment-return sections to detail pages. The user wants the `return1y` column removed and replaced with dedicated report pages.
- Existing research (still useful for backend patterns): `.prompts/investment-return-report-research/research.md`
- Sidebar: `frontend/stocks-frontend/src/app/core/layout/sidebar/sidebar.component.ts` — simple `routerLink` entries
- Routes: `frontend/stocks-frontend/src/app/app.routes.ts` — lazy-loaded standalone components
- Existing report pattern: `ScoresReportComponent` and `MoatScoresReportComponent` — filterable, sortable, paginated tables

## Questions to Answer

1. **What code from commit `2ac0fe8` should be kept vs removed?** The commit added backend services (`InvestmentReturnService`, `GetPriceNearDateStmt`, `GetLatestPriceByTickerStmt`, `GetAllPricesNearDateStmt`), the `return_1y` DB column/migration, enrichment in the batch pipeline (`Program.cs`), frontend `return1y` column in list pages, and inline investment-return sections on detail pages. Identify precisely which pieces to keep (reusable backend services/statements), which to remove (return1y column from list pages, inline sections from detail pages, migration, DTO fields), and which to modify.

2. **What backend endpoint is needed for the new pages?** The new pages need to show all scored companies (from `company_scores` or `company_moat_scores`) enriched with investment return data for a user-selected start date. Should this be a new endpoint that combines score data + return data, or should the frontend call two endpoints (existing scores + a new batch-return endpoint)? What are the performance implications of computing returns for 50-100 companies on each page load?

3. **What does the new page UI look like?** Examine the existing `ScoresReportComponent` (`frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts`) in detail. The new pages should follow the same table/filter/sort/pagination pattern but display: company name, ticker, score, total return %, annualized return %, $1,000 invested value. What columns from the existing report should carry over, and what's new?

4. **How should the date picker work?** The user wants a date picker on the new report pages. When the date changes, the return data recomputes for all companies in the list. Should this be a single date (start date, with "end" always being the latest price)? What's the default date? How does this interact with pagination and sorting — does the backend need to accept the date parameter in the existing report endpoints, or is a separate endpoint cleaner?

5. **What changes are needed in the existing report pages to remove `return1y`?** Trace all files that reference `return1y` or `Return1y` — DTOs, statements, migrations, endpoints, frontend interfaces, frontend components. List each file and what needs to change. Can the migration be reversed or should it stay (column present but unused)?

## Explore
- `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts` — full file, understand the table/filter/sort pattern
- `frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.ts` — full file, compare with Graham report
- `frontend/stocks-frontend/src/app/core/services/api.service.ts` — existing API methods for reports
- `dotnet/Stocks.WebApi/Endpoints/ReportEndpoints.cs` — existing report endpoint pattern
- `dotnet/Stocks.WebApi/Endpoints/MoatReportEndpoints.cs` — existing moat report endpoint pattern
- `dotnet/Stocks.Persistence/Services/InvestmentReturnService.cs` — existing service from current commit (keep or modify?)
- `dotnet/Stocks.Persistence/Database/Statements/GetAllPricesNearDateStmt.cs` — batch price lookup (reusable?)
- `dotnet/Stocks.EDGARScraper/Program.cs` — batch pipeline enrichment code to understand and potentially remove
- `dotnet/Stocks.DataModels/Scoring/CompanyScoreSummary.cs` — DTO with Return1y field to remove
- `dotnet/Stocks.DataModels/Scoring/CompanyMoatScoreSummary.cs` — DTO with Return1y field to remove
- Search all files for `return1y` and `Return1y` to find every reference

## Output
Write findings to `.prompts/investment-return-pages-research/research.md`:
- Answers to the questions above
- Existing patterns to follow
- Risks or concerns
- Recommended approach
- Metadata block (append at end):
  ## Metadata
  ### Status
  [success | partial | failed]
  ### Dependencies
  - [files or decisions this relies on, or "None"]
  ### Open Questions
  - [unresolved issues, or "None"]
  ### Assumptions
  - [what was assumed, or "None"]

<!-- Self-review: converged after 1 pass -->
