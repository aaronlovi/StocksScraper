# Plan: Moat Score Implementation

## Context
- Research: `.prompts/moat-score-research/research.md`
- Design spec: `docs/moat-score-design.md`
- Guidelines: `CLAUDE.md`, `dotnet/project_fact_sheet.md`, `dotnet/project_instructions.md`

## Instructions
1. Read research.md — it contains answers to 18 questions covering backend scoring logic, new metrics, data layer, API endpoints, frontend (list page, detail page, sparkline, routing, API service), testing, and cross-cutting concerns.
2. Read the design spec `docs/moat-score-design.md` for the authoritative check definitions, thresholds, and trend chart specifications.
3. Design implementation as checkpoints, ordered so each checkpoint builds on the previous and leaves the codebase in a compilable, test-passing state.
4. Each checkpoint must include:
   - **Build**: what to implement (specific files to create/modify, specific classes/methods/records)
   - **Test**: what unit tests to write for THIS checkpoint's code
   - **Verify**: how to confirm all existing + new tests pass before moving on
5. NEVER design a dedicated "testing" checkpoint at the end. Tests are written alongside the code they verify, within the same checkpoint. Each checkpoint must leave the test suite green.

## Key Implementation Decisions (from research)

These decisions were identified during research. Apply them in the plan:

- **Separate service:** Create `MoatScoringService.cs` in `Stocks.Persistence/Services/` — NOT extending `ScoringService`. Call existing static helpers (`ResolveField`, `ResolveEquity`, `GroupAndPartitionData`) directly from `ScoringService`.
- **Separate table:** Create `company_moat_scores` table via migration, with Moat-specific columns (gross margin, operating margin, revenue CAGR, capex ratio, interest coverage, etc.).
- **Year limit:** The existing `GetScoringDataPointsStmt` and `GetAllScoringDataPointsStmt` limit to 5 annual dates. The Moat Score needs 7+ years (check 11). Parameterize the year limit or create Moat-specific statement classes.
- **New concepts:** 10 new XBRL concept tags (Revenue, COGS, GrossProfit, OperatingIncomeLoss, InterestExpense fallback chains) — define in `MoatScoringService` as static arrays.
- **Shared models:** Reuse `ScoringCheck`, `ScoringCheckResult`, `ScoringConceptValue`, `BatchScoringConceptValue`. Create new `MoatDerivedMetrics`, `MoatScoringResult`, `CompanyMoatScoreSummary` records.
- **API:** Create `MoatScoringEndpoints.cs` and `MoatReportEndpoints.cs` — independent from Value Score endpoints. Include per-year trend data in the on-demand endpoint response.
- **Frontend:** Create `MoatScoringComponent` and `MoatScoresReportComponent` — new standalone components. Extract a reusable sparkline helper for the 6 trend charts.
- **`MakeCheck`:** Currently private in `ScoringService` — make it `internal` so `MoatScoringService` can call it, or duplicate (it's a one-liner).

## Checkpoint Ordering Constraints

1. Data models must come before service logic (service references models).
2. Data layer (migration, statements) must come before service logic that queries the database.
3. Service logic must come before API endpoints (endpoints call service).
4. Backend must be complete before frontend (frontend calls API).
5. Frontend routing/sidebar comes after frontend components exist.
6. CLI command can come after service logic is complete.

## Output
Write plan to `.prompts/moat-score-plan/plan.md`:
- Ordered checkpoints (implementation + tests each — no checkpoint without tests unless it is purely non-code work like documentation or configuration)
- Files to create/modify per checkpoint
- Metadata block (Status, Dependencies, Open Questions, Assumptions)

<!-- Self-review: converged after 1 pass -->
