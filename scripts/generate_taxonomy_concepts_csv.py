#!/usr/bin/env python3
import argparse
import csv
import re
from pathlib import Path


def normalize_header(value: str) -> str:
    return re.sub(r"[^a-z]", "", value.lower())


def get_value(row: dict, header_map: dict[str, str], key: str) -> str:
    header = header_map.get(key, "")
    return (row.get(header) or "").strip() if header else ""


def get_prefix(row: dict, header_map: dict[str, str]) -> str:
    namespace = get_value(row, header_map, "namespace")
    if "fasb.org/us-gaap" in namespace:
        return "us-gaap"

    prefix = get_value(row, header_map, "prefix")
    if prefix:
        return prefix

    qname = get_value(row, header_map, "qname")
    if ":" in qname:
        return qname.split(":", 1)[0]

    name = get_value(row, header_map, "name")
    if ":" in name:
        return name.split(":", 1)[0]

    return ""


def get_local_name(row: dict, header_map: dict[str, str]) -> str:
    name = get_value(row, header_map, "name")
    if name:
        return name.split(":", 1)[1] if ":" in name else name

    local_name = get_value(row, header_map, "localname")
    if local_name:
        return local_name

    qname = get_value(row, header_map, "qname")
    return qname.split(":", 1)[1] if ":" in qname else qname


def convert_concepts(raw_path: Path, out_path: Path) -> None:
    with raw_path.open("r", encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f)
        header_map = {normalize_header(k): k for k in reader.fieldnames or []}
        out_path.parent.mkdir(parents=True, exist_ok=True)
        with out_path.open("w", encoding="utf-8", newline="") as out:
            writer = csv.writer(out)
            writer.writerow(["prefix", "periodType", "balance", "abstract", "name", "label", "documentation"])

            for row in reader:
                prefix = get_prefix(row, header_map)
                if prefix != "us-gaap":
                    continue

                writer.writerow([
                    prefix,
                    get_value(row, header_map, "periodtype"),
                    get_value(row, header_map, "balance"),
                    get_value(row, header_map, "abstract"),
                    get_local_name(row, header_map),
                    get_value(row, header_map, "label"),
                    get_value(row, header_map, "documentation"),
                ])


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate taxonomy concepts CSV.")
    parser.add_argument("--year", type=int, required=True)
    parser.add_argument("--raw-concepts", type=Path, required=True)
    parser.add_argument("--out-dir", type=Path, required=True)
    args = parser.parse_args()

    out_path = args.out_dir / f"{args.year}_GAAP_Taxonomy.worksheets.concepts.csv"
    convert_concepts(args.raw_concepts, out_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
