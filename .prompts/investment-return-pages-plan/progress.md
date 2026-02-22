# Progress

Pre-implementation base: 2ac0fe87042d8f0539bd2f1233703866182d1423

- [x] Checkpoint 1: Remove return1y from Backend
  - Files: CompanyScoreSummary.cs, CompanyMoatScoreSummary.cs, ScoresReportRequest.cs, MoatScoresReportRequest.cs, BulkInsertCompanyScoresStmt.cs, BulkInsertCompanyMoatScoresStmt.cs, GetCompanyScoresStmt.cs, GetCompanyMoatScoresStmt.cs, DbmInMemoryData.cs, ReportEndpoints.cs, MoatReportEndpoints.cs, Program.cs, ScoringService.cs, MoatScoringService.cs, V019__DropReturn1yFromScoreTables.sql, CompanyScoresReportTests.cs, BatchScoringServiceTests.cs, MoatScoringModelTests.cs
  - Tests: Deleted Return1yEnrichmentTests.cs, updated MoatScoringModelTests.cs, CompanyScoresReportTests.cs, BatchScoringServiceTests.cs
  - Committed: d219d66

- [x] Checkpoint 2: Remove Per-Company Investment Return Infrastructure
  - Files: Deleted InvestmentReturnEndpoints.cs, InvestmentReturnService.cs, InvestmentReturnResult.cs, GetPriceNearDateStmt.cs, GetLatestPriceByTickerStmt.cs, InvestmentReturnServiceTests.cs. Modified IDbmService.cs, DbmService.cs, DbmInMemoryService.cs, DbmInMemoryData.cs, Program.cs
  - Tests: Deleted InvestmentReturnServiceTests.cs
  - Committed: 96db4c4

- [x] Checkpoint 3: Remove return1y and Investment Return from Frontend
  - Files: api.service.ts, scores-report.component.ts, moat-scores-report.component.ts, scoring.component.ts, moat-scoring.component.ts, scoring.component.spec.ts
  - Tests: Updated scoring.component.spec.ts (removed investment return mocks and test)
  - Committed: 289c268

- [x] Checkpoint 4: New Backend: Return Computation Service and Report Endpoints
  - Files: CompanyScoreReturnSummary.cs, ReturnsReportRequest.cs, InvestmentReturnReportService.cs, GrahamReturnsEndpoints.cs, BuffettReturnsEndpoints.cs, Program.cs
  - Tests: InvestmentReturnReportServiceTests.cs (15 tests)
  - Committed: 96aaeab

- [x] Checkpoint 5: New Frontend: Report Components, Routes, Sidebar
  - Files: api.service.ts, graham-returns-report.component.ts, buffett-returns-report.component.ts, app.routes.ts, sidebar.component.ts
  - Tests: No new spec files (following existing report component pattern); all 42 existing frontend tests pass
  - Committed: 41a0998

## Finalization
- [x] Squash
- [x] Review-and-fix
- [x] Wrap-up
