# 11. Multi-Year Taxonomy Strategy

## Status
Accepted

## Context
The system originally shipped with a single hardcoded US-GAAP taxonomy (2025, `taxonomy_type_id = 1`). SEC filings span many years, and each year's filing typically references the US-GAAP taxonomy version effective at that time. Using only one taxonomy version causes concept mismatches for older filings — presentation hierarchies and concept definitions change between years.

Key questions that needed resolution:

1. How should multiple taxonomy versions be stored and identified?
2. How should the system select the correct taxonomy version for a given operation?
3. How should taxonomy data be imported across years?

## Decision
Store each US-GAAP taxonomy year (2011–2025) as a separate row in the `taxonomy_types` table, keyed by `(taxonomy_type_name, taxonomy_type_version)` where `taxonomy_type_name = 'us-gaap'` and `taxonomy_type_version` is the four-digit year. Each year gets its own `taxonomy_type_id`, with corresponding rows in `taxonomy_concepts` and `taxonomy_presentation`.

### Year selection strategy
- **Statement printing**: The taxonomy year is inferred from the `--date` argument (filing/report date). The year component of the date is used to look up the matching `taxonomy_type_id` via `GetTaxonomyTypeByNameVersion("us-gaap", year)`.
- **CLI override**: An optional `--taxonomy-year <YYYY>` flag allows explicit selection when the inferred year is not desired (e.g., a company using an older taxonomy for a recent filing).
- **Data point import (`--parse-bulk-xbrl-archive`)**: Currently uses the 2025 taxonomy for concept matching. Future work may extend this to select the taxonomy year per filing.

### Import strategy
- Year discovery is filesystem-based: scan the taxonomy data directory for files matching `*_GAAP_Taxonomy.worksheets.concepts.csv`.
- `--load-taxonomy-all` imports every discovered year; `--load-taxonomy-year --year YYYY` imports a single year.
- Import is idempotent: existing concept/presentation counts are checked before inserting.
- `EnsureTaxonomyType` upserts the `taxonomy_types` row before loading data.

## Consequences
- Supports accurate concept and presentation hierarchy resolution for filings from 2011 onward.
- Adding a new taxonomy year (e.g., 2026) requires only placing the CSV files in the data directory and running `--load-taxonomy-all`.
- The `taxonomy_type_version` column uses `int` (year), which works for US-GAAP but may need revisiting if non-year-based taxonomies (e.g., IFRS) are added.
- Data point import still hardcodes the 2025 taxonomy for concept matching — a known limitation to address separately.
- Storage scales linearly with the number of taxonomy years (~15 years x ~20k concepts each).

## Alternatives Considered
- **Single merged taxonomy**: Combine all years into one set of concepts. Rejected because concept definitions, labels, and hierarchies change between years — merging would lose version-specific semantics.
- **Latest-year-only with fallback**: Always use the most recent taxonomy and fall back to name matching for older concepts. Rejected because presentation hierarchies differ significantly across years, leading to incorrect statement rendering.
- **Year as enum**: Model taxonomy years as a C# enum (`TaxonomyTypes`). Rejected in favor of treating year as data in the database, since new years are added regularly and should not require code changes.

---
