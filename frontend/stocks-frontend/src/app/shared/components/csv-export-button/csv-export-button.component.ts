import { Component, Input } from '@angular/core';
import { CsvColumn, autoColumns, toCsv } from '../../csv.utils';

@Component({
  selector: 'app-csv-export-button',
  standalone: true,
  templateUrl: './csv-export-button.component.html',
  styleUrls: ['./csv-export-button.component.css']
})
export class CsvExportButtonComponent {
  /** Rows to export. The button is disabled while empty. */
  @Input() rows: readonly unknown[] | null = null;
  /** Column spec; omitted = one column per property of the first row. */
  @Input() columns: CsvColumn[] | null = null;
  /** Base file name; the download is `<filename>-<yyyy-mm-dd>.csv`. */
  @Input() filename = 'export';
  @Input() label = 'Export CSV';

  get disabled(): boolean {
    return !this.rows || this.rows.length === 0;
  }

  export(): void {
    if (!this.rows || this.rows.length === 0) return;

    const columns = this.columns && this.columns.length > 0 ? this.columns : autoColumns(this.rows);
    const csv = toCsv(this.rows, columns);

    // BOM so Excel opens UTF-8 correctly
    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${this.filename}-${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
  }
}
