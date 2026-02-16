# Plan: Financial Statements Web Application

## Table of Contents

- [1. Overview](#1-overview)
- [2. Phase 1 — Data Layer Gaps](#2-phase-1--data-layer-gaps)
  - [2.1. Checkpoint 1 — Company Tickers Table and Submissions-by-Company Query](#21-checkpoint-1--company-tickers-table-and-submissions-by-company-query)
  - [2.2. Checkpoint 2 — Unified Search with pg_trgm](#22-checkpoint-2--unified-search-with-pg_trgm)
  - [2.3. Checkpoint 3 — Dashboard Statistics Statement](#23-checkpoint-3--dashboard-statistics-statement)
- [3. Phase 2 — Web API](#3-phase-2--web-api)
  - [3.1. Checkpoint 4 — WebApi Project Scaffold](#31-checkpoint-4--webapi-project-scaffold)
  - [3.2. Checkpoint 5 — StatementDataService Extraction](#32-checkpoint-5--statementdataservice-extraction)
  - [3.3. Checkpoint 6 — Core API Endpoints](#33-checkpoint-6--core-api-endpoints)
  - [3.4. Checkpoint 7 — Statement Rendering and Typeahead Endpoints](#34-checkpoint-7--statement-rendering-and-typeahead-endpoints)
- [4. Phase 3 — Angular Front-End](#4-phase-3--angular-front-end)
  - [4.1. Checkpoint 8 — Angular Workspace and Layout Shell](#41-checkpoint-8--angular-workspace-and-layout-shell)
  - [4.2. Checkpoint 9 — Dashboard and Search Views](#42-checkpoint-9--dashboard-and-search-views)
  - [4.3. Checkpoint 10 — Company Semi-Detail View](#43-checkpoint-10--company-semi-detail-view)
  - [4.4. Checkpoint 11 — Report Detail View](#44-checkpoint-11--report-detail-view)

---

## 1. Overview

Build a financial statements web application in three phases:

1. **Data Layer Gaps** — Add missing database tables, indexes, queries, and `IDbmService` methods
2. **Web API** — Create `Stocks.WebApi` with REST/JSON endpoints, extract statement rendering logic
3. **Angular Front-End** — Build the SPA with dashboard, search, company detail, and report views

Each checkpoint produces compilable, tested code. Tests are written alongside implementation.

---

## 2. Phase 1 — Data Layer Gaps

### 2.1. Checkpoint 1 — Company Tickers Table and Submissions-by-Company Query

Two data layer gaps addressed together: persisting CIK-to-ticker mappings and querying submissions for a specific company.

#### Build

**Migration: `dotnet/Stocks.Persistence/Database/Migrations/V010__AddCompanyTickers.sql`**
- Create `company_tickers` table: `company_id BIGINT NOT NULL REFERENCES companies(company_id)`, `ticker VARCHAR(20) NOT NULL`, `exchange VARCHAR(50)`, `PRIMARY KEY (company_id, ticker)`
- Add index: `idx_company_tickers_ticker ON company_tickers(ticker)`
- Add index: `idx_submissions_company_id ON submissions(company_id)` (for `GetSubmissionsByCompanyIdStmt`)

**Model: `dotnet/Stocks.DataModels/CompanyTicker.cs`**
- Record: `CompanyTicker(ulong CompanyId, string Ticker, string? Exchange)`

**Statements:**
- `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyTickersStmt.cs` — bulk insert `List<CompanyTicker>` into `company_tickers`, using `ON CONFLICT (company_id, ticker) DO UPDATE SET exchange = EXCLUDED.exchange`
- `dotnet/Stocks.Persistence/Database/Statements/GetCompanyTickersByCompanyIdStmt.cs` — `SELECT ticker, exchange FROM company_tickers WHERE company_id = @company_id`
- `dotnet/Stocks.Persistence/Database/Statements/GetSubmissionsByCompanyIdStmt.cs` — `SELECT * FROM submissions WHERE company_id = @company_id ORDER BY report_date DESC`

**Statement: `dotnet/Stocks.Persistence/Database/Statements/GetCompanyByCikStmt.cs`**
- `SELECT * FROM companies WHERE cik = @cik LIMIT 1`
- Returns a single `Company` or failure if not found

**IDbmService additions** (`dotnet/Stocks.Persistence/Database/IDbmService.cs`):
- `Task<Result> BulkInsertCompanyTickers(List<CompanyTicker> tickers, CancellationToken ct)`
- `Task<Result<IReadOnlyCollection<CompanyTicker>>> GetCompanyTickersByCompanyId(ulong companyId, CancellationToken ct)`
- `Task<Result<IReadOnlyCollection<Submission>>> GetSubmissionsByCompanyId(ulong companyId, CancellationToken ct)`
- `Task<Result<Company>> GetCompanyByCik(string cik, CancellationToken ct)`

**DbmService** (`dotnet/Stocks.Persistence/Database/DbmService.cs`): Implement the four new methods using the new statement classes.

**DbmInMemoryService** (`dotnet/Stocks.Persistence/Database/DbmInMemoryService.cs`):
- Implement the four new methods against `DbmInMemoryData`
- Also implement prerequisite methods that currently throw `NotSupportedException` but are needed for test data setup: `BulkInsertCompanies`, `BulkInsertCompanyNames`, `BulkInsertSubmissions`, `GetCompanyById` (these store/retrieve from in-memory lists in `DbmInMemoryData`)

**DbmInMemoryData** (`dotnet/Stocks.Persistence/Database/DbmInMemoryData.cs`):
- Add `List<CompanyTicker> CompanyTickers` backing field
- Add `List<Company> Companies`, `List<CompanyName> CompanyNames`, `List<Submission> Submissions` backing fields (if not already present)

#### Test

**`dotnet/Stocks.EDGARScraper.Tests/CompanyTickersTests.cs`** (new file):
- `BulkInsertCompanyTickers_InsertsAndRetrievesByCompanyId` — insert tickers for two companies, retrieve by one company_id, verify correct tickers returned
- `BulkInsertCompanyTickers_UpsertUpdatesExchange` — insert a ticker, re-insert with different exchange, verify exchange updated
- `GetCompanyTickersByCompanyId_ReturnsEmptyForUnknownCompany` — query non-existent company_id, verify empty result
- `GetSubmissionsByCompanyId_ReturnsDescByReportDate` — insert submissions for a company, verify returned in descending report_date order
- `GetSubmissionsByCompanyId_ReturnsEmptyForUnknownCompany` — query non-existent company_id, verify empty result
- `GetCompanyByCik_Found_ReturnsCompany` — insert a company, look up by CIK, verify match
- `GetCompanyByCik_NotFound_ReturnsFailure` — look up non-existent CIK, verify `Result.IsFailure`

#### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

### 2.2. Checkpoint 2 — Unified Search with pg_trgm

Add a PostgreSQL trigram-based search statement that queries across company names and tickers for the paginated search results page.

#### Build

**Migration: `dotnet/Stocks.Persistence/Database/Migrations/V011__AddTrigramSearch.sql`**
- `CREATE EXTENSION IF NOT EXISTS pg_trgm`
- `CREATE INDEX idx_company_names_name_trgm ON company_names USING gin (name gin_trgm_ops)`
- `CREATE INDEX idx_company_tickers_ticker_trgm ON company_tickers USING gin (ticker gin_trgm_ops)`

**Search result record: `dotnet/Stocks.DataModels/CompanySearchResult.cs`**
- Record: `CompanySearchResult(ulong CompanyId, string Cik, string CompanyName, string? Ticker, string? Exchange)`

**Statement: `dotnet/Stocks.Persistence/Database/Statements/SearchCompaniesStmt.cs`**
- Paginated search using `pg_trgm` similarity:
  ```sql
  WITH matches AS (
    SELECT DISTINCT c.company_id, c.cik,
      cn.name AS company_name,
      ct.ticker, ct.exchange,
      GREATEST(
        COALESCE(similarity(cn.name, @query), 0),
        COALESCE(similarity(ct.ticker, @query), 0),
        CASE WHEN c.cik = @query THEN 1.0 ELSE 0 END
      ) AS rank
    FROM companies c
    JOIN company_names cn ON cn.company_id = c.company_id
    LEFT JOIN company_tickers ct ON ct.company_id = c.company_id
    WHERE cn.name % @query OR ct.ticker % @query OR c.cik = @query
  )
  SELECT *, COUNT(*) OVER() AS total_count
  FROM matches
  ORDER BY rank DESC, company_name ASC
  LIMIT @limit OFFSET @offset
  ```
- Returns `PagedResults<CompanySearchResult>` using the existing `PaginationRequest`/`PaginationResponse` pattern from `GetPagedCompaniesByDataSourceStmt.cs`

**IDbmService addition**:
- `Task<Result<PagedResults<CompanySearchResult>>> SearchCompanies(string query, PaginationRequest pagination, CancellationToken ct)`

**DbmService**: Implement using `SearchCompaniesStmt`.

**DbmInMemoryService**: Implement with explicit loop search over `DbmInMemoryData` companies + tickers + names (case-insensitive substring match, no trigram in memory).

#### Test

**`dotnet/Stocks.EDGARScraper.Tests/CompanySearchTests.cs`** (new file):
- `SearchCompanies_ByName_ReturnsMatchingCompanies` — insert companies with names, search by partial name, verify results
- `SearchCompanies_ByTicker_ReturnsMatchingCompanies` — insert companies with tickers, search by ticker, verify results
- `SearchCompanies_ByCik_ReturnsExactMatch` — insert company with CIK, search by exact CIK, verify match
- `SearchCompanies_NoMatch_ReturnsEmptyPage` — search for non-existent term, verify empty results with zero total
- `SearchCompanies_Pagination_RespectsPageSize` — insert 10 companies, search with pageSize=3, verify 3 results and correct total count

#### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

### 2.3. Checkpoint 3 — Dashboard Statistics Statement

Add a single statement that returns aggregate statistics for the dashboard in one database round trip.

#### Build

**Record: `dotnet/Stocks.DataModels/DashboardStats.cs`**
- Record:
  ```csharp
  DashboardStats(
    long TotalCompanies,
    long TotalSubmissions,
    long TotalDataPoints,
    DateOnly? EarliestFilingDate,
    DateOnly? LatestFilingDate,
    long CompaniesWithPriceData,
    IReadOnlyDictionary<string, long> SubmissionsByFilingType
  )
  ```

**Statement: `dotnet/Stocks.Persistence/Database/Statements/GetDashboardStatsStmt.cs`**
- Uses CTEs for each aggregate:
  ```sql
  WITH company_count AS (SELECT COUNT(*) AS cnt FROM companies),
       submission_count AS (SELECT COUNT(*) AS cnt FROM submissions),
       datapoint_count AS (SELECT COUNT(*) AS cnt FROM data_points),
       filing_dates AS (SELECT MIN(report_date) AS earliest, MAX(report_date) AS latest FROM submissions),
       price_companies AS (SELECT COUNT(DISTINCT ticker) AS cnt FROM price_imports),
       submissions_by_type AS (SELECT filing_type, COUNT(*) AS cnt FROM submissions GROUP BY filing_type)
  SELECT ... FROM company_count, submission_count, datapoint_count, filing_dates, price_companies, submissions_by_type
  ```
- Returns `DashboardStats` record (maps `filing_type` int values to `FilingType` enum names for the dictionary)

**IDbmService addition**:
- `Task<Result<DashboardStats>> GetDashboardStats(CancellationToken ct)`

**DbmService**: Implement using `GetDashboardStatsStmt`.

**DbmInMemoryService**: Implement by iterating over in-memory data collections to compute counts. Also implement `BulkInsertDataPoints` (if not already done) so tests can populate data points for verification. Add `List<DataPoint> DataPoints` to `DbmInMemoryData` if not already present.

#### Test

**`dotnet/Stocks.EDGARScraper.Tests/DashboardStatsTests.cs`** (new file):
- `GetDashboardStats_EmptyDatabase_ReturnsZeroCounts` — empty in-memory data, verify all counts are zero, dates are null
- `GetDashboardStats_WithData_ReturnsCorrectCounts` — populate in-memory data with known companies/submissions/data points, verify all counts match
- `GetDashboardStats_SubmissionsByFilingType_GroupsCorrectly` — insert submissions with different filing types, verify the dictionary has correct counts per type

#### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

## 3. Phase 2 — Web API

### 3.1. Checkpoint 4 — WebApi Project Scaffold

Create the `Stocks.WebApi` project with ASP.NET minimal APIs, DI wiring, and a health check endpoint.

#### Build

**Project: `dotnet/Stocks.WebApi/Stocks.WebApi.csproj`**
- Target: `net8.0`
- SDK: `Microsoft.NET.Sdk.Web`
- References: `Stocks.Persistence`, `Stocks.DataModels`, `Stocks.Shared`
- Properties: `<Nullable>enable</Nullable>`, `<ImplicitUsings>disable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`

**Entry point: `dotnet/Stocks.WebApi/Program.cs`**
- Register `PostgresExecutor` as singleton (same pattern as `Stocks.DataService/Program.cs:57`)
- Register `DbmService` as singleton via `IDbmService` (same pattern as `Stocks.DataService/Program.cs:62`)
- Add CORS policy allowing Angular dev server origin (`http://localhost:4200`)
- Map a health check endpoint: `GET /api/health` → returns `{ "status": "healthy" }`
- Configure Kestrel for HTTP/1.1 + HTTP/2 (not HTTP/2-only like the gRPC service)

**Solution update: `dotnet/EDGARScraper.sln`**
- Add `Stocks.WebApi` project

**Test project: `dotnet/Stocks.WebApi.Tests/Stocks.WebApi.Tests.csproj`**
- Target: `net8.0`
- References: `Stocks.WebApi`, `Stocks.Persistence`, `Stocks.DataModels`, `Stocks.Shared`
- Packages: `xunit`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing`, `Moq`

**Solution update**: Add `Stocks.WebApi.Tests` project to `EDGARScraper.sln`

#### Test

**`dotnet/Stocks.WebApi.Tests/HealthCheckTests.cs`** (new file):
- Use `WebApplicationFactory<Program>` with `IDbmService` replaced by `DbmInMemoryService`
- `HealthEndpoint_ReturnsOk` — `GET /api/health` returns 200 with `"healthy"` in response body
- `CorsHeaders_AllowAngularOrigin` — request with `Origin: http://localhost:4200`, verify CORS headers present

#### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

### 3.2. Checkpoint 5 — StatementDataService Extraction

Extract the hierarchy traversal and data assembly logic from `StatementPrinter` into a reusable `StatementDataService` that returns structured data instead of writing to streams.

#### Build

**Record: `dotnet/Stocks.DataModels/StatementData.cs`**
- Record:
  ```csharp
  StatementData(
    List<HierarchyNode> Hierarchy,
    Dictionary<long, DataPoint> DataPointMap,
    HashSet<long> IncludedConceptIds,
    Dictionary<long, List<HierarchyNode>> ChildrenMap,
    List<HierarchyNode> RootNodes
  )
  ```

**Record: `dotnet/Stocks.DataModels/StatementListItem.cs`**
- Record: `StatementListItem(string RoleName, string RootConceptName, string RootLabel)` — for `--list-statements` equivalent

**Service: `dotnet/Stocks.Persistence/Services/StatementDataService.cs`**
- Constructor: `StatementDataService(IDbmService dbmService)`
- Method: `Task<Result<StatementData>> GetStatementData(ulong companyId, ulong submissionId, string concept, int taxonomyTypeId, int maxDepth, string? roleName, CancellationToken ct)`
  - Extracts logic from `StatementPrinter` methods: `BuildParentToChildrenMap`, `TraverseConceptTree`, `BuildChildrenMap`, `HasValueOrChildValue`
  - Returns the assembled `StatementData` without any formatting
- Method: `Task<Result<IReadOnlyCollection<StatementListItem>>> ListStatements(ulong companyId, ulong submissionId, int taxonomyTypeId, CancellationToken ct)`
  - Extracts the `--list-statements` logic from `StatementPrinter.ListStatements` (lines 108-137)
  - Returns list of available presentation roles for a given submission

**Refactor `StatementPrinter`** (`dotnet/Stocks.EDGARScraper/Services/Statements/StatementPrinter.cs`):
- Add dependency on `StatementDataService` (constructor injection)
- Delegate data assembly to `StatementDataService.GetStatementData()` and `ListStatements()`
- Keep formatting logic (CSV, HTML, JSON output) in `StatementPrinter`
- `StatementPrinter` becomes a thin formatting layer over `StatementDataService`

**Move shared model**: `HierarchyNode.cs` from `Stocks.EDGARScraper/Models/Statements/` to `Stocks.DataModels/` so it can be referenced by both `Stocks.Persistence` and `Stocks.EDGARScraper`. `TraverseContext.cs` stays internal to `StatementDataService` as a private implementation detail of the traversal algorithm.

#### Test

**`dotnet/Stocks.EDGARScraper.Tests/StatementDataServiceTests.cs`** (new file, uses Moq to mock `IDbmService` — same pattern as existing `StatementPrinterTests`):
- `GetStatementData_ReturnsHierarchyWithDataPoints` — mock taxonomy concepts, presentations, data points via `IDbmService`; verify returned `StatementData` has correct hierarchy and data point mapping
- `GetStatementData_RespectsMaxDepth` — set up deep hierarchy, pass `maxDepth=2`, verify no nodes at depth > 2
- `GetStatementData_UnknownConcept_ReturnsFailure` — pass a concept name that doesn't exist, verify `Result.IsFailure`
- `ListStatements_ReturnsAvailableRoles` — set up presentations with different role names, verify returned list matches
- **Existing `StatementPrinterTests`** — all 11 existing tests must continue passing (StatementPrinter now delegates to StatementDataService internally)

#### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

### 3.3. Checkpoint 6 — Core API Endpoints

Add REST endpoints for company lookup, submissions, search, and dashboard statistics.

#### Build

**Controllers or endpoint group classes in `dotnet/Stocks.WebApi/`:**

**`Endpoints/CompanyEndpoints.cs`**:
- `GET /api/companies/{cik}` → returns company by CIK via `GetCompanyByCik` (added in Checkpoint 1), then fetches tickers via `GetCompanyTickersByCompanyId`
  - Response: `{ companyId, cik, name, tickers: [{ ticker, exchange }] }`
  - 404 if not found

**`Endpoints/SubmissionEndpoints.cs`**:
- `GET /api/companies/{cik}/submissions` → returns all submissions for a company via `GetSubmissionsByCompanyId` (added in Checkpoint 1)
  - No pagination needed — a single company has at most ~100 filings
  - Response: `{ items: [{ submissionId, filingType, filingCategory, reportDate, filingReference }] }`

**`Endpoints/SearchEndpoints.cs`**:
- `GET /api/search?q=...&page=1&pageSize=25` → paginated search via `SearchCompanies`
  - Response: `{ items: [...], pagination: { ... } }`

**`Endpoints/DashboardEndpoints.cs`**:
- `GET /api/dashboard/stats` → returns `DashboardStats` as JSON

**Middleware: `dotnet/Stocks.WebApi/Middleware/ResultExtensions.cs`**
- Extension method `ToActionResult<T>(this Result<T> result)` mapping `Result.IsFailure` → appropriate HTTP status (404 for not found, 500 for internal errors)

#### Test

**`dotnet/Stocks.WebApi.Tests/CompanyEndpointsTests.cs`**:
- `GetCompanyByCik_Found_Returns200WithCompanyData`
- `GetCompanyByCik_NotFound_Returns404`

**`dotnet/Stocks.WebApi.Tests/SubmissionEndpointsTests.cs`**:
- `GetSubmissions_ReturnsAllSubmissionsForCompany`
- `GetSubmissions_UnknownCik_Returns404`

**`dotnet/Stocks.WebApi.Tests/SearchEndpointsTests.cs`**:
- `Search_ByName_ReturnsMatches`
- `Search_NoResults_ReturnsEmptyPage`
- `Search_Pagination_RespectsPageSize`

**`dotnet/Stocks.WebApi.Tests/DashboardEndpointsTests.cs`**:
- `GetDashboardStats_ReturnsStats`

All tests use `WebApplicationFactory` with `DbmInMemoryService` injected.

#### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

### 3.4. Checkpoint 7 — Statement Rendering and Typeahead Endpoints

Add the statement rendering endpoint and an in-memory trie service for typeahead search.

#### Build

**`Endpoints/StatementEndpoints.cs`** (`dotnet/Stocks.WebApi/`):
- `GET /api/companies/{cik}/submissions/{submissionId}/statements` → list available statements (presentation roles) via `StatementDataService.ListStatements`
  - Response: `[{ roleName, rootConceptName, rootLabel }]`
- `GET /api/companies/{cik}/submissions/{submissionId}/statements/{concept}` → render a statement as JSON tree via `StatementDataService.GetStatementData`
  - Query params: `?maxDepth=10&taxonomyYear=2024&roleName=...`
  - Taxonomy year defaults to submission's `ReportDate.Year` (resolve by calling `GetSubmissionsByCompanyId` and finding the matching `submissionId` in the result list)
  - Response: JSON tree (same structure as `StatementPrinter.BuildJsonTree`)

**Trie service: `dotnet/Stocks.WebApi/Services/TypeaheadTrieService.cs`**
- Implements `IHostedService` — loads data on startup
- Builds an in-memory trie from companies (CIK + name) and tickers
- Method: `List<TypeaheadResult> Search(string prefix, int maxResults = 10)`
- Record: `TypeaheadResult(string Text, string Type, string Cik)` where `Type` is `"company"` or `"ticker"`

**`Endpoints/TypeaheadEndpoints.cs`** (`dotnet/Stocks.WebApi/`):
- `GET /api/typeahead?q=...` → returns top 10 matches from trie
  - Response: `[{ text, type, cik }]`

**Register `StatementDataService`** as singleton in `Program.cs`.

#### Test

**`dotnet/Stocks.WebApi.Tests/StatementEndpointsTests.cs`**:
- `ListStatements_ReturnsAvailableRoles`
- `GetStatement_ReturnsJsonTree`
- `GetStatement_InvalidConcept_Returns404`

**`dotnet/Stocks.WebApi.Tests/TypeaheadTests.cs`**:
- `TypeaheadTrieService_Search_ReturnsPrefixMatches` — unit test the trie directly (no HTTP)
- `TypeaheadTrieService_Search_LimitsResults` — verify max results cap
- `TypeaheadEndpoint_ReturnsMatches` — integration test via `WebApplicationFactory`

#### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

## 4. Phase 3 — Angular Front-End

### 4.1. Checkpoint 8 — Angular Workspace and Layout Shell

Scaffold the Angular workspace and implement the application shell with sidebar navigation and title bar.

#### Build

**Workspace: `frontend/`**
- Install Angular CLI project-locally: `npm init -y && npm install @angular/cli@21`
- Scaffold: `npx ng new stocks-frontend --routing --style=scss --skip-tests=false`
- Angular 21 defaults: standalone components, Vitest as test runner
- Configure `proxy.conf.json` to proxy `/api/*` to `http://localhost:5000` (Web API)
- Update `angular.json` to use the proxy config in `serve`

**App layout:**
- `app.component.ts` — shell with sidebar + title bar + `<router-outlet>`
- `core/layout/sidebar/sidebar.component.ts` — navigation links: Dashboard (`/dashboard`), Search (`/search`)
- `core/layout/titlebar/titlebar.component.ts` — app title + typeahead search input
- `core/services/api.service.ts` — Angular `HttpClient` wrapper for `/api/*` calls with typed responses

**Routing:**
- `/dashboard` — lazy-loaded standalone component via `loadComponent`
- `/search` — lazy-loaded standalone component via `loadComponent`
- `/company/:cik` — lazy-loaded standalone component via `loadComponent`
- `/company/:cik/report/:submissionId/:concept` — child route under company
- Default redirect: `''` → `/dashboard`

**Placeholder components**: Each lazy-loaded route gets a placeholder standalone component displaying the route name (to be implemented in subsequent checkpoints).

#### Test

- `sidebar.component.spec.ts` — renders navigation links, active link highlighted for current route
- `titlebar.component.spec.ts` — renders search input, emits search event on enter
- `api.service.spec.ts` — mocks `HttpClient`, verifies correct URL construction for `getCompany`, `searchCompanies`, `getDashboardStats`
- `app.component.spec.ts` — renders sidebar and title bar, contains router outlet

#### Verify

```bash
cd frontend && npx ng test && cd ..
```

---

### 4.2. Checkpoint 9 — Dashboard and Search Views

Implement the dashboard view with summary statistics cards and the search results page with a paginated table.

#### Build

**Dashboard feature: `frontend/src/app/features/dashboard/`**
- `dashboard.component.ts` — calls `ApiService.getDashboardStats()` on init
- `dashboard.component.html` — summary cards: total companies, total filings, filing date range, companies with price data, data points count
- `models/dashboard-stats.model.ts` — TypeScript interface matching the `DashboardStats` API response

**Search feature: `frontend/src/app/features/search/`**
- `search.component.ts` — reads `?q=` from query params, calls `ApiService.searchCompanies(query, page, pageSize)` on init and on pagination change
- `search.component.html` — search input (pre-filled from query), results table (Company Name, CIK, Ticker, Exchange), pagination controls
- `models/search-result.model.ts` — TypeScript interface matching `CompanySearchResult`
- `models/paginated-response.model.ts` — generic `PaginatedResponse<T>` with `items` and `pagination`

**Title bar update**: Wire the typeahead input to call `ApiService.getTypeahead(query)` on keyup (debounced 300ms). Show dropdown with results. On selection → navigate to `/company/:cik`. On enter → navigate to `/search?q=...`.

#### Test

- `dashboard.component.spec.ts` — mocks `ApiService`, verifies stats cards display correct values
- `search.component.spec.ts` — mocks `ApiService`, verifies table renders rows, pagination emits correct page numbers, empty results shows "no results" message
- `titlebar.component.spec.ts` (update) — mocks typeahead API, verifies dropdown shows results, selection navigates to company route

#### Verify

```bash
cd frontend && npx ng test && cd ..
```

---

### 4.3. Checkpoint 10 — Company Semi-Detail View

Implement the company detail page showing company info, tickers, and filings grouped by date with available statement types.

#### Build

**Company feature: `frontend/src/app/features/company/`**
- `company-detail.component.ts` — reads `:cik` from route, calls:
  1. `ApiService.getCompany(cik)` — company info + tickers
  2. `ApiService.getSubmissions(cik)` — all submissions (no pagination — small per-company list)
- `company-detail.component.html`:
  - Header: company name, CIK, tickers (as chips/badges)
  - Submissions table: Report Date, Filing Type, Filing Category
  - Each row expandable or clickable to load available statements
  - On row expand: calls `ApiService.listStatements(cik, submissionId)` → shows list of presentation roles
  - Each statement link navigates to `/company/:cik/report/:submissionId/:concept`

**Models:**
- `models/company.model.ts` — company with tickers
- `models/submission.model.ts` — submission fields
- `models/statement-list-item.model.ts` — `{ roleName, rootConceptName, rootLabel }`

#### Test

- `company-detail.component.spec.ts`:
  - Renders company name and CIK from mocked API response
  - Renders ticker badges
  - Renders submissions table with correct columns
  - Expanding a row calls `listStatements` API and displays statement links
  - Statement link navigates to correct report route

#### Verify

```bash
cd frontend && npx ng test && cd ..
```

---

### 4.4. Checkpoint 11 — Report Detail View

Implement the financial statement rendering view that displays the statement as a hierarchical tree/table.

#### Build

**Report component: `frontend/src/app/features/company/report/`**
- `report-detail.component.ts` — reads `:cik`, `:submissionId`, `:concept` from route, calls `ApiService.getStatement(cik, submissionId, concept, taxonomyYear?)`
- `report-detail.component.html`:
  - Breadcrumb: Company Name > Filing Date > Statement Name
  - Tree table: indented rows showing concept label, value (formatted as currency/number)
  - Expand/collapse for tree nodes with children
  - Optional taxonomy year selector dropdown

**Shared:**
- `models/statement-tree-node.model.ts` — recursive: `{ conceptName, label, value, children: StatementTreeNode[] }`
- `shared/components/tree-table/tree-table.component.ts` — reusable tree-table component for rendering hierarchical financial data
  - Input: `StatementTreeNode[]` root nodes
  - Renders indented rows with expand/collapse toggles
  - Formats numeric values with commas and optional decimal places

#### Test

- `report-detail.component.spec.ts`:
  - Renders breadcrumb with company name and statement name
  - Renders tree table from mocked statement data
  - Expand/collapse toggles visibility of child rows
  - Taxonomy year selector changes displayed data

- `tree-table.component.spec.ts`:
  - Renders root nodes as top-level rows
  - Indents child nodes
  - Toggling a parent row shows/hides children
  - Formats numeric values correctly

#### Verify

```bash
cd frontend && npx ng test && cd ..
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

## Metadata

### Status
success

### Dependencies
- `dotnet/Stocks.Persistence/Database/IDbmService.cs` — all new methods added here
- `dotnet/Stocks.DataModels/` — new models: `CompanyTicker`, `CompanySearchResult`, `DashboardStats`, `StatementData`, `StatementListItem`
- `dotnet/Stocks.EDGARScraper/Services/Statements/StatementPrinter.cs` — refactored to delegate to `StatementDataService`
- ADR 0011 — taxonomy year resolution strategy
- `docker-scripts/docker-compose.yml` — PostgreSQL must have `pg_trgm` extension available

### Open Questions
- Should the in-memory trie include all ~500k company names, or only companies with financial data (submissions)? (Defaulting to all companies for completeness; can filter later if memory is a concern.)
- Should the Web API host also serve the Angular static files via SPA fallback, or run the Angular dev server separately? (Plan assumes separate processes during development; production deployment is out of scope.)
- Should the report detail view support side-by-side comparison of multiple filing dates? (Deferred to a future phase.)

### Assumptions
- Angular 21 CLI installed project-locally via `npm install @angular/cli@21` (not global), invoked via `npx ng`
- Standalone components throughout (Angular 21 default) — no NgModules
- Vitest is the test runner (Angular 21 default) — no Karma/Jasmine
- The Web API runs on port 5000 during development
- `pg_trgm` extension is available in the PostgreSQL instance from `docker-compose.yml`
- `DbmInMemoryService` in-memory search uses case-insensitive substring matching (not trigram) since trigram is a PostgreSQL extension
- The existing 18 tests in `Stocks.EDGARScraper.Tests` continue passing through all checkpoints
