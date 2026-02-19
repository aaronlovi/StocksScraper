#!/usr/bin/env bash
# Generate DEI worksheet CSVs from raw taxonomy CSVs.
#
# Raw CSV columns (2017+, 13 columns):
#   Label,Name,ID,Namespace,Abstract,Substitution Group,Type,Period Type,Balance,Nillable,Typed Domain Ref,Facets,Documentation
#
# Raw CSV columns (2011-2016, 12 columns):
#   Label,Name,ID,Namespace,Abstract,Substitution Group,Type,Period Type,Balance,Nillable,Facets,Documentation
#
# Worksheet CSV columns:
#   prefix,periodType,balance,abstract,name,label,documentation
#
# DEI rows are identified by Namespace containing "/dei/".

set -euo pipefail

ROOT_DIR="${1:-/var/lib/edgar-data/us-gaap-taxonomies}"

for raw_csv in "$ROOT_DIR"/*_concepts_raw.csv; do
    [ -f "$raw_csv" ] || continue

    year=$(basename "$raw_csv" | grep -oP '^\d{4}')
    [ -z "$year" ] && continue

    output="$ROOT_DIR/${year}_DEI_Taxonomy.worksheets.concepts.csv"

    if [ -f "$output" ]; then
        echo "Skipping $year — $output already exists"
        continue
    fi

    echo "Generating $output ..."

    # Write header
    echo 'prefix,periodType,balance,abstract,name,label,documentation' > "$output"

    # Use Python for reliable CSV parsing (raw CSVs have quoted fields with commas)
    python3 -c "
import csv, sys

with open(sys.argv[1], newline='', encoding='utf-8-sig') as f:
    reader = csv.reader(f)
    header = next(reader)
    num_cols = len(header)

    # Determine documentation column index based on column count
    # 13 columns: doc is index 12; 12 columns: doc is index 11
    doc_idx = num_cols - 1

    for row in reader:
        if len(row) < num_cols:
            continue
        namespace = row[3]
        if '/dei/' not in namespace:
            continue
        label = row[0]
        name = row[1]
        abstract_val = row[4].lower()
        period_type = row[7].lower()
        balance = row[8].lower()
        documentation = row[doc_idx]

        writer = csv.writer(sys.stdout)
        writer.writerow(['dei', period_type, balance, abstract_val, name, label, documentation])
" "$raw_csv" >> "$output"

    count=$(( $(wc -l < "$output") - 1 ))
    echo "  -> $count dei concepts for year $year"
done

echo "Done."
