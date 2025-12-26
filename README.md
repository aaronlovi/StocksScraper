# StocksScraper

## Taxonomy CSV Sources (US-GAAP 2024/2025)

The taxonomy CSVs under `/var/lib/edgar-data/us-gaap-taxonomies/` are generated from the official FASB US-GAAP taxonomy packages hosted at:

- <https://xbrl.fasb.org/us-gaap/2024/us-gaap-2024.zip>
- <https://xbrl.fasb.org/us-gaap/2025/us-gaap-2025.zip>

## How the CSVs Were Generated (Brief)

The project expects CSVs named like:

- `/var/lib/edgar-data/us-gaap-taxonomies/2025_GAAP_Taxonomy.worksheets.concepts.csv`
- `/var/lib/edgar-data/us-gaap-taxonomies/2025_GAAP_Taxonomy.worksheets.presentation.csv`

Because the FASB zip does not ship these exact worksheets, they were generated with Arelle and lightly transformed into the schema used by the loaders in `dotnet/Stocks.EDGARScraper`.

**Arelle** is an open-source XBRL processing tool that can load taxonomy entry points (XSDs) and export concepts/linkbase relationships to CSV. It was used here to export raw concept and presentation data before converting to the project’s expected CSV schema.

High-level steps used:

1) Download and unzip the FASB taxonomy packages into versioned folders.
2) Use Arelle to export raw concept and presentation CSVs from the taxonomy entry point.
3) Transform the raw CSVs into the minimal column set expected by the project.

Example commands (2025):

```bash
# 1) Download
mkdir -p /var/lib/edgar-data/us-gaap-taxonomies
curl -L -o /var/lib/edgar-data/us-gaap-taxonomies/us-gaap-2025.zip \
  https://xbrl.fasb.org/us-gaap/2025/us-gaap-2025.zip
unzip -q /var/lib/edgar-data/us-gaap-taxonomies/us-gaap-2025.zip \
  -d /var/lib/edgar-data/us-gaap-taxonomies/2025

# 2) Export raw concept + presentation CSVs
/home/aaron/projects/StocksScraper/.venv-taxonomy/bin/arelleCmdLine \
  -f /var/lib/edgar-data/us-gaap-taxonomies/2025/us-gaap-2025/entire/us-gaap-entryPoint-all-2025.xsd \
  --csvConcepts /var/lib/edgar-data/us-gaap-taxonomies/2025_concepts_raw.csv \
  --csvPre /var/lib/edgar-data/us-gaap-taxonomies/2025_pre_raw.csv \
  --relationshipCols=Name,LocalName

# 3) Transform to project schema (summary)
# - Concepts: keep US-GAAP namespace rows; output columns:
#   prefix, periodType, balance, abstract, name, label, documentation
# - Presentations: compute depth from left-most non-empty column; output columns:
#   prefix, name, depth, order, parent (parent uses prefix:name)
```

The same process was repeated for 2024. The final CSVs are placed at:

- `/var/lib/edgar-data/us-gaap-taxonomies/2024_GAAP_Taxonomy.worksheets.concepts.csv`
- `/var/lib/edgar-data/us-gaap-taxonomies/2024_GAAP_Taxonomy.worksheets.presentation.csv`
- `/var/lib/edgar-data/us-gaap-taxonomies/2025_GAAP_Taxonomy.worksheets.concepts.csv`
- `/var/lib/edgar-data/us-gaap-taxonomies/2025_GAAP_Taxonomy.worksheets.presentation.csv`

These files are consumed by:

- `dotnet/Stocks.EDGARScraper/Services/Taxonomies/UsGaap2025ConceptsFileProcessor.cs`
- `dotnet/Stocks.EDGARScraper/Services/Taxonomies/UsGaap2025PresentationFileProcessor.cs`
