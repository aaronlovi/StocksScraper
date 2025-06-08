# Stocks.EDGARScraper

## Overview

The `Stocks.EDGARScraper` project is a .NET 8 console application for downloading, parsing, and processing bulk EDGAR filings and XBRL data from the SEC. It orchestrates the extraction, transformation, and loading (ETL) of company, submission, and financial data into a database, and supports taxonomy processing for US GAAP 2025.

## Main Components

### 1. Data Download and Parsing
- **EdgarHttpClientService**: Downloads raw data files from the SEC EDGAR system.
- **ParseBulkEdgarSubmissionsContext / ParseBulkXbrlArchiveContext**: Context classes for parsing bulk EDGAR submissions and XBRL archives.
- **XBRLFileParser**: Parses XBRL JSON files and extracts financial data points.

### 2. ETL and Database Integration
- **Program.cs**: Main entry point; supports command-line operations for:
  - Downloading CIK/company lists
  - Parsing and loading bulk EDGAR submissions
  - Parsing and loading XBRL data
  - Loading taxonomy concepts and presentations
  - Dropping and managing database tables
- **Integration with Stocks.Persistence**: Uses database services for bulk inserts and data management.

### 3. Taxonomy Processing
- **UsGaap2025ConceptsFileProcessor / UsGaap2025PresentationFileProcessor**: Parse and load US GAAP 2025 taxonomy concepts and presentation hierarchies from CSV files into the database.
- **Taxonomy Options Models**: Configuration for taxonomy file locations.

### 4. Utilities and Services
- **PuppeteerService**: (Not currently in use) Utility for fetching rendered HTML using headless Chromium.
- **Enums and Models**: Internal enums and models for parsing and error handling.
- **Startup / ReportingHostConfig**: Application and gRPC service configuration.

---

This project is intended to be run as a batch ETL tool or as a service, and is designed to be extensible for additional data sources or taxonomy years.
