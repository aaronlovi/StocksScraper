export interface CsvColumn {
  header: string;
  value: (row: never) => unknown;
}

// Derive one column per own-property of the first row, headers = property names.
export function autoColumns(rows: readonly unknown[]): CsvColumn[] {
  if (rows.length === 0) return [];
  const first = rows[0] as Record<string, unknown>;
  const columns: CsvColumn[] = [];
  for (const key of Object.keys(first)) {
    columns.push({ header: key, value: (row: never) => (row as Record<string, unknown>)[key] });
  }
  return columns;
}

function escapeCell(value: unknown): string {
  if (value === null || value === undefined) return '';
  let text: string;
  if (Array.isArray(value)) {
    text = value.join('; ');
  } else if (typeof value === 'object') {
    text = JSON.stringify(value);
  } else {
    text = String(value);
  }
  if (/[",\r\n]/.test(text)) {
    text = '"' + text.replace(/"/g, '""') + '"';
  }
  return text;
}

export function toCsv(rows: readonly unknown[], columns: readonly CsvColumn[]): string {
  const lines: string[] = [];
  const headerCells: string[] = [];
  for (const col of columns) headerCells.push(escapeCell(col.header));
  lines.push(headerCells.join(','));

  for (const row of rows) {
    const cells: string[] = [];
    for (const col of columns) cells.push(escapeCell(col.value(row as never)));
    lines.push(cells.join(','));
  }
  return lines.join('\r\n') + '\r\n';
}
