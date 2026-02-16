#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="/var/lib/edgar-data/us-gaap-taxonomies"
ARELLE_CMD="${ARELLE_CMD:-arelleCmdLine}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --root-dir)
      ROOT_DIR="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1"
      exit 2
      ;;
  esac
done

if ! command -v "$ARELLE_CMD" >/dev/null 2>&1; then
  echo "Missing Arelle command '$ARELLE_CMD' in PATH. Set ARELLE_CMD or install Arelle."
  exit 2
fi

if [[ ! -d "$ROOT_DIR" ]]; then
  echo "Root dir not found: $ROOT_DIR"
  exit 2
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

CONCEPTS_SCRIPT="$REPO_ROOT/scripts/generate_taxonomy_concepts_csv.py"
PRESENTATION_SCRIPT="$REPO_ROOT/scripts/generate_taxonomy_csvs.py"

if [[ ! -f "$CONCEPTS_SCRIPT" || ! -f "$PRESENTATION_SCRIPT" ]]; then
  echo "Missing taxonomy scripts under $REPO_ROOT/scripts"
  exit 2
fi

shopt -s nullglob
confirm_zip_found=false
for zip_path in "$ROOT_DIR"/us-gaap-*.zip; do
  confirm_zip_found=true
  zip_name="$(basename "$zip_path")"
  if [[ "$zip_name" =~ us-gaap-([0-9]{4}) ]]; then
    year="${BASH_REMATCH[1]}"
  else
    echo "Skipping unrecognized zip: $zip_name"
    continue
  fi

  year_dir="$ROOT_DIR/$year"
  mkdir -p "$year_dir"

  echo "Extracting $zip_name -> $year_dir"
  unzip -q -n "$zip_path" -d "$year_dir"

  entrypoint="$(find "$year_dir" -type f -path "*/entire/us-gaap-entryPoint-all-${year}*.xsd" -print -quit)"
  if [[ -z "$entrypoint" ]]; then
    echo "Missing entry point for $year under $year_dir"
    continue
  fi

  raw_concepts="$ROOT_DIR/${year}_concepts_raw.csv"
  raw_pre="$ROOT_DIR/${year}_pre_raw.csv"

  echo "Exporting raw CSVs for $year"
  "$ARELLE_CMD" \
    -f "$entrypoint" \
    --csvConcepts "$raw_concepts" \
    --csvPre "$raw_pre" \
    --relationshipCols=Name,LocalName

  echo "Transforming CSVs for $year"
  python3 "$CONCEPTS_SCRIPT" \
    --year "$year" \
    --raw-concepts "$raw_concepts" \
    --out-dir "$ROOT_DIR"

  python3 "$PRESENTATION_SCRIPT" \
    --year "$year" \
    --raw-pre "$raw_pre" \
    --out-dir "$ROOT_DIR"
done

if [[ "$confirm_zip_found" == false ]]; then
  echo "No taxonomy ZIPs found under $ROOT_DIR"
  exit 1
fi
