# Research Findings: Financial Statements Web Application

## Table of Contents

- [1. Data Layer & API Surface](#1-data-layer--api-surface)
  - [1.1. Company Search Data](#11-company-search-data)
  - [1.2. Submissions Per Company](#12-submissions-per-company)
  - [1.3. Statement Rendering Reuse](#13-statement-rendering-reuse)
  - [1.4. Taxonomy Year Resolution](#14-taxonomy-year-resolution)
  - [1.5. Dashboard Statistics](#15-dashboard-statistics)
- [2. Architecture & Integration](#2-architecture--integration)
  - [2.1. Web API vs gRPC](#21-web-api-vs-grpc)
  - [2.2. Long-Running Backend](#22-long-running-backend)
  - [2.3. Trie-Based Search](#23-trie-based-search)
  - [2.4. Project Structure](#24-project-structure)
  - [2.5. Shared Persistence](#25-shared-persistence)
- [3. Front-End Design](#3-front-end-design)
  - [3.1. Angular Routing Structure](#31-angular-routing-structure)
  - [3.2. Pagination Support](#32-pagination-support)
  - [3.3. Report Type Enumeration](#33-report-type-enumeration)
- [4. Existing Patterns to Follow](#4-existing-patterns-to-follow)
- [5. Risks and Concerns](#5-risks-and-concerns)
- [6. Recommended Approach](#6-recommended-approach)

---

## 1. Data Layer & API Surface

### 1.1. Company Search Data

**Current state**: No unified search exists. Searching by CIK, ticker, or company name each requires different data paths.

**Companies table** (`V001__CreateTables.sql`):
- Indexes: `idx_companies_cik` on `(cik)`, `idx_companies_data_source` on `(data_source)`
- No full-text or trigram index on any column

**Company names table** (`V001__CreateTables.sql`):
- Index: `idx_company_names_company_id` on `(company_id)` — reverse lookup only
- **No index on `name`** — name-based search would require a full table scan

**Ticker/symbol storage**:
- `Instrument` record (`Stocks.DataModels/Instrument.cs`) holds `(Symbol, Name, Exchange)` but is **in-memory only** — not persisted to a database table
- Ticker-CIK mappings exist indirectly in `prices`, `price_imports`, and `price_downloads` tables (via `V007__AddPrices.sql`, `V009__AddPriceDownloads.sql`)
- `SecTickerMappingsDownloader.cs` downloads `company_tickers.json` and `company_tickers_exchange.json` from SEC but writes them to the **filesystem only**, not to the database
- **No dedicated ticker master table** exists

**Existing query statements**:
- `GetCompanyByIdStmt` — by `company_id` only
- `GetAllCompaniesByDataSourceStmt` — all companies for a data source, no filtering
- `GetPagedCompaniesByDataSourceStmt` — paginated, no name/ticker search

**Gaps requiring new work**:
- A `company_tickers` table (or similar) to persist CIK-to-ticker mappings
- A unified search statement that queries across `companies.cik`, `company_names.name`, and ticker symbols
- A trigram or full-text index on `company_names.name` for partial matching
- An index on the ticker column for fast lookups

### 1.2. Submissions Per Company

**Submission model** (`Stocks.DataModels/Submission.cs`):
```
SubmissionId (ulong), CompanyId (ulong), FilingReference (string),
FilingType (enum), FilingCategory (enum), ReportDate (DateOnly), AcceptanceTime (DateTime?)
```

**Filing types** (16 values): TenK, TenQ, EightK, EightK_A, TenK_A, TenQ_A, TenKT_A, TenQT_A, TenKT, TenQT, FortyF, FortyF_A, TwentyF, TwentyF_A, SixK, SixK_A

**Filing categories** (3 values): Annual, Quarterly, Other

**Existing query**: Only `GetAllSubmissionsStmt` exists — fetches ALL submissions with no WHERE clause.

**Gaps**:
- **No `GetSubmissionsByCompanyId` statement** — critical gap for the semi-detail view
- No index on `submissions.company_id` alone (only a unique constraint on `(company_id, filing_reference)`)
- No index on `filing_type` or `filing_category`
- Need a new statement: `GetSubmissionsByCompanyIdStmt` with `WHERE company_id = @company_id ORDER BY report_date DESC`

### 1.3. Statement Rendering Reuse

**Current architecture** (`Stocks.EDGARScraper/Services/Statements/StatementPrinter.cs`): The class mixes data assembly with output formatting. However, the traversal logic is cleanly separable.

**Extractable components**:

| Component | Location (StatementPrinter.cs) | Input | Output |
|-----------|-------------------------------|-------|--------|
| `BuildParentToChildrenMap` | lines 513-527 | presentations, roleName | `Dictionary<long, List<PresentationDetailsDTO>>` |
| `TraverseConceptTree` | lines 416-422 | TraverseContext | `Result<List<HierarchyNode>>` |
| `BuildChildrenMap` | lines 459-475 | hierarchy nodes | childrenMap + rootNodes |
| `HasValueOrChildValue` | lines 477-494 | conceptId, childrenMap, dataPointMap | includedConceptIds |

**HierarchyNode** (`Stocks.EDGARScraper/Models/Statements/HierarchyNode.cs`): Record with `ConceptId`, `Name`, `Label`, `Depth`, `ParentConceptId`.

**TraverseContext** (`Stocks.EDGARScraper/Models/Statements/TraverseContext.cs`): Record with `ConceptId`, `ParentToChildren`, `ConceptMap`, `Depth`, `MaxDepth`, `ParentConceptId`, `Visited`.

**JSON output format** (from `BuildJsonTree`, StatementPrinter.cs line 382):
```json
{
  "ConceptName": "Assets",
  "Label": "Total Assets",
  "Value": 350000,
  "Children": [
    { "ConceptName": "CurrentAssets", "Label": "Current Assets", "Value": 150000, "Children": [...] }
  ]
}
```

**Recommendation**: Extract a `StatementDataService` that returns a `StatementData` record (containing `List<HierarchyNode> Hierarchy`, `Dictionary<long, DataPoint> DataPointMap`, `HashSet<long> IncludedConceptIds`). No tuples per project convention. The Web API controller serializes to JSON; the CLI continues to use `StatementPrinter` for CSV/HTML output.

### 1.4. Taxonomy Year Resolution

**Current logic** (`Stocks.EDGARScraper/Program.cs:192`):
```csharp
int taxonomyYear = parsed.TaxonomyYear ?? parsed.Date.Year;
```

Then looks up via `GetTaxonomyTypeByNameVersion("us-gaap", taxonomyYear)`.

**For the Web API**: Use `submission.ReportDate.Year` as the default taxonomy year. The `Submission.ReportDate` field is `DateOnly`, confirmed at `Stocks.DataModels/Submission.cs:7-16`.

**Recommendation**: Default to filing report year. Optionally expose a `?taxonomyYear=YYYY` query parameter for override. This mirrors the CLI pattern exactly.

### 1.5. Dashboard Statistics

**Already available** (no new queries needed):
- Company count — via `GetPagedCompaniesByDataSource` CTE (returns total count)
- Taxonomy concept/presentation counts — `GetTaxonomyConceptCountByType`, `GetTaxonomyPresentationCountByType`

**Require new DB statements**:
- Total submission count (currently only get-all, count in-app)
- Submissions grouped by filing type/category
- Filing date range (min/max report_date)
- Companies with price data (count from `price_imports`)
- Data points per company (aggregate)

**Recommendation**: Create a single `GetDashboardStatsStmt` that runs multiple `COUNT(*)` / `MIN()` / `MAX()` in one database round trip using CTEs.

---

## 2. Architecture & Integration

### 2.1. Web API vs gRPC

**Current state**: `Stocks.DataService` uses **standard gRPC only** (no gRPC-Web).
- `Stocks.DataService/Startup.cs:22`: `services.AddGrpc()` — standard gRPC only
- `Stocks.DataService/Program.cs:51`: Kestrel configured for HTTP/2 only (`HttpProtocols.Http2`)
- No CORS configuration, no gRPC-Web middleware
- Single RPC method: `RawDataService.GetCompaniesData`

**Recommendation**: Add a **new ASP.NET Web API project** (`Stocks.WebApi`) with REST/JSON endpoints. Reasons:
- Angular has mature HTTP client support for REST; gRPC-Web requires additional tooling (protoc codegen, `grpc-web` npm package)
- REST is simpler to debug, test, and document (OpenAPI/Swagger)
- The existing gRPC service can continue serving machine-to-machine consumers
- Both can coexist, sharing `IDbmService` via the `Stocks.Persistence` library

### 2.2. Long-Running Backend

**Current state**: `Stocks.DataService` IS a long-running service:
- `RawDataQueryProcessor` extends `BackgroundService` (`Stocks.DataService/RawDataService/RawDataQueryProcessor.cs:18`)
- Registered as hosted service (`Stocks.DataService/Program.cs:64`)
- Uses `System.Threading.Channels` for async message queuing
- Has a 1-minute heartbeat for monitoring

**For the new Web API**: The ASP.NET Web API host is itself long-running. If background work is needed (taxonomy tree caching, search index building), add `IHostedService` implementations within the Web API project. No separate backend process needed initially.

**Potential background tasks for later**:
- Pre-compute and cache taxonomy trees per year on startup
- Build in-memory trie for typeahead search on startup
- Periodic refresh of dashboard statistics

### 2.3. Trie-Based Search

**Elasticsearch**: Configured in `docker-compose.yml` (v8.16.2, port 9200). Kibana also available (port 5601). However, **no Elasticsearch client or integration exists in the .NET code**.

**Three options evaluated**:

| Option | Pros | Cons | Fit |
|--------|------|------|-----|
| **(a) In-memory trie** | Fast typeahead, no external deps, simple | Memory usage, cold start, stale data | Good for ~500k companies |
| **(b) PostgreSQL trigram** | Uses existing DB, `pg_trgm` extension | Slower than in-memory for typeahead, requires index migration | Good for search results page |
| **(c) Elasticsearch** | Already deployed, full-text search, fuzzy matching | Requires new NuGet package, index management, data sync | Overkill for current scale |

**Recommendation**: Use **(a) in-memory trie** for the title bar typeahead (loaded at startup from companies + tickers), and **(b) PostgreSQL with `pg_trgm`** for the full search results page with pagination. Elasticsearch can be adopted later if scale demands it.

### 2.4. Project Structure

**Current projects** (6 in `EDGARScraper.sln`):
1. `Stocks.DataModels` — shared DTOs
2. `Stocks.Shared` — utilities, protos, Result pattern
3. `Stocks.Persistence` — data access (PostgresExecutor, statements, migrations)
4. `Stocks.EDGARScraper` — CLI/ETL console app
5. `Stocks.DataService` — gRPC server
6. `Stocks.EDGARScraper.Tests` — xUnit tests

**Recommended additions**:
- `dotnet/Stocks.WebApi/` — ASP.NET Web API project (controllers, middleware, DI)
- `dotnet/Stocks.WebApi.Tests/` — xUnit tests for API layer
- `frontend/` — Angular workspace (top-level, outside `dotnet/`)

The Web API project references `Stocks.Persistence`, `Stocks.DataModels`, and `Stocks.Shared`. Add both new .csproj files to `EDGARScraper.sln`.

### 2.5. Shared Persistence

**Current DI pattern** (in `Stocks.DataService/Program.cs`):
- `PostgresExecutor` — registered as **singleton** (line 57)
- `DbmService` — registered as **singleton** (line 62), implements `IDbmService`
- `DbmService` constructor receives `IServiceProvider`, resolves `PostgresExecutor`, runs migrations on init
- `PostgresExecutor` uses semaphore-based concurrency limiting (20 concurrent statements, 20 concurrent reads)

**For the Web API**: Reference `Stocks.Persistence` directly and register `IDbmService` as singleton using the same pattern. No intermediate service layer needed — the existing `IDbmService` interface is clean and directly usable from controllers.

**Connection string**: Both the Web API and DataService read from `appsettings.json` key `ConnectionStrings:StocksData` (constant `DbmService.StocksDataConnectionStringName`).

---

## 3. Front-End Design

### 3.1. Angular Routing Structure

**Recommended routes**:

| Route | View | Parameters | Description |
|-------|------|------------|-------------|
| `/dashboard` | Dashboard | — | Summary statistics, default landing page |
| `/search` | Search results | `?q=...&page=1` | Paginated table from search query |
| `/company/:cik` | Company semi-detail | CIK | Filing dates, available report types |
| `/company/:cik/report/:submissionId/:concept` | Report detail | CIK, submission ID, root concept | Full statement rendering |

**Sidebar links**: Dashboard, Search (may also include saved/recent companies).

**Title bar**: Unified typeahead search box → navigates to `/search?q=...` on enter, or directly to `/company/:cik` on typeahead selection.

**Lazy loading**: Each view can be a lazy-loaded Angular module (`DashboardModule`, `SearchModule`, `CompanyModule`).

### 3.2. Pagination Support

**Existing model** (`Stocks.DataModels/Pagination.cs`):
- `PaginationRequest(pageNumber: uint, pageSize: uint)` — 1-based, max 100 per page
- `PaginationResponse(currentPage, totalItems, totalPages)`
- `PagedResults<T>(Items, Pagination)` — generic wrapper

**SQL pattern** (`GetPagedCompaniesByDataSourceStmt.cs`): CTE for total count + `LIMIT @limit OFFSET @offset` in one query.

**Reusability**: The pagination model is directly reusable for the Web API. Map to query parameters: `?page=1&pageSize=25`. Return `PaginationResponse` in the JSON response envelope. Reasonable default page size: 25 for search results, 10 for submissions list.

### 3.3. Report Type Enumeration

**"Report type" has two meanings** in this system:

1. **Filing type** (`FilingType` enum): 10-K, 10-Q, 8-K, etc. — categorizes the SEC filing itself.

2. **Presentation role** (from taxonomy): e.g., `"Statement - Balance Sheet"`, `"Statement - Cash Flows"`. These are the individual financial statements within a filing. The `--list-statements` mode outputs CSV: `RoleName,RootConceptName,RootLabel`.

**For the semi-detail view**: Show filings grouped by date and filing type. For each filing, show available presentation roles (from `--list-statements` logic at `Stocks.EDGARScraper/Services/Statements/StatementPrinter.cs:108-137`). The user clicks a specific role to see the detail report.

**Report detail view needs**: Company CIK + Submission ID + Root concept name (or concept ID) + taxonomy year. This fully identifies a single renderable statement.

---

## 4. Existing Patterns to Follow

- **Result pattern**: All service methods return `Result<T>`. Web API controllers should map `Result.IsFailure` to appropriate HTTP status codes (400, 404, 500).
- **Statement pattern**: Each new DB query gets its own class in `Database/Statements/`. Follows `QueryDbStmtBase<T>` for reads, inheriting ordinal caching.
- **No LINQ in production**: Use explicit loops. LINQ is acceptable in tests only.
- **No tuples**: Use record types for multi-value returns.
- **Explicit usings**: ImplicitUsings is disabled across the solution.
- **Singleton services**: `PostgresExecutor` and `DbmService` are singletons. Follow this for the Web API.
- **Naming**: Statements named `Get*Stmt`, `BulkInsert*Stmt`. DTOs named `*DTO`. Models are records.

---

## 5. Risks and Concerns

1. **No `GetSubmissionsByCompanyId`**: The current `GetSubmissions()` fetches ALL submissions globally. This will not scale for a Web API serving individual company pages. A filtered query is essential before building the API.

2. **No ticker master table**: Ticker-CIK mappings are only available via `price_imports`/`price_downloads` or filesystem JSON. A persistent, indexed `company_tickers` table is needed for search.

3. **StatementPrinter coupling**: The traversal logic is tightly coupled to `TextWriter`-based output. Extracting it requires careful refactoring to avoid breaking the existing CLI.

4. **Taxonomy tree size**: Each taxonomy year has ~20k concepts. Loading all concepts + presentations per API request could be slow. Consider caching taxonomy data per year in memory.

5. **Data point import limitation**: `--parse-bulk-xbrl-archive` currently hardcodes US-GAAP 2025 for concept matching (noted in ADR 0011). Data points for older filings may not have correct `taxonomy_concept_id` values.

6. **No company-specific data point query by concept**: `GetDataPointsForSubmission` returns all data points for a submission. For the report view this is correct (need all), but dashboard aggregates may need new queries.

---

## 6. Recommended Approach

### Phase 1: Data Layer Gaps
1. Add `company_tickers` migration and import from SEC JSON files
2. Add `GetSubmissionsByCompanyIdStmt` with proper indexes
3. Add unified search statement with `pg_trgm` for partial name matching
4. Add `GetDashboardStatsStmt` for aggregate statistics

### Phase 2: Web API Project
1. Create `Stocks.WebApi` project with ASP.NET minimal APIs or controllers
2. Reference `Stocks.Persistence`, `Stocks.DataModels`, `Stocks.Shared`
3. Extract statement traversal logic from `StatementPrinter` into a shared `StatementDataService`
4. Implement endpoints: search, company detail, statement rendering, dashboard stats
5. Add in-memory trie service (`IHostedService`) for typeahead, loaded at startup

### Phase 3: Angular Front-End
1. Scaffold Angular workspace with routing, lazy-loaded modules
2. Title bar with typeahead search component
3. Sidebar navigation (Dashboard, Search)
4. Dashboard view with summary cards
5. Search results page with paginated table
6. Company semi-detail view (filings + available statements)
7. Report detail view (rendered financial statement as tree/table)

---

## Metadata
### Status
success
### Dependencies
- `Stocks.Persistence/Database/IDbmService.cs` — interface for all new DB methods
- `Stocks.DataModels/` — shared models, must remain compatible
- ADR 0011 (multi-year taxonomy strategy) — taxonomy year resolution approach
- `docker-scripts/docker-compose.yml` — PostgreSQL and Elasticsearch infrastructure
### Open Questions
- Should the in-memory trie include ALL ~500k+ company names, or only companies with financial data (submissions)?
- Should the Web API host also serve the Angular static files (SPA fallback), or run separately?
- What dashboard statistics are most valuable to show initially? (Needs user input to prioritize)
- Should the detail report view support side-by-side comparison of multiple filing dates?
### Assumptions
- The Angular front-end will be a single-page application communicating with the Web API via REST/JSON
- The Web API and the existing gRPC DataService run as separate processes (separate ports)
- The existing `DbmService` singleton pattern is safe for concurrent Web API requests (confirmed: semaphore-limited to 20 concurrent statements)
- Initial dataset scale: ~500k companies, ~2M submissions, ~15 taxonomy years — fits comfortably in PostgreSQL without sharding
