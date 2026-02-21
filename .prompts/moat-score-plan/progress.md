# Progress

- [x] Checkpoint 1: Data Models
  - Files: MoatDerivedMetrics.cs, MoatYearMetrics.cs, MoatScoringResult.cs, CompanyMoatScoreSummary.cs, MoatScoresReportRequest.cs
  - Tests: MoatScoringModelTests.cs (5 tests)
  - Committed: yes

- [x] Checkpoint 2: Database Layer
  - Files: V017__AddCompanyMoatScoresTable.sql, BulkInsertCompanyMoatScoresStmt.cs, TruncateCompanyMoatScoresStmt.cs, GetCompanyMoatScoresStmt.cs, GetScoringDataPointsStmt.cs, GetAllScoringDataPointsStmt.cs, IDbmService.cs, DbmService.cs, DbmInMemoryService.cs, DbmInMemoryData.cs
  - Tests: GetMoatScoringDataPointsTests.cs (3 tests)
  - Committed: yes

- [x] Checkpoint 3: MoatScoringService — Concept Resolution and Derived Metrics
  - Files: MoatScoringService.cs (created), ScoringService.cs (MakeCheck→internal)
  - Tests: MoatScoringServiceTests.cs (16 tests)
  - Committed: yes

- [x] Checkpoint 4: MoatScoringService — Check Evaluation and Single-Company Scoring
  - Files: MoatScoringService.cs (EvaluateMoatChecks, ComputeScore), MoatScoringServiceTests.cs (+19 tests)
  - Tests: MoatScoringServiceTests.cs (35 total)
  - Committed: yes

- [x] Checkpoint 5: Batch Scoring and CLI Command
  - Files: MoatScoringService.cs (ComputeAllMoatScores), Program.cs (--compute-all-moat-scores), DbmInMemoryData.cs (GetCompanyMoatScores accessor)
  - Tests: BatchMoatScoringServiceTests.cs (3 tests)
  - Committed: yes

- [x] Checkpoint 6: Backend API Endpoints
  - Files: MoatScoringEndpoints.cs, MoatReportEndpoints.cs, WebApi/Program.cs (service registration + endpoint mapping)
  - Tests: none (thin delegation layer, verified by full suite — 267 tests passing)
  - Committed: yes

- [x] Checkpoint 7: Frontend — API Service and Moat Detail Page
  - Files: api.service.ts (Moat interfaces + 2 API methods), sparkline.utils.ts (reusable sparkline utility), moat-scoring.component.ts (detail page with scorecard, metrics, 6 trend charts, raw data)
  - Tests: ng build passes, 267 .NET tests passing
  - Committed: pending

- [x] Checkpoint 8: Frontend — Moat List Page and Navigation
  - Files: moat-scores-report.component.ts (list page with sortable columns, filters, pagination), app.routes.ts (+2 routes: moat-scores, company/:cik/moat-scoring), sidebar.component.ts (+Moat Scores nav entry)
  - Tests: ng build passes, 267 .NET tests passing
  - Committed: pending
