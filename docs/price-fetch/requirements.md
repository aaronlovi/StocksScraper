# Price Fetch Pipeline Requirements (Stooq CSV)

## Summary

Build a CLI-driven price fetch pipeline that uses the existing SEC CIK-to-ticker mapping JSON files to download daily price data from Stooq without ingesting any data into the database yet. The output will be stored as files under a configurable directory so the data can later be imported or analyzed.

## Requirements Table

| ID | Requirement | Description | Status | Notes |
| --- | --- | --- | --- | --- |
| 1 | Source selection | Use Stooq CSV as the primary free data source for daily prices. | Proposed | Stooq URLs: `https://stooq.com/q/d/l/?s=<ticker>.us&i=d` |
| 2 | Input mapping | Read ticker symbols from the SEC mapping JSON files already downloaded to the configured data directory. | Proposed | Use `company_tickers.json` and `company_tickers_exchange.json` if present. |
| 3 | CLI command | Provide a new CLI switch to fetch daily prices and write output files to disk. | Proposed | Example: `--download-prices-stooq` |
| 4 | Output files | Write one CSV per ticker symbol in the output directory, overwritten each run. | Proposed | Each file includes all dates for that ticker. |
| 5 | Config-driven paths | Use `EdgarDataDir` (or another config key) for locating mappings and writing output. | Proposed | No database changes. |
| 6 | Error handling | Handle missing tickers, invalid responses, or rate limits without aborting the whole run. | Proposed | Log warnings and continue. |
| 7 | Deduping/overwrites | Support overwriting existing files or skipping up-to-date ones. | Proposed | Controlled by a flag. |
| 8 | Logging | Log progress (counts, failures, elapsed time). | Proposed | Use existing Serilog configuration. |

## Kanban Task List (<= 2h each)

- [ ] Define Stooq output layout and naming convention for per-ticker CSVs.
- [ ] Add CLI command to trigger price downloads.
- [ ] Implement Stooq fetcher and file writer.
- [ ] Add config options for output folder and overwrite behavior.
- [ ] Add minimal tests for fetcher parsing and file output.
- [ ] Update `dotnet/run.sh` with new menu option.
- [ ] Update README/docs with usage and output description.

## High-Level Design

A CLI switch will load SEC mapping JSONs from the configured data directory, normalize tickers to Stooq’s expected format (e.g., `AAPL` -> `aapl.us`), fetch daily CSV data per ticker, and write the raw CSVs to an output directory. This pipeline is file-based only; no database ingestion occurs.

## Technical Design

- New CLI switch in `dotnet/Stocks.EDGARScraper/Program.cs`.
- New service class for Stooq download logic (uses `HttpClient`).
- Mapping loader reads:
  - `EdgarDataDir/company_tickers.json`
  - `EdgarDataDir/company_tickers_exchange.json` (optional for exchange filtering)
- Output layout:
  - One CSV per ticker under `StooqPrices:OutputDir`, overwritten each run.
  - Each row includes CIK, normalized ticker, exchange, Stooq symbol, and price fields.
- Optional flags:
  - `--overwrite` to overwrite existing files
  - `--limit <N>` to cap tickers per run
  - `StooqPrices:DelayMilliseconds` to slow requests when rate limits are hit
  - `StooqPrices:MaxRetries` to cap retry attempts on parse failures

## Implementation Context

- CLI entry point: `dotnet/Stocks.EDGARScraper/Program.cs`
- Existing config key: `EdgarDataDir` in `dotnet/Stocks.EDGARScraper/appsettings.json`
- SEC mapping download command: `--download-sec-ticker-mappings`
- Logging: Serilog configuration in `appsettings.json`

## Implementation Hints

- Normalize tickers to uppercase for output, and lowercase + `.us` for Stooq requests.
- Stooq CSV schema: `Date,Open,High,Low,Close,Volume`.
- Output schema (proposed): `Cik,Ticker,Exchange,StooqSymbol,Date,Open,High,Low,Close,Volume`.
- For missing symbols, Stooq returns a short CSV or error content; detect invalid content early.
- Prefer explicit loops (avoid LINQ in production).

## Glossary

- **CIK**: Central Index Key assigned by the SEC to issuers.
- **Ticker**: Exchange symbol used to identify a security for trading/pricing.
- **Stooq**: Free data provider offering daily price CSV downloads.
- **EdgarDataDir**: Base local directory for SEC data artifacts and related downloads.
- **Normalized ticker**: Uppercase ticker symbol used in output (e.g., `AAPL`).
- **Stooq symbol**: Lowercase ticker with Stooq suffix (e.g., `aapl.us`).
