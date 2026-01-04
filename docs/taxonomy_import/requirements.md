# Taxonomy Import (Multi-Year) Requirements

## Summary

Extend taxonomy ingestion to import all available US-GAAP taxonomies (not just 2025). The import should load concepts and presentation hierarchies for each year available on disk, register the taxonomy year in `taxonomy_types`, and keep the existing 2025 pipeline working. The system should support selecting the appropriate taxonomy year for downstream usage and provide a consistent way to add new years as they become available.

## Requirements Table

| ID | Requirement | Description | Status | Notes |
| --- | --- | --- | --- | --- |
| 1 | Discover years | Detect available US-GAAP taxonomy years from the taxonomy data directory. | Proposed | Use on-disk CSVs as the source of truth. |
| 2 | Taxonomy types | Insert missing years into `taxonomy_types` before import. | Proposed | Avoid duplicate rows; keep id stable. |
| 3 | Concepts import | Import concepts for each year into `taxonomy_concepts`. | Proposed | Reuse existing CSV parsing logic. |
| 4 | Presentation import | Import presentations for each year into `taxonomy_presentation`. | Proposed | Include role name and parent mapping. |
| 5 | CLI workflow | Add a CLI switch to import all years (or a specific year). | Proposed | Default to all years. |
| 6 | Safety | Make import idempotent and resumable. | Proposed | Skip years already imported unless forced. |
| 7 | Tests | Add tests for year discovery and per-year import selection. | Proposed | No external network calls. |
| 8 | Downstream selection | Provide a way to select taxonomy year when loading concepts/presentations. | Proposed | Default to most recent year. |

## Kanban Task List (<= 2h each)

- [ ] Inspect taxonomy storage layout and define year discovery rules.
- [ ] Add repository/config options for taxonomy root directory.
- [ ] Implement `taxonomy_types` upsert for multiple years.
- [ ] Generalize concept/presentation processors to accept year.
- [ ] Add CLI switch for multi-year import + optional `--year`.
- [ ] Add tests for discovery and selection.
- [ ] Update statement printing and report loading to pick a taxonomy year.
- [ ] Create ADR for multi-year taxonomy strategy.

## High-Level Design

1) Scan the taxonomy data directory for available years.  
2) Ensure each year exists in `taxonomy_types`.  
3) For each year, load the concepts CSV and presentation CSV, then bulk insert.  
4) Expose CLI switches to run imports across all years or a single year.  
5) Provide a default year (latest available) for downstream use, with an override option.

## Technical Design

- **Year discovery**:
  - Use filesystem patterns like `YYYY_GAAP_Taxonomy.worksheets.concepts.csv` and `...presentation.csv`.
  - Derive year from filename.
- **Taxonomy types**:
  - Add a statement to read existing `taxonomy_types` and insert missing year rows.
  - Keep `taxonomy_type_name` as `us-gaap`; set `taxonomy_type_version` to year.
- **Processors**:
  - Convert `UsGaap2025ConceptsFileProcessor` and `UsGaap2025PresentationFileProcessor` into year-parameterized processors.
  - Accept CSV path and taxonomy_type_id as inputs.
- **CLI**:
  - New switch: `--load-taxonomy-all` (imports all years).
  - Optional `--year` arg to import one year.
- **Downstream selection**:
  - Provide a helper to resolve “latest available year” from `taxonomy_types`.
  - When querying concepts/presentations, accept taxonomy_type_id or year.

## Implementation Context

- `dotnet/Stocks.Persistence/Database/Migrations/V003__UsGaapTaxonomies.sql`
- `dotnet/Stocks.Persistence/Database/Statements/GetTaxonomyConceptsStmt.cs`
- `dotnet/Stocks.Persistence/Database/Statements/GetTaxonomyPresentationsByTaxonomyTypeStmt.cs`
- `dotnet/Stocks.EDGARScraper/Services/Taxonomies/UsGaap2025ConceptsFileProcessor.cs`
- `dotnet/Stocks.EDGARScraper/Services/Taxonomies/UsGaap2025PresentationFileProcessor.cs`
- `dotnet/Stocks.DataModels/Enums/TaxonomyTypes.cs`
- `scripts/generate_taxonomy_concepts_csv.py`
- `scripts/generate_taxonomy_presentation_csv.py`

## Implementation Hints

- Prefer explicit loops; avoid LINQ in production.
- Reuse existing CSV parsing logic; avoid duplicating parsers.
- Treat taxonomy year as data, not enum, wherever possible.
- Make imports idempotent: check for existing taxonomy_type_id and existing concepts before inserting.

## Open Questions

- Where should the taxonomy root directory be configured (appsettings vs CLI)?
- Should we store an explicit `taxonomy_type_id` mapping table by year, or resolve on demand?
- Do we need to backfill `taxonomy_types` for years already imported if the table is empty?
- Should statement printing default to the latest taxonomy year or to the filing year?

## Glossary

- **Taxonomy year**: US-GAAP version year (e.g., 2025, 2024) used to label taxonomy data.
- **Taxonomy types table**: Database table tracking taxonomy name + version.
- **Concepts CSV**: Generated CSV file containing taxonomy concept metadata.
- **Presentation CSV**: Generated CSV file containing taxonomy presentation hierarchy.
