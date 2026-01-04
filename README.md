# StocksScraper

## Taxonomy CSV Sources (US-GAAP 2024/2025)

The taxonomy CSVs under `/var/lib/edgar-data/us-gaap-taxonomies/` are generated from the official FASB US-GAAP taxonomy packages hosted at:

- 2022–present: `https://xbrl.fasb.org/us-gaap/<YEAR>/us-gaap-<YEAR>.zip`
- 2011–2021: `https://xbrl.fasb.org/us-gaap/<YEAR>/us-gaap-<YEAR>-01-31.zip`
Examples:
- <https://xbrl.fasb.org/us-gaap/2024/us-gaap-2024.zip>
- <https://xbrl.fasb.org/us-gaap/2025/us-gaap-2025.zip>

Download helper (2011–present, no overwrite):

```bash
mkdir -p /var/lib/edgar-data/us-gaap-taxonomies
for year in $(seq 2011 2025); do
  if [ "$year" -le 2021 ]; then
    suffix="-01-31"
  else
    suffix=""
  fi
  zip_path="/var/lib/edgar-data/us-gaap-taxonomies/us-gaap-${year}${suffix}.zip"
  if [ -f "$zip_path" ]; then
    echo "skip: $zip_path exists"
    continue
  fi
  url="https://xbrl.fasb.org/us-gaap/${year}/us-gaap-${year}${suffix}.zip"
  echo "downloading: $url"
  if ! curl -fL --retry 2 --retry-delay 1 -o "$zip_path" "$url"; then
    echo "failed: $url"
    rm -f "$zip_path"
  fi
done
```

## How the CSVs Were Generated (Brief)

The project expects CSVs named like:

- `/var/lib/edgar-data/us-gaap-taxonomies/2025_GAAP_Taxonomy.worksheets.concepts.csv`
- `/var/lib/edgar-data/us-gaap-taxonomies/2025_GAAP_Taxonomy.worksheets.presentation.csv`

Because the FASB zip does not ship these exact worksheets, they were generated with Arelle and lightly transformed into the schema used by the loaders in `dotnet/Stocks.EDGARScraper`.

**Arelle** is an open-source XBRL processing tool that can load taxonomy entry points (XSDs) and export concepts/linkbase relationships to CSV. It was used here to export raw concept and presentation data before converting to the project’s expected CSV schema.

The presentation CSV now includes a `role_name` column so statement traversal can be scoped to a specific presentation role.
Use `--list-statements` to see available role names and pass one via `--role` when printing a statement.

## Regenerate Taxonomy CSVs (Role-Aware)

Use this when you need to rebuild the presentation CSV with `role_name`.

Prerequisites:

- FASB US-GAAP taxonomy ZIPs downloaded and extracted under `/var/lib/edgar-data/us-gaap-taxonomies/<YEAR>/`.
- Arelle installed (example below uses a local venv).

Steps (2025 example):

```bash
# 0) Download and extract the FASB taxonomy package (one-time per year)
mkdir -p /var/lib/edgar-data/us-gaap-taxonomies/2025
curl -L -o /var/lib/edgar-data/us-gaap-taxonomies/us-gaap-2025.zip \
  https://xbrl.fasb.org/us-gaap/2025/us-gaap-2025.zip
unzip -q /var/lib/edgar-data/us-gaap-taxonomies/us-gaap-2025.zip \
  -d /var/lib/edgar-data/us-gaap-taxonomies/2025

# 1) Create a local venv and install Arelle (one-time)
python3 -m venv .venv-taxonomy
. .venv-taxonomy/bin/activate
pip install arelle-release

# 2) Export raw concept + presentation CSVs from the entry point
arelleCmdLine \
  -f /var/lib/edgar-data/us-gaap-taxonomies/2025/us-gaap-2025/entire/us-gaap-entryPoint-all-2025.xsd \
  --csvConcepts /var/lib/edgar-data/us-gaap-taxonomies/2025_concepts_raw.csv \
  --csvPre /var/lib/edgar-data/us-gaap-taxonomies/2025_pre_raw.csv \
  --relationshipCols=Name,LocalName

# 3) Transform raw CSVs to the project schema
python3 scripts/generate_taxonomy_concepts_csv.py \
  --year 2025 \
  --raw-concepts /var/lib/edgar-data/us-gaap-taxonomies/2025_concepts_raw.csv \
  --out-dir /var/lib/edgar-data/us-gaap-taxonomies

python3 scripts/generate_taxonomy_csvs.py \
  --year 2025 \
  --raw-pre /var/lib/edgar-data/us-gaap-taxonomies/2025_pre_raw.csv \
  --out-dir /var/lib/edgar-data/us-gaap-taxonomies
```

This produces:

- `/var/lib/edgar-data/us-gaap-taxonomies/2025_GAAP_Taxonomy.worksheets.concepts.csv`
- `/var/lib/edgar-data/us-gaap-taxonomies/2025_GAAP_Taxonomy.worksheets.presentation.csv`

Repeat with `--year 2024` and a 2024 entry point to rebuild 2024.

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

# 3) Transform to project schema
python3 scripts/generate_taxonomy_concepts_csv.py \
  --year 2025 \
  --raw-concepts /var/lib/edgar-data/us-gaap-taxonomies/2025_concepts_raw.csv \
  --out-dir /var/lib/edgar-data/us-gaap-taxonomies

python3 scripts/generate_taxonomy_csvs.py \
  --year 2025 \
  --raw-pre /var/lib/edgar-data/us-gaap-taxonomies/2025_pre_raw.csv \
  --out-dir /var/lib/edgar-data/us-gaap-taxonomies

# Output schemas
# - Concepts: keep US-GAAP namespace rows; output columns:
#   prefix, periodType, balance, abstract, name, label, documentation
# - Presentations: track the current role header row, compute depth from the
#   left-most non-empty column, and output columns:
#   prefix, name, depth, order, parent (parent uses prefix:name), role_name
#   (role_name is sourced from the role header row in the Arelle CSV output)
# - Arelle concept CSV headers can include soft hyphens (e.g., "Abs­tract"),
#   and do not always include a Prefix column. The concepts script normalizes
#   headers and uses Namespace to identify US-GAAP rows.
```

The same process was repeated for 2024. The final CSVs are placed at:

- `/var/lib/edgar-data/us-gaap-taxonomies/2024_GAAP_Taxonomy.worksheets.concepts.csv`
- `/var/lib/edgar-data/us-gaap-taxonomies/2024_GAAP_Taxonomy.worksheets.presentation.csv`
- `/var/lib/edgar-data/us-gaap-taxonomies/2025_GAAP_Taxonomy.worksheets.concepts.csv`
- `/var/lib/edgar-data/us-gaap-taxonomies/2025_GAAP_Taxonomy.worksheets.presentation.csv`

These files are consumed by:

- `dotnet/Stocks.EDGARScraper/Services/Taxonomies/UsGaap2025ConceptsFileProcessor.cs`
- `dotnet/Stocks.EDGARScraper/Services/Taxonomies/UsGaap2025PresentationFileProcessor.cs`
