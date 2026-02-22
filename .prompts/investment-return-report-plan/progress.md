# Progress

- [x] Checkpoint 1: Backend — Detail Page Investment Return Service + Endpoint
  - Files: InvestmentReturnResult.cs, GetPriceNearDateStmt.cs, GetLatestPriceByTickerStmt.cs, InvestmentReturnService.cs, InvestmentReturnEndpoints.cs, IDbmService.cs, DbmService.cs, DbmInMemoryService.cs, DbmInMemoryData.cs, Enums.cs, Program.cs
  - Tests: InvestmentReturnServiceTests.cs (7 tests)
  - Committed: yes/e73971b

- [x] Checkpoint 2: Frontend — Detail Page Investment Return Section
  - Files: api.service.ts, scoring.component.ts, moat-scoring.component.ts
  - Tests: Angular build verification
  - Committed: yes/12c7356

- [x] Checkpoint 3: Backend — List Page Pre-Computed 1-Year Return
  - Files: V018__AddReturn1yToScoreTables.sql, GetAllPricesNearDateStmt.cs, CompanyScoreSummary.cs, CompanyMoatScoreSummary.cs, ScoresReportRequest.cs, MoatScoresReportRequest.cs, BulkInsertCompanyScoresStmt.cs, BulkInsertCompanyMoatScoresStmt.cs, GetCompanyScoresStmt.cs, GetCompanyMoatScoresStmt.cs, IDbmService.cs, DbmService.cs, DbmInMemoryService.cs, DbmInMemoryData.cs, ReportEndpoints.cs, MoatReportEndpoints.cs, Program.cs, ScoringService.cs, MoatScoringService.cs
  - Tests: Return1yEnrichmentTests.cs (6 tests)
  - Committed: yes/5e05478

- [x] Checkpoint 4: Frontend — List Page Return Column
  - Files: api.service.ts, scores-report.component.ts, moat-scores-report.component.ts
  - Tests: Angular production build verification, .NET tests (291 passing)
  - Committed: yes/b49727d
