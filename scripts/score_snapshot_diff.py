#!/usr/bin/env python3
"""Compare two score snapshots written by `--snapshot-scores --out <prefix>`.

Usage: score_snapshot_diff.py <a.csv> <b.csv> value|moat

Reports:
  - how many scores changed, with a histogram of the change;
  - entries and exits (with tickers) for value 13+, 14+, 15/15 or moat 11+, 12+, 13/13;
  - per-check flips (e.g. "c01 P>F 315");
  - companies present in only one file.

Standard library only.
"""

import csv
import sys
from collections import Counter

GROUPS = {"value": (15, [13, 14, 15]), "moat": (13, [11, 12, 13])}
MAX_TICKERS = 25


def load(path):
    with open(path, newline="") as f:
        return {row["company_id"]: row for row in csv.DictReader(f)}


def label(row):
    return row["ticker"] or f"#{row['company_id']}"


def ticker_list(rows):
    names = sorted(label(r) for r in rows)
    more = " ..." if len(names) > MAX_TICKERS else ""
    return " ".join(names[:MAX_TICKERS]) + more


def main(argv):
    if len(argv) != 4 or argv[3] not in GROUPS:
        print(__doc__.strip(), file=sys.stderr)
        return 2
    a, b = load(argv[1]), load(argv[2])
    n, groups = GROUPS[argv[3]]
    common = [cid for cid in a if cid in b]

    deltas = Counter()
    for cid in common:
        d = int(b[cid]["score"]) - int(a[cid]["score"])
        if d:
            deltas[d] += 1
    changed = sum(deltas.values())
    hist = ", ".join(f"{d:+d} x{k}" for d, k in sorted(deltas.items()))
    print(f"{changed} of {len(common)} scores changed" + (f": {hist}" if hist else ""))

    for g in groups:
        before = sum(1 for cid in common if int(a[cid]["score"]) >= g)
        after = sum(1 for cid in common if int(b[cid]["score"]) >= g)
        entered = [b[cid] for cid in common if int(a[cid]["score"]) < g <= int(b[cid]["score"])]
        exited = [b[cid] for cid in common if int(b[cid]["score"]) < g <= int(a[cid]["score"])]
        name = f"{g}/{n}" if g == n else f"{g}+"
        line = f"  {name}: {before} -> {after}"
        if entered:
            line += f"  +{len(entered)} ({ticker_list(entered)})"
        if exited:
            line += f"  -{len(exited)} ({ticker_list(exited)})"
        print(line)

    flips = []
    for i in range(1, n + 1):
        col = f"c{i:02d}"
        counts = Counter((a[cid][col], b[cid][col]) for cid in common if a[cid][col] != b[cid][col])
        if counts:
            flips.append(f"{col} " + " ".join(f"{x}>{y} {k}" for (x, y), k in counts.most_common()))
    print("  flips: " + ("; ".join(flips) if flips else "none"))

    only_a = [a[cid] for cid in a if cid not in b]
    only_b = [b[cid] for cid in b if cid not in a]
    if only_a:
        print(f"  only in {argv[1]}: {len(only_a)} ({ticker_list(only_a)})")
    if only_b:
        print(f"  only in {argv[2]}: {len(only_b)} ({ticker_list(only_b)})")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
