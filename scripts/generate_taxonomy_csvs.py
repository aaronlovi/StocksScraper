#!/usr/bin/env python3
import argparse
import csv
from pathlib import Path


def normalize_role_name(value: str) -> str:
    return value.strip()


def convert_presentation(raw_path: Path, out_path: Path) -> None:
    with raw_path.open("r", encoding="utf-8-sig", newline="") as f:
        reader = csv.reader(f)
        header = next(reader)
        try:
            pref_label_idx = header.index("Pref. Label")
        except ValueError as exc:
            raise ValueError("Missing 'Pref. Label' column in raw CSV.") from exc
        try:
            name_idx = header.index("Name")
        except ValueError as exc:
            raise ValueError("Missing 'Name' column (use --relationshipCols=Name,LocalName).") from exc

        out_path.parent.mkdir(parents=True, exist_ok=True)
        with out_path.open("w", encoding="utf-8", newline="") as out:
            writer = csv.writer(out)
            writer.writerow(["prefix", "name", "depth", "order", "parent", "role_name"])

            role_name = ""
            parent_chain = []
            order_by_parent = {}

            for row in reader:
                if not row:
                    continue
                if len(row) < len(header):
                    row = row + [""] * (len(header) - len(row))

                name_value = row[name_idx].strip()
                depth = None
                for i in range(pref_label_idx):
                    if row[i].strip():
                        depth = i
                        break
                if depth is None:
                    continue

                if depth == 0:
                    role_name = normalize_role_name(row[0])
                    parent_chain = []
                    order_by_parent = {}
                    continue

                if not name_value:
                    continue

                if ":" in name_value:
                    prefix, local_name = name_value.split(":", 1)
                else:
                    prefix, local_name = "", name_value

                parent = parent_chain[depth - 2] if depth > 1 and len(parent_chain) >= depth - 1 else None
                parent_key = parent if parent else ("", "")
                order_by_parent[(depth, parent_key)] = order_by_parent.get((depth, parent_key), 0) + 1
                order = order_by_parent[(depth, parent_key)]

                parent_value = f"{parent[0]}:{parent[1]}" if parent and parent[0] else (parent[1] if parent else "")

                writer.writerow([prefix, local_name, str(depth), str(order), parent_value, role_name])

                depth_index = depth - 1
                if depth_index == len(parent_chain):
                    parent_chain.append((prefix, local_name))
                else:
                    if depth_index < len(parent_chain):
                        parent_chain[depth_index] = (prefix, local_name)
                    else:
                        while len(parent_chain) < depth_index:
                            parent_chain.append(("", ""))
                        parent_chain.append((prefix, local_name))
                if len(parent_chain) > depth_index + 1:
                    parent_chain = parent_chain[: depth_index + 1]


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate role-aware taxonomy presentation CSV.")
    parser.add_argument("--year", type=int, required=True)
    parser.add_argument("--raw-pre", type=Path, required=True)
    parser.add_argument("--out-dir", type=Path, required=True)
    args = parser.parse_args()

    out_path = args.out_dir / f"{args.year}_GAAP_Taxonomy.worksheets.presentation.csv"
    convert_presentation(args.raw_pre, out_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
