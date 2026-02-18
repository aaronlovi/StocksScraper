# Progress

- [x] Checkpoint 1: Scoring Data Models
  - Files: dotnet/Stocks.DataModels/Scoring/ScoringConceptValue.cs, ScoringCheck.cs, DerivedMetrics.cs, ScoringResult.cs
  - Tests: dotnet/Stocks.EDGARScraper.Tests/Scoring/ScoringModelTests.cs
  - Committed: yes

- [x] Checkpoint 2: Data Access Layer
  - Files: GetScoringDataPointsStmt.cs, IDbmService.cs, DbmService.cs, DbmInMemoryData.cs, DbmInMemoryService.cs
  - Tests: dotnet/Stocks.EDGARScraper.Tests/Scoring/GetScoringDataPointsTests.cs
  - Committed: yes

- [x] Checkpoint 3: Scoring Computation Service
  - Files: dotnet/Stocks.Persistence/Services/ScoringService.cs
  - Tests: dotnet/Stocks.EDGARScraper.Tests/Scoring/ScoringServiceTests.cs
  - Committed: yes

- [x] Checkpoint 4: API Endpoint
  - Files: dotnet/Stocks.WebApi/Endpoints/ScoringEndpoints.cs, dotnet/Stocks.WebApi/Program.cs
  - Tests: dotnet/Stocks.WebApi.Tests/ScoringEndpointsTests.cs
  - Committed: yes

- [x] Checkpoint 5: Frontend Scoring Page
  - Files: frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts, frontend/stocks-frontend/src/app/core/services/api.service.ts, frontend/stocks-frontend/src/app/app.routes.ts, frontend/stocks-frontend/src/app/features/company/company.component.ts
  - Tests: frontend/stocks-frontend/src/app/features/scoring/scoring.component.spec.ts, frontend/stocks-frontend/src/app/features/company/company.component.spec.ts
  - Committed: pending
