# Plan: Batch Scoring for Top/Bottom/All Companies Reports

## Context
- Research: `.prompts/batch-scoring-reports-research/research.md`
- Guidelines: `CLAUDE.md`

## Instructions
1. Read research.md thoroughly — it contains the recommended architecture (ETL pre-computation to a `company_scores` summary table), proposed schema, batch SQL sketch, API shape, and patterns to follow.
2. Design implementation as checkpoints following the execution order from research section 13.
3. Each checkpoint must include:
   - Build: what to implement
   - Test: what unit tests to write for THIS checkpoint's code
   - Verify: how to confirm all existing + new tests pass before moving on
4. NEVER design a dedicated "testing" checkpoint at the end. Tests are written alongside the code they verify, within the same checkpoint. Each checkpoint must leave the test suite green.

## Key Constraints (from research)
- Follow the **Statement pattern** for all new DB operations (each operation = separate class inheriting from `QueryDbStmtBase`, `NonQueryDbStmtBase`, or `BulkInsertDbStmtBase<T>`)
- Follow the **Result pattern** — use `Result<T>` / `Result` with `ErrorCodes` enum, no exceptions
- **No LINQ** in production code — explicit loops only (LINQ is fine in tests)
- **No tuples** — use record types for multi-value returns
- **ImplicitUsings disabled** — all `using` statements must be explicit
- **TreatWarningsAsErrors** — code must compile warning-free
- New `IDbmService` methods require corresponding implementations in both `DbmService` (PostgreSQL) and `DbmInMemoryService` (test double backed by `DbmInMemoryData`)
- Next available migration number is **V013** (V012 is taken by `AddDataPointUpsertIndex`)
- Reuse existing `internal static` methods from `ScoringService`: `GroupByYear`, `ComputeDerivedMetrics`, `EvaluateChecks`, and all resolvers
- Use existing pagination types: `PaginationRequest`, `PaginationResponse`, `PagedResults<T>`
- Use `COUNT(*) OVER()` + `LIMIT/OFFSET` pagination pattern from `SearchCompaniesStmt`

## Key Files to Reference
- `dotnet/Stocks.Persistence/Services/ScoringService.cs` — scoring logic to reuse
- `dotnet/Stocks.Persistence/Database/Statements/GetScoringDataPointsStmt.cs` — single-company query to adapt
- `dotnet/Stocks.Persistence/Database/Statements/SearchCompaniesStmt.cs` — pagination + LATERAL JOIN pattern
- `dotnet/Stocks.Persistence/Database/Statements/GetDashboardStatsStmt.cs` — CTE aggregate pattern
- `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompaniesStmt.cs` — bulk insert pattern
- `dotnet/Stocks.Persistence/Database/Statements/DbStmtBase.cs` — base classes (`QueryDbStmtBase`, `BulkInsertDbStmtBase<T>`, `NonQueryDbStmtBase`)
- `dotnet/Stocks.Persistence/Database/IDbmService.cs` — interface to extend
- `dotnet/Stocks.Persistence/Database/DbmService.cs` — PostgreSQL implementation
- `dotnet/Stocks.Persistence/Database/DbmInMemoryService.cs` — in-memory test double
- `dotnet/Stocks.Persistence/Database/DbmInMemoryData.cs` — backing data for test double
- `dotnet/Stocks.DataModels/Scoring/` — existing scoring data models
- `dotnet/Stocks.DataModels/Pagination.cs` — `PaginationRequest`, `PaginationResponse`
- `dotnet/Stocks.DataModels/PagedResults.cs` — `PagedResults<T>`
- `dotnet/Stocks.WebApi/Endpoints/ScoringEndpoints.cs` — existing endpoint pattern
- `dotnet/Stocks.EDGARScraper/Program.cs` — CLI command orchestration (switch on `args[0]`)
- `dotnet/Stocks.EDGARScraper.Tests/` — test project location
- `dotnet/Stocks.Persistence/Database/Migrations/` — SQL migration files (Evolve, auto-run on startup)

## Output
Write plan to `.prompts/batch-scoring-reports-plan/plan.md`:
- Ordered checkpoints (implementation + tests each — no checkpoint without tests unless it is purely non-code work like documentation or configuration)
- Files to create/modify per checkpoint
- Metadata block (Status, Dependencies, Open Questions, Assumptions)
