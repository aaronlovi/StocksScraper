# Plan: Financial Statements Web Application

## Context
- Research: `.prompts/financial-statements-web-app-research/research.md`
- Guidelines: `CLAUDE.md`

## Instructions
1. Read research.md
2. Design implementation as checkpoints following the three-phase approach from research (Data Layer Gaps → Web API → Angular Front-End)
3. Each checkpoint must include:
   - Build: what to implement (specific files, classes, methods)
   - Test: what unit tests to write for THIS checkpoint's code
   - Verify: how to confirm all existing + new tests pass before moving on
4. NEVER design a dedicated "testing" checkpoint at the end. Tests are written alongside the code they verify, within the same checkpoint. Each checkpoint must leave the test suite green.

## Key Constraints (from research.md)

### Coding Conventions
- No LINQ in production code (explicit loops only; LINQ acceptable in tests)
- No tuples (use record types)
- Explicit `using` statements (ImplicitUsings disabled)
- Result pattern (`Result<T>`) for all service methods
- Statement pattern for DB operations (each query in its own class under `Database/Statements/`)
- TreatWarningsAsErrors — must compile warning-free
- Tests required (xUnit, Moq for mocking `IDbmService`)

### Data Layer Gaps to Address
- `company_tickers` table with CIK-to-ticker mappings (currently filesystem-only via SEC JSON)
- `GetSubmissionsByCompanyIdStmt` with index on `submissions.company_id`
- Unified search statement with `pg_trgm` for partial name matching
- `GetDashboardStatsStmt` for aggregate statistics

### Architecture Decisions
- New `dotnet/Stocks.WebApi/` project with REST/JSON (not gRPC-Web)
- References `Stocks.Persistence`, `Stocks.DataModels`, `Stocks.Shared`
- `IDbmService` as singleton, same DI pattern as `Stocks.DataService/Program.cs`
- Extract statement traversal from `StatementPrinter` into `StatementDataService` returning a `StatementData` record
- In-memory trie for typeahead, `pg_trgm` for paginated search results
- Angular workspace at `frontend/`

## Output
Write plan to `.prompts/financial-statements-web-app-plan/plan.md`:
- Ordered checkpoints (implementation + tests each — no checkpoint without tests unless purely non-code work)
- Files to create/modify per checkpoint
- Metadata block (Status, Dependencies, Open Questions, Assumptions)
