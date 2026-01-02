# Price Import Phase 2 Requirements (Stooq -> Database)

## Summary

Extend the Stooq price pipeline to import daily price data into the database. Persist raw price rows with the same schema as the current Stooq CSV outputs, and add a supplementary table to track when each ticker was last imported. The importer should select which tickers to import based on last-import timestamps to reduce repeated hits and avoid exhausting the Stooq daily limit on the same symbols.

## Requirements Table

| ID | Requirement | Description | Status | Notes |
| --- | --- | --- | --- | --- |
| 1 | Price data table | Create a database table to store Stooq daily price rows with the same fields as the CSV output. | Proposed | Fields: Cik, Ticker, Exchange, StooqSymbol, Date, Open, High, Low, Close, Volume. |
| 2 | Import tracking table | Create a supplementary table to track last-imported date/time per ticker. | Proposed | Used to rotate tickers on subsequent runs. |
| 3 | Import CLI | Add a CLI switch to import Stooq price data into the database. | Proposed | Separate from download command. |
| 4 | Import selection | Determine which tickers to import based on last-imported timestamps. | Proposed | Prefer least recently imported first. |
| 5 | Incremental import | Avoid duplicate inserts for previously-imported dates. | Proposed | Use unique constraint or upsert logic. |
| 6 | Error handling | Log per-ticker failures and continue. | Proposed | Do not abort entire run for a few failures. |
| 7 | Config | Add config values for import batch size and max tickers per run. | Proposed | Avoid hitting Stooq daily limits. |
| 8 | Tests | Add unit tests for ticker selection and import logic. | Proposed | No external network calls in tests. |

## Kanban Task List (<= 2h each)

- [ ] Design schema for Stooq price table and import tracking table.
- [ ] Add migrations for new tables and indexes/constraints.
- [ ] Implement DB statements and DTOs for inserting price rows.
- [ ] Implement import tracking upsert/update.
- [ ] Implement ticker selection (least recently imported first).
- [ ] Add CLI command and options for import run.
- [ ] Add tests for selection and import logic.
- [ ] Update docs/run.sh for the new import command.

## High-Level Design

Introduce two new tables: one for daily price rows and one for per-ticker import tracking. The importer reads existing Stooq CSVs (from the download step) and loads them into the price table. After a ticker is imported, the tracking table is updated with the last-imported timestamp. The importer chooses tickers in ascending order of last-imported time to spread requests across the universe and reduce daily limit issues.

## Technical Design

- **New tables**:
  - `prices` (name TBD):
    - `price_id` (PK)
    - `cik` (bigint)
    - `ticker` (text)
    - `exchange` (text)
    - `stooq_symbol` (text)
    - `price_date` (date)
    - `open` (numeric)
    - `high` (numeric)
    - `low` (numeric)
    - `close` (numeric)
    - `volume` (bigint)
    - Unique index on `(ticker, price_date)` (no vendor name)
  - `price_imports` (name TBD):
    - `ticker` (text, PK)
    - `last_imported_utc` (timestamp)
    - Optional: `last_attempt_utc`, `last_error` (future)

- **Importer flow**:
  1) Load tickers from `company_tickers.json` (existing mapping).
  2) Read `stooq_price_imports` to determine last import times.
  3) Select the next N tickers by oldest `last_imported_utc`.
  4) For each ticker:
     - Read CSV from `StooqPrices:OutputDir/<TICKER>.csv`.
     - Insert rows into `prices` (skip duplicates via conflict handling).
     - Update `price_imports` with current timestamp.

- **Config**:
  - `StooqImport:MaxTickersPerRun`
  - `StooqImport:BatchSize`

## Implementation Context

- CLI entry point: `dotnet/Stocks.EDGARScraper/Program.cs`
- DB access patterns: `dotnet/Stocks.Persistence/Database/*` statements and DTOs
- Migration mechanism: `dotnet/Stocks.Persistence/Database/Migrations` (Evolve)
- Existing Stooq download: `dotnet/Stocks.EDGARScraper/Services/StooqPriceDownloader.cs`

## Implementation Hints

- Prefer explicit loops; avoid LINQ in production code.
- Use `Result` pattern for errors.
- Use a unique constraint to make inserts idempotent.
- Update tracking table only after a successful import for that ticker.

## Glossary

- **Stooq price row**: A daily OHLCV record for a ticker.
- **Import tracking**: A record of when each ticker was last imported.
- **Last-imported UTC**: Timestamp used to prioritize the next tickers for import.
