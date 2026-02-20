# Progress

- [x] Checkpoint 1: Migration and Data Models
  - Files: V013__AddCompanyScoresTable.sql, CompanyScoreSummary.cs, ScoresReportRequest.cs, LatestPrice.cs, BatchScoringConceptValue.cs
  - Tests: None (configuration checkpoint)
  - Committed: c9de099

- [x] Checkpoint 2: Batch Data Fetch Statements
  - Files: GetAllScoringDataPointsStmt.cs, GetAllLatestPricesStmt.cs, IDbmService.cs, DbmService.cs, DbmInMemoryService.cs, DbmInMemoryData.cs, BatchScoringDataFetchTests.cs
  - Tests: 9 new tests (6 for GetAllScoringDataPoints, 3 for GetAllLatestPrices)
  - Committed: 3707ba8

- [x] Checkpoint 3: Score Persistence, Batch Scoring Service, and CLI Command
  - Files: TruncateCompanyScoresStmt.cs, BulkInsertCompanyScoresStmt.cs, IDbmService.cs, DbmService.cs, DbmInMemoryService.cs, DbmInMemoryData.cs, ScoringService.cs (ComputeAllScores), Program.cs (--compute-all-scores), BatchScoringServiceTests.cs
  - Tests: 6 new tests (5 for ComputeAllScores, 1 for truncate+insert round-trip)
  - Committed: pending

- [ ] Checkpoint 4: Report Query Statement and API Endpoint
  - (pending)

- [ ] Checkpoint 5: Angular Frontend — Scores Report Page
  - (pending)
