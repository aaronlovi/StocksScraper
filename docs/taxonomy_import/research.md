# Taxonomy Import Research Notes

## Goal

Define how to import multi-year US-GAAP taxonomies into the existing schema, including data sources, download approach, and ingestion workflow.

## Current State (from codebase)

- Taxonomy concepts and presentations are imported from CSVs:
  - `scripts/generate_taxonomy_concepts_csv.py`
  - `scripts/generate_taxonomy_presentation_csv.py`
- `UsGaap2025ConceptsFileProcessor` and `UsGaap2025PresentationFileProcessor` load CSVs into:
  - `taxonomy_concepts`
  - `taxonomy_presentation`
- `taxonomy_types` currently has only one entry (us-gaap, 2025) created in `V003__UsGaapTaxonomies.sql`.
- `TaxonomyTypes` enum is currently hard-coded to `US_GAAP_2025`.

## Data Sources (EDGAR)

Based on the current pipeline, taxonomy data is derived from US-GAAP taxonomy files (typically XBRL packages released annually by FASB/SEC). The project already assumes:

- A “raw concepts CSV” exists per year (e.g., `2025_concepts_raw.csv`).
- A “presentation CSV” exists per year (e.g., `2025_GAAP_Taxonomy.worksheets.presentation.csv`).

Download patterns confirmed from existing 2024/2025 guidance plus 2011+ testing:

- For 2022–present: `https://xbrl.fasb.org/us-gaap/<YEAR>/us-gaap-<YEAR>.zip`
- For 2011–2021: `https://xbrl.fasb.org/us-gaap/<YEAR>/us-gaap-<YEAR>-01-31.zip`

## Download Options

### Option A: Keep Python scripts for CSV generation (current pattern)

1) Download taxonomy packages manually or via script.
2) Use `scripts/generate_taxonomy_concepts_csv.py` and `scripts/generate_taxonomy_presentation_csv.py`.
3) Store CSVs per year in a consistent directory layout.
4) Import via a generalized .NET processor that takes year + CSV paths.

Pros:

- Reuses existing scripts and parsing logic.
- Minimal .NET complexity for raw XBRL parsing.

Cons:

- Requires a separate step outside of .NET for downloads and CSV generation.

### Option B: Implement taxonomy download + CSV generation in .NET

1) Add an HTTP downloader for taxonomy zip files per year.
2) Parse XBRL taxonomy files directly in C#.
3) Generate concepts/presentation rows in memory and insert.

Pros:

- Single tooling stack.
- Easier to automate end-to-end.

Cons:

- More complex; likely re-implements Arelle-like logic in C#.

### Option C: Hybrid (download in C#, parse via Python)

1) C# downloads taxonomy zip files.
2) Python scripts parse and write CSVs.
3) C# imports CSVs into DB.

Pros:

- Keeps parsing logic in Python; reduces C# complexity.
- Automates downloads.

Cons:

- Multi-language workflow.

## Proposed Import Workflow (Multi-Year)

1) **Discover available years** from CSV filenames:
   - `YYYY_concepts_raw.csv` (raw input)
   - `YYYY_GAAP_Taxonomy.worksheets.concepts.csv`
   - `YYYY_GAAP_Taxonomy.worksheets.presentation.csv`
2) **Ensure taxonomy_types** has `(us-gaap, YEAR)` entries for each year.
3) **Import concepts** from `...concepts.csv` into `taxonomy_concepts` with the year-specific `taxonomy_type_id`.
4) **Import presentations** from `...presentation.csv` into `taxonomy_presentation` with the same year-specific `taxonomy_type_id` association via `taxonomy_concept_id`.

## Database Fit

The existing schema supports multiple years via:

- `taxonomy_types.taxonomy_type_version` (year)
- `taxonomy_concepts.taxonomy_type_id`
- `taxonomy_presentation` referencing `taxonomy_concept_id`

No schema changes required to add multi-year support, but application code and enums need to be generalized to resolve taxonomy type by year.

## Required Code Changes (likely)

Based on your preferences:

- Prefer C# for new work unless a Python package dramatically reduces effort.
- Multi-step automation is acceptable.
- Target all available years from EDGAR.
- Store files under `/var/lib/edgar-data/...` alongside other data files.

Likely code changes:

- Replace hard-coded `US_GAAP_2025` usage with a resolver for taxonomy year.
- Generalize `UsGaap2025ConceptsFileProcessor` and `UsGaap2025PresentationFileProcessor` to accept year + CSV path.
- Add a CLI switch to import all years or a single year.
- Add a DB query for available taxonomy years (from `taxonomy_types`).

## Open Questions for You

Answered:

- **Preferred tooling**: Prefer C# unless a Python package is significantly easier.
- **Desired automation**: Multi-step is acceptable.
- **Year coverage**: All available years from EDGAR.
- **Storage layout**: Under `/var/lib/edgar-data/...` alongside existing data files.

Still open:

- **Source of taxonomy packages**: Please provide the official source/URL pattern for historical US-GAAP taxonomies (SEC archive, FASB, etc.).

## Next Step Proposal

Start by standardizing a year-based folder/filename convention and add a .NET importer that loops over all available years, reusing the existing CSV format. Once that’s in place, decide whether to automate downloads in C# or keep the Python-based CSV generation step.
