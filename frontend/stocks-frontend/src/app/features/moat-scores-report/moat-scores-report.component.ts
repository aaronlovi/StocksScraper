import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  ApiService,
  CompanyMoatScoreSummary,
  PaginationResponse
} from '../../core/services/api.service';

@Component({
  selector: 'app-moat-scores-report',
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
    <h2>Company Buffett Scores Report</h2>

    <div class="filters">
      <label>
        Min Score
        <select [(ngModel)]="minScore" (ngModelChange)="onFilterChange()">
          <option [ngValue]="null">Any</option>
          <option [ngValue]="10">10+</option>
          <option [ngValue]="9">9+</option>
          <option [ngValue]="8">8+</option>
          <option [ngValue]="7">7+</option>
          <option [ngValue]="5">5+</option>
        </select>
      </label>
      <label>
        Exchange
        <select [(ngModel)]="exchange" (ngModelChange)="onFilterChange()">
          <option [ngValue]="null">All</option>
          <option value="NASDAQ">NASDAQ</option>
          <option value="NYSE">NYSE</option>
          <option value="CBOE">CBOE</option>
        </select>
      </label>
      <label>
        Page Size
        <select [(ngModel)]="pageSize" (ngModelChange)="onFilterChange()">
          <option [ngValue]="25">25</option>
          <option [ngValue]="50">50</option>
          <option [ngValue]="100">100</option>
        </select>
      </label>
    </div>

    @if (loading()) {
      <p>Loading Buffett scores...</p>
    } @else if (error()) {
      <p class="error">{{ error() }}</p>
    } @else if (items().length === 0) {
      <p class="no-results">No companies match the current filters.</p>
    } @else {
      <table>
        <thead>
          <tr>
            <th class="sortable" (click)="toggleSort('overallScore')">
              Score {{ sortIndicator('overallScore') }}
            </th>
            <th>Company</th>
            <th>Ticker</th>
            <th>Exchange</th>
            <th class="num">Price</th>
            <th class="num">Market Cap</th>
            <th class="num sortable" (click)="toggleSort('averageGrossMargin')">
              Gross Margin {{ sortIndicator('averageGrossMargin') }}
            </th>
            <th class="num sortable" (click)="toggleSort('averageOperatingMargin')">
              Op. Margin {{ sortIndicator('averageOperatingMargin') }}
            </th>
            <th class="num sortable" (click)="toggleSort('averageRoeCF')">
              Avg ROE (CF) {{ sortIndicator('averageRoeCF') }}
            </th>
            <th class="num sortable" (click)="toggleSort('averageRoeOE')">
              Avg ROE (OE) {{ sortIndicator('averageRoeOE') }}
            </th>
            <th class="num sortable" (click)="toggleSort('estimatedReturnOE')">
              Est. Return (OE) {{ sortIndicator('estimatedReturnOE') }}
            </th>
            <th class="num sortable" (click)="toggleSort('revenueCagr')">
              Revenue CAGR {{ sortIndicator('revenueCagr') }}
            </th>
          </tr>
        </thead>
        <tbody>
          @for (row of items(); track row.companyId) {
            <tr [class]="rowHighlightClass(row.overallScore, row.computableChecks)">
              <td>
                <span class="score-badge" [class]="scoreBadgeClass(row.overallScore)">
                  {{ row.overallScore }}/{{ row.computableChecks }}
                </span>
              </td>
              <td>
                <a [routerLink]="['/company', row.cik, 'moat-scoring']" target="_blank">{{ row.companyName ?? ('CIK ' + row.cik) }}</a>
              </td>
              <td>{{ row.ticker ?? '' }}</td>
              <td>{{ row.exchange ?? '' }}</td>
              <td class="num">{{ fmtPrice(row.pricePerShare) }}</td>
              <td class="num">{{ fmtMarketCap(row.pricePerShare, row.sharesOutstanding) }}</td>
              <td class="num">{{ fmtPct(row.averageGrossMargin) }}</td>
              <td class="num">{{ fmtPct(row.averageOperatingMargin) }}</td>
              <td class="num">{{ fmtPct(row.averageRoeCF) }}</td>
              <td class="num">{{ fmtPct(row.averageRoeOE) }}</td>
              <td class="num">{{ fmtPct(row.estimatedReturnOE) }}</td>
              <td class="num">{{ fmtPct(row.revenueCagr) }}</td>
            </tr>
          }
        </tbody>
      </table>

      @if (pagination()) {
        <div class="pagination">
          <button [disabled]="page() <= 1" (click)="goToPage(page() - 1)">Previous</button>
          <span>Page {{ page() }} of {{ pagination()!.totalPages }} ({{ pagination()!.totalItems }} companies)</span>
          <button [disabled]="page() >= pagination()!.totalPages" (click)="goToPage(page() + 1)">Next</button>
        </div>
      }

      @if (computedAt()) {
        <p class="computed-at">Scores computed: {{ computedAt() }}</p>
      }
    }
  `,
  styles: [`
    .filters {
      display: flex;
      gap: 16px;
      margin-bottom: 16px;
      align-items: center;
    }
    .filters label {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 13px;
      font-weight: 500;
      color: #475569;
    }
    .filters select {
      padding: 4px 8px;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      background: #fff;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    th, td {
      text-align: left;
      padding: 4px 12px;
      border-bottom: 1px solid #e2e8f0;
    }
    th {
      background: #f1f5f9;
      font-weight: 600;
      white-space: nowrap;
    }
    .sortable {
      cursor: pointer;
      user-select: none;
    }
    .sortable:hover {
      background: #e2e8f0;
    }
    .num { text-align: right; }
    a {
      color: #3b82f6;
      text-decoration: none;
    }
    a:hover {
      text-decoration: underline;
    }
    .score-badge {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 8px;
      font-weight: 600;
      font-size: 12px;
      text-align: center;
      min-width: 40px;
    }
    .score-green { background: #dcfce7; color: #166534; }
    .score-yellow { background: #fef9c3; color: #854d0e; }
    .score-red { background: #fee2e2; color: #991b1b; }
    .pagination {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-top: 16px;
    }
    .pagination button {
      padding: 6px 12px;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      background: #fff;
      cursor: pointer;
    }
    .pagination button:disabled {
      opacity: 0.5;
      cursor: default;
    }
    .no-results {
      color: #64748b;
    }
    .row-perfect { background: #dcfce7; }
    .row-near-perfect { background: #fef9c3; }
    .positive { color: #16a34a; }
    .negative { color: #dc2626; }
    .error { color: #dc2626; }
    .computed-at {
      font-size: 12px;
      color: #94a3b8;
      margin-top: 12px;
    }
  `]
})
export class MoatScoresReportComponent implements OnInit {
  page = signal(1);
  pageSize = 50;
  sortBy = 'overallScore';
  sortDir = 'desc';
  minScore: number | null = null;
  exchange: string | null = null;

  items = signal<CompanyMoatScoreSummary[]>([]);
  pagination = signal<PaginationResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  computedAt = signal<string | null>(null);

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.fetchScores();
  }

  toggleSort(column: string): void {
    if (this.sortBy === column) {
      this.sortDir = this.sortDir === 'desc' ? 'asc' : 'desc';
    } else {
      this.sortBy = column;
      this.sortDir = 'desc';
    }
    this.page.set(1);
    this.fetchScores();
  }

  sortIndicator(column: string): string {
    if (this.sortBy !== column) return '';
    return this.sortDir === 'desc' ? '\u25BC' : '\u25B2';
  }

  onFilterChange(): void {
    this.page.set(1);
    this.fetchScores();
  }

  goToPage(p: number): void {
    this.page.set(p);
    this.fetchScores();
  }

  scoreBadgeClass(score: number): string {
    if (score >= 10) return 'score-green';
    if (score >= 7) return 'score-yellow';
    return 'score-red';
  }

  rowHighlightClass(score: number, computableChecks: number): string {
    if (score === computableChecks) return 'row-perfect';
    if (score === computableChecks - 1) return 'row-near-perfect';
    return '';
  }

  fmtPrice(val: number | null): string {
    if (val == null) return '';
    return '$' + val.toFixed(2);
  }

  fmtMarketCap(price: number | null, shares: number | null): string {
    if (price == null || shares == null) return '';
    return this.fmtCurrency(price * shares);
  }

  fmtCurrency(val: number | null): string {
    if (val == null) return '';
    const sign = val < 0 ? '-' : '';
    const abs = Math.abs(val);
    if (abs >= 1_000_000_000_000) return sign + '$' + (abs / 1_000_000_000_000).toFixed(2) + 'T';
    if (abs >= 1_000_000_000) return sign + '$' + (abs / 1_000_000_000).toFixed(2) + 'B';
    if (abs >= 1_000_000) return sign + '$' + (abs / 1_000_000).toFixed(2) + 'M';
    return sign + '$' + abs.toFixed(2);
  }

  fmtPct(val: number | null): string {
    if (val == null) return '';
    return val.toFixed(2) + '%';
  }

  private fetchScores(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.getMoatScoresReport({
      page: this.page(),
      pageSize: this.pageSize,
      sortBy: this.sortBy,
      sortDir: this.sortDir,
      minScore: this.minScore,
      exchange: this.exchange
    }).subscribe({
      next: data => {
        this.items.set(data.items);
        this.pagination.set(data.pagination);
        if (data.items.length > 0) {
          this.computedAt.set(data.items[0].computedAt);
        }
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load Buffett scores report.');
        this.loading.set(false);
      }
    });
  }
}
