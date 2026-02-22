import { Component, OnInit, signal, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  ApiService,
  CompanyScoreReturnSummary,
  PaginationResponse
} from '../../core/services/api.service';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

function defaultStartDate(): string {
  const d = new Date();
  d.setMonth(d.getMonth() - 6);
  return d.toISOString().slice(0, 10);
}

interface ScorePreset {
  label: string;
  minScore: number | null;
  minChecks: number | null;
}

const SCORE_PRESETS: ScorePreset[] = [
  { label: 'All', minScore: null, minChecks: null },
  { label: '12/12+', minScore: 12, minChecks: null },
  { label: '12/13+', minScore: 12, minChecks: 13 },
  { label: '13/13', minScore: 13, minChecks: 13 },
];

interface ReturnsSummary {
  count: number;
  avgTotalReturn: number;
  medianTotalReturn: number;
  avgAnnualizedReturn: number | null;
  avgValueOf1000: number;
  bestTicker: string;
  bestReturn: number;
  worstTicker: string;
  worstReturn: number;
}

@Component({
  selector: 'app-buffett-returns-report',
  standalone: true,
  imports: [RouterLink, FormsModule, LoadingOverlayComponent],
  template: `
    <h2>Buffett Returns</h2>

    <div class="filters">
      <label>
        Score Filter
        <select [(ngModel)]="selectedPresetIndex" (ngModelChange)="onFilterChange()">
          @for (preset of presets; track preset.label; let i = $index) {
            <option [ngValue]="i">{{ preset.label }}</option>
          }
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
      <label>
        Start Date
        <input type="date" [ngModel]="startDate" (ngModelChange)="onStartDateChange($event)" />
      </label>
    </div>

    @if (loading()) {
      <app-loading-overlay />
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
            <th class="num sortable" (click)="toggleSort('totalReturnPct')">
              Total Return % {{ sortIndicator('totalReturnPct') }}
            </th>
            <th class="num sortable" (click)="toggleSort('annualizedReturnPct')">
              Annualized % {{ sortIndicator('annualizedReturnPct') }}
            </th>
            <th class="num sortable" (click)="toggleSort('currentValueOf1000')">
              $1,000 Invested {{ sortIndicator('currentValueOf1000') }}
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
              <td class="num" [class]="returnClass(row.totalReturnPct)">{{ fmtReturn(row.totalReturnPct) }}</td>
              <td class="num" [class]="returnClass(row.annualizedReturnPct)">{{ fmtReturn(row.annualizedReturnPct) }}</td>
              <td class="num">{{ fmtInvested(row.currentValueOf1000) }}</td>
            </tr>
          }
        </tbody>
      </table>

      @if (summary()) {
        <div class="summary">
          <h3>Summary</h3>
          <div class="summary-grid">
            <div class="summary-item">
              <span class="summary-label">Companies with return data</span>
              <span class="summary-value">{{ summary()!.count }} of {{ items().length }}</span>
            </div>
            <div class="summary-item">
              <span class="summary-label">Avg Total Return</span>
              <span class="summary-value" [class]="returnClass(summary()!.avgTotalReturn)">{{ fmtReturn(summary()!.avgTotalReturn) }}</span>
            </div>
            <div class="summary-item">
              <span class="summary-label">Median Total Return</span>
              <span class="summary-value" [class]="returnClass(summary()!.medianTotalReturn)">{{ fmtReturn(summary()!.medianTotalReturn) }}</span>
            </div>
            <div class="summary-item">
              <span class="summary-label">Avg Annualized Return</span>
              <span class="summary-value" [class]="returnClass(summary()!.avgAnnualizedReturn)">{{ fmtReturn(summary()!.avgAnnualizedReturn) }}</span>
            </div>
            <div class="summary-item">
              <span class="summary-label">Avg $1,000 Invested</span>
              <span class="summary-value">{{ fmtInvested(summary()!.avgValueOf1000) }}</span>
            </div>
            <div class="summary-item">
              <span class="summary-label">Best Performer</span>
              <span class="summary-value positive">{{ summary()!.bestTicker }} ({{ fmtReturn(summary()!.bestReturn) }})</span>
            </div>
            <div class="summary-item">
              <span class="summary-label">Worst Performer</span>
              <span class="summary-value negative">{{ summary()!.worstTicker }} ({{ fmtReturn(summary()!.worstReturn) }})</span>
            </div>
            <div class="summary-item">
              <span class="summary-label">S&amp;P 500 Benchmark</span>
              <span class="summary-value benchmark-links">
                <a [href]="yahooFinanceUrl()" target="_blank" rel="noopener">Yahoo Finance</a>
                <a [href]="googleFinanceUrl()" target="_blank" rel="noopener">Google Finance</a>
              </span>
            </div>
          </div>
        </div>
      }

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
    .filters select, .filters input[type="date"] {
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
    tbody tr:hover {
      background: #f8fafc;
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
    .summary {
      margin-top: 20px;
      padding: 16px;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
    }
    .summary h3 {
      margin: 0 0 12px 0;
      font-size: 14px;
      color: #334155;
    }
    .summary-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 12px;
    }
    .summary-item {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .summary-label {
      font-size: 11px;
      font-weight: 500;
      color: #64748b;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .summary-value {
      font-size: 16px;
      font-weight: 600;
      color: #1e293b;
    }
    .benchmark-links {
      display: flex;
      gap: 12px;
      font-size: 14px;
    }
    .benchmark-links a {
      color: #3b82f6;
      text-decoration: none;
      font-weight: 500;
    }
    .benchmark-links a:hover {
      text-decoration: underline;
    }
    .computed-at {
      font-size: 12px;
      color: #94a3b8;
      margin-top: 12px;
    }
  `]
})
export class BuffettReturnsReportComponent implements OnInit {
  presets = SCORE_PRESETS;
  selectedPresetIndex = 1; // default: 12/12+

  page = signal(1);
  pageSize = 50;
  sortBy = 'overallScore';
  sortDir = 'desc';
  exchange: string | null = null;
  startDate: string = defaultStartDate();

  items = signal<CompanyScoreReturnSummary[]>([]);
  pagination = signal<PaginationResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  computedAt = signal<string | null>(null);

  summary = computed<ReturnsSummary | null>(() => {
    const rows = this.items();
    const withData: CompanyScoreReturnSummary[] = [];
    for (const r of rows) {
      if (r.totalReturnPct != null && r.currentValueOf1000 != null && r.startDate && r.endDate) {
        withData.push(r);
      }
    }
    if (withData.length === 0) return null;

    let sumTotal = 0;
    let sumValue = 0;
    let sumDays = 0;
    let best = withData[0];
    let worst = withData[0];
    for (const r of withData) {
      sumTotal += r.totalReturnPct!;
      sumValue += r.currentValueOf1000!;
      const days = (new Date(r.endDate!).getTime() - new Date(r.startDate!).getTime()) / 86_400_000;
      sumDays += days;
      if (r.totalReturnPct! > best.totalReturnPct!) best = r;
      if (r.totalReturnPct! < worst.totalReturnPct!) worst = r;
    }

    const avgTotalReturn = sumTotal / withData.length;
    const avgDays = sumDays / withData.length;
    let avgAnnualizedReturn: number | null = null;
    if (avgDays > 0) {
      avgAnnualizedReturn = (Math.pow(1 + avgTotalReturn / 100, 365.25 / avgDays) - 1) * 100;
      avgAnnualizedReturn = Math.round(avgAnnualizedReturn * 100) / 100;
    }

    const sorted = withData.map(r => r.totalReturnPct!).sort((a, b) => a - b);
    const mid = Math.floor(sorted.length / 2);
    const median = sorted.length % 2 === 0
      ? (sorted[mid - 1] + sorted[mid]) / 2
      : sorted[mid];

    return {
      count: withData.length,
      avgTotalReturn: Math.round(avgTotalReturn * 100) / 100,
      medianTotalReturn: Math.round(median * 100) / 100,
      avgAnnualizedReturn: avgAnnualizedReturn,
      avgValueOf1000: Math.round(sumValue / withData.length * 100) / 100,
      bestTicker: best.ticker ?? best.cik,
      bestReturn: best.totalReturnPct!,
      worstTicker: worst.ticker ?? worst.cik,
      worstReturn: worst.totalReturnPct!,
    };
  });

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.fetchReturns();
  }

  toggleSort(column: string): void {
    if (this.sortBy === column) {
      this.sortDir = this.sortDir === 'desc' ? 'asc' : 'desc';
    } else {
      this.sortBy = column;
      this.sortDir = 'desc';
    }
    this.page.set(1);
    this.fetchReturns();
  }

  sortIndicator(column: string): string {
    if (this.sortBy !== column) return '';
    return this.sortDir === 'desc' ? '\u25BC' : '\u25B2';
  }

  onFilterChange(): void {
    this.page.set(1);
    this.fetchReturns();
  }

  onStartDateChange(value: string): void {
    this.startDate = value;
    this.page.set(1);
    this.fetchReturns();
  }

  goToPage(p: number): void {
    this.page.set(p);
    this.fetchReturns();
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

  fmtReturn(val: number | null): string {
    if (val == null) return '';
    const sign = val > 0 ? '+' : '';
    return sign + val.toFixed(2) + '%';
  }

  returnClass(val: number | null): string {
    if (val == null) return '';
    if (val > 0) return 'positive';
    if (val < 0) return 'negative';
    return '';
  }

  fmtInvested(val: number | null): string {
    if (val == null) return '';
    return '$' + val.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  yahooFinanceUrl(): string {
    const period1 = Math.floor(new Date(this.startDate + 'T00:00:00').getTime() / 1000);
    const period2 = Math.floor(Date.now() / 1000);
    return `https://finance.yahoo.com/quote/%5EGSPC/chart/?period1=${period1}&period2=${period2}`;
  }

  googleFinanceUrl(): string {
    return `https://www.google.com/finance/quote/.INX:INDEXSP`;
  }

  private fetchReturns(): void {
    this.loading.set(true);
    this.error.set(null);

    const preset = this.presets[this.selectedPresetIndex];
    this.api.getBuffettReturns({
      startDate: this.startDate,
      page: this.page(),
      pageSize: this.pageSize,
      sortBy: this.sortBy,
      sortDir: this.sortDir,
      minScore: preset.minScore,
      minChecks: preset.minChecks,
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
        this.error.set('Failed to load returns report.');
        this.loading.set(false);
      }
    });
  }
}
