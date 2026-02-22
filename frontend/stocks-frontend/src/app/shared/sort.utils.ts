export class SortState {
  sortBy: string;
  sortDir: 'asc' | 'desc';

  constructor(defaultSort: string, defaultDir: 'asc' | 'desc' = 'desc') {
    this.sortBy = defaultSort;
    this.sortDir = defaultDir;
  }

  toggle(column: string): void {
    if (this.sortBy === column) {
      this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = column;
      this.sortDir = 'desc';
    }
  }

  indicator(column: string): string {
    if (this.sortBy !== column) return '';
    return this.sortDir === 'asc' ? '\u25B2' : '\u25BC';
  }
}
