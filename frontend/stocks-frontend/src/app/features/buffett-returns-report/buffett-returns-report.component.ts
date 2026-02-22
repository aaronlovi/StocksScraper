import { Component, OnInit, signal, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  ApiService,
  CompanyScoreReturnSummary,
  PaginationResponse
} from '../../core/services/api.service';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import {
  ReturnsSummary,
  computeReturnsSummary,
  defaultStartDate,
  yahooFinanceSP500Url,
  googleFinanceSP500Url
} from '../../shared/returns-summary.utils';
import {
  fmtPrice as fmtPriceFn,
  fmtReturn as fmtReturnFn,
  fmtInvested as fmtInvestedFn,
  returnClass as returnClassFn,
  scoreBadgeClass as scoreBadgeClassFn,
  rowHighlightClass as rowHighlightClassFn
} from '../../shared/format.utils';
import { SortState } from '../../shared/sort.utils';

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

@Component({
  selector: 'app-buffett-returns-report',
  standalone: true,
  imports: [RouterLink, FormsModule, LoadingOverlayComponent, PaginationComponent],
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
              Score {{ sort.indicator('overallScore') }}
            </th>
            <th>Company</th>
            <th>Ticker</th>
            <th>Exchange</th>
            <th class="num">Price</th>
            <th class="num sortable" (click)="toggleSort('totalReturnPct')">
              Total Return % {{ sort.indicator('totalReturnPct') }}
            </th>
            <th class="num sortable" (click)="toggleSort('annualizedReturnPct')">
              Annualized % {{ sort.indicator('annualizedReturnPct') }}
            </th>
            <th class="num sortable" (click)="toggleSort('currentValueOf1000')">
              $1,000 Invested {{ sort.indicator('currentValueOf1000') }}
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
        <app-pagination [page]="page()" [totalPages]="pagination()!.totalPages" [totalItems]="pagination()!.totalItems" (pageChange)="goToPage($event)" />
      }

      @if (computedAt()) {
        <p class="computed-at">Scores computed: {{ computedAt() }}</p>
      }
    }
  `,
  styles: [`
    .filters input[type="date"] {
      padding: 4px 8px;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      background: #fff;
    }
    tbody tr:hover {
      background: #f8fafc;
    }
  `],
  styleUrls: [
    '../../shared/styles/report-table.css',
    '../../shared/styles/summary-section.css'
  ]
})
export class BuffettReturnsReportComponent implements OnInit {
  presets = SCORE_PRESETS;
  selectedPresetIndex = 1; // default: 12/12+

  page = signal(1);
  pageSize = 50;
  sort = new SortState('overallScore');
  exchange: string | null = null;
  startDate: string = defaultStartDate();

  items = signal<CompanyScoreReturnSummary[]>([]);
  pagination = signal<PaginationResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  computedAt = signal<string | null>(null);

  summary = computed<ReturnsSummary | null>(() => computeReturnsSummary(this.items()));

  readonly fmtPrice = (val: number | null | undefined) => fmtPriceFn(val, '');
  readonly fmtReturn = (val: number | null | undefined) => fmtReturnFn(val, '');
  readonly fmtInvested = (val: number | null | undefined) => fmtInvestedFn(val, '');
  readonly returnClass = returnClassFn;
  readonly scoreBadgeClass = scoreBadgeClassFn;
  readonly rowHighlightClass = rowHighlightClassFn;

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.fetchReturns();
  }

  toggleSort(column: string): void {
    this.sort.toggle(column);
    this.page.set(1);
    this.fetchReturns();
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

  yahooFinanceUrl(): string {
    return yahooFinanceSP500Url(this.startDate);
  }

  googleFinanceUrl(): string {
    return googleFinanceSP500Url();
  }

  private fetchReturns(): void {
    this.loading.set(true);
    this.error.set(null);

    const preset = this.presets[this.selectedPresetIndex];
    this.api.getBuffettReturns({
      startDate: this.startDate,
      page: this.page(),
      pageSize: this.pageSize,
      sortBy: this.sort.sortBy,
      sortDir: this.sort.sortDir,
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
