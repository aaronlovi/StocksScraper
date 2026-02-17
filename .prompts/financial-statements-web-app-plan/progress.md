# Progress

- [x] Checkpoint 1: Company Tickers Table and Submissions-by-Company Query
  - Files: V010__AddCompanyTickers.sql, CompanyTicker.cs, BulkInsertCompanyTickersStmt.cs, GetCompanyTickersByCompanyIdStmt.cs, GetSubmissionsByCompanyIdStmt.cs, GetCompanyByCikStmt.cs, IDbmService.cs, DbmService.cs, DbmInMemoryService.cs, DbmInMemoryData.cs
  - Tests: CompanyTickersTests.cs (7 tests)
  - Committed: 064daba

- [x] Checkpoint 2: Unified Search with pg_trgm
  - Files: V011__AddTrigramSearch.sql, CompanySearchResult.cs, SearchCompaniesStmt.cs, IDbmService.cs, DbmService.cs, DbmInMemoryService.cs, DbmInMemoryData.cs
  - Tests: CompanySearchTests.cs (5 tests)
  - Committed: 35ea331

- [x] Checkpoint 3: Dashboard Statistics Statement
  - Files: DashboardStats.cs, GetDashboardStatsStmt.cs, IDbmService.cs, DbmService.cs, DbmInMemoryService.cs, DbmInMemoryData.cs
  - Tests: DashboardStatsTests.cs (3 tests)
  - Committed: 30aa22e

- [x] Checkpoint 4: WebApi Project Scaffold
  - Files: Stocks.WebApi.csproj, Program.cs, Stocks.WebApi.Tests.csproj, HealthCheckTests.cs, EDGARScraper.sln
  - Tests: HealthCheckTests.cs (2 tests)
  - Committed: 47beaa7

- [x] Checkpoint 5: StatementDataService Extraction
  - Files: HierarchyNode.cs (moved), StatementData.cs, StatementListItem.cs, StatementDataService.cs, StatementPrinter.cs (refactored), deleted TraverseContext.cs
  - Tests: StatementDataServiceTests.cs (4 tests), all 11 StatementPrinterTests still pass
  - Committed: 99eaf69

- [x] Checkpoint 6: Core API Endpoints
  - Files: ResultExtensions.cs, CompanyEndpoints.cs, SubmissionEndpoints.cs, SearchEndpoints.cs, DashboardEndpoints.cs, Program.cs
  - Tests: CompanyEndpointsTests.cs (2), SubmissionEndpointsTests.cs (2), SearchEndpointsTests.cs (3), DashboardEndpointsTests.cs (1)
  - Committed: 3822147

- [x] Checkpoint 7: Statement Rendering and Typeahead Endpoints
  - Files: StatementEndpoints.cs, TypeaheadEndpoints.cs, TypeaheadTrieService.cs, Program.cs, IDbmService.cs, DbmService.cs, DbmInMemoryService.cs, DbmInMemoryData.cs
  - Tests: StatementEndpointsTests.cs (3), TypeaheadTests.cs (3)
  - Committed: (pending)

- [x] Checkpoint 8: Angular Workspace and Layout Shell
  - Files: Angular workspace (frontend/stocks-frontend/), app.ts, app.routes.ts, app.config.ts, sidebar.component.ts, titlebar.component.ts, api.service.ts, proxy.conf.json, placeholder components (dashboard, search, company, report)
  - Tests: app.spec.ts (4), sidebar.component.spec.ts (3), titlebar.component.spec.ts (3), api.service.spec.ts (3)
  - Committed: (pending)

- [x] Checkpoint 9: Dashboard and Search Views
  - Files: dashboard.component.ts, search.component.ts, titlebar.component.ts (updated with typeahead)
  - Tests: dashboard.component.spec.ts (3), search.component.spec.ts (3), titlebar.component.spec.ts (4)
  - Committed: (pending)

- [x] Checkpoint 10: Company Semi-Detail View
  - Files: api.service.ts (updated), company.component.ts, company.component.spec.ts
  - Tests: company.component.spec.ts (5 tests)
  - Committed: bd3f12c

- [ ] Checkpoint 11: Report Detail View
  - (pending)
