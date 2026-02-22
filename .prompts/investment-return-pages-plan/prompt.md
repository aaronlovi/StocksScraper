# Plan: Remove return1y Column and Create Investment Return Report Pages

## Context
- Research: `.prompts/investment-return-pages-research/research.md`
- Guidelines: `CLAUDE.md`
- Previous (incorrect) implementation to undo: commit `2ac0fe8`

## Goal
1. Remove the `return1y` column from existing Graham and Buffett score list pages
2. Remove the inline investment-return sections from Graham and Buffett detail pages
3. Remove per-company `InvestmentReturnEndpoints`, `InvestmentReturnService`, and per-ticker price statements
4. Create two new sidebar-linked report pages — Graham Returns and Buffett Returns — showing all scored companies enriched with investment return data (total return %, annualized return %, $1,000 invested) for a user-selected start date

## Instructions
1. Read `research.md` — it contains the complete removal checklist, backend endpoint design, UI design, date picker behavior, and existing patterns to follow
2. Design implementation as checkpoints
3. Each checkpoint must include:
   - Build: what to implement
   - Test: what unit tests to write for THIS checkpoint's code
   - Verify: how to confirm all existing + new tests pass before moving on
4. NEVER design a dedicated "testing" checkpoint at the end. Tests are written alongside the code they verify, within the same checkpoint. Each checkpoint must leave the test suite green.

## Checkpoint Ordering Guidance

The research identifies three phases. Map these to checkpoints in this order:

### Phase 1: Remove return1y (clean up existing code)
- Remove `Return1y` from DTOs, enums, DB statements, in-memory data, endpoints, batch pipeline, frontend interfaces, frontend components
- Create drop-column migration `V019__DropReturn1yFromScoreTables.sql`
- Remove inline investment-return sections from detail pages (scoring.component.ts, moat-scoring.component.ts)
- Remove per-company `InvestmentReturnEndpoints`, `InvestmentReturnService`, `InvestmentReturnResult`, `GetPriceNearDateStmt`, `GetLatestPriceByTickerStmt`, and their IDbmService/DbmService/DbmInMemoryService/DbmInMemoryData implementations
- Remove frontend `InvestmentReturnResponse` interface and `getInvestmentReturn()` API method
- Delete test files: `Return1yEnrichmentTests.cs`, `InvestmentReturnServiceTests.cs`
- Update `MoatScoringModelTests.cs` (remove `Return1y: null`)
- Update `scoring.component.spec.ts` (remove investment-return test expectations)

### Phase 2: New backend endpoints
- New response DTO(s) for score + return data
- New service or endpoint logic: fetch all scores (unpaginated), fetch batch prices via `GetAllPricesNearDate(startDate)` + `GetAllLatestPrices()`, compute returns, sort, paginate in memory
- Two new endpoints: `GET /api/reports/graham-returns` and `GET /api/reports/buffett-returns` with query params: `startDate`, `page`, `pageSize`, `sortBy`, `sortDir`, `minScore`, `exchange`
- Wire up in WebApi `Program.cs`

### Phase 3: New frontend pages
- Two new standalone Angular components: `GrahamReturnsReportComponent` and `BuffettReturnsReportComponent`
- Follow existing report component pattern (filters, sortable table, pagination)
- Add date picker to filters row (default: 1 year ago)
- Columns: Score, Company (link), Ticker, Exchange, Price, Total Return %, Annualized Return %, $1,000 Invested
- New API service methods
- New routes in `app.routes.ts`
- New sidebar links in `sidebar.component.ts`

## Key Technical Details from Research

- **Sorting by return columns** requires fetching ALL scores unpaginated, computing returns, sorting in memory, then paginating. The dataset is small (~50-200 companies) so this is fine.
- **Batch price lookups**: Reuse existing `GetAllPricesNearDateStmt` and `GetAllLatestPricesStmt` (kept from commit `2ac0fe8`).
- **Return computation**: `totalReturnPct = (endPrice / startPrice - 1) * 100`, `annualizedReturnPct = (Math.Pow(ratio, 365.25/days) - 1) * 100`, `currentValueOf1000 = 1000 * endPrice / startPrice`. Handle missing price data as null.
- **Date picker**: Single start date, end is always latest price. Default 1 year ago. Resets page to 1 on change.
- **`CompanyEndpoints.cs`** does NOT depend on `GetLatestPriceByTicker` — safe to delete all per-ticker price infrastructure.

## Output
Write plan to `.prompts/investment-return-pages-plan/plan.md`:
- Ordered checkpoints (implementation + tests each — no checkpoint without tests unless it is purely non-code work)
- Files to create/modify per checkpoint
- Metadata block (Status, Dependencies, Open Questions, Assumptions)

<!-- Self-review: converged after 1 pass -->
