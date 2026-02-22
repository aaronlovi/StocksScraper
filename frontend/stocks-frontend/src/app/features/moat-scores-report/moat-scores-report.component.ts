import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  ApiService,
  CompanyMoatScoreSummary,
  PaginationResponse
} from '../../core/services/api.service';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import {
  fmtCurrency as fmtCurrencyFn,
  fmtPct as fmtPctFn,
  fmtPrice as fmtPriceFn,
  scoreBadgeClass as scoreBadgeClassFn,
  rowHighlightClass as rowHighlightClassFn
} from '../../shared/format.utils';
import { SortState } from '../../shared/sort.utils';

@Component({
  selector: 'app-moat-scores-report',
  standalone: true,
  imports: [RouterLink, FormsModule, LoadingOverlayComponent, PaginationComponent],
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
            <th class="num">Market Cap</th>
            <th class="num sortable" (click)="toggleSort('averageGrossMargin')">
              Gross Margin {{ sort.indicator('averageGrossMargin') }}
            </th>
            <th class="num sortable" (click)="toggleSort('averageOperatingMargin')">
              Op. Margin {{ sort.indicator('averageOperatingMargin') }}
            </th>
            <th class="num sortable" (click)="toggleSort('averageRoeCF')">
              Avg ROE (CF) {{ sort.indicator('averageRoeCF') }}
            </th>
            <th class="num sortable" (click)="toggleSort('averageRoeOE')">
              Avg ROE (OE) {{ sort.indicator('averageRoeOE') }}
            </th>
            <th class="num sortable" (click)="toggleSort('estimatedReturnOE')">
              Est. Return (OE) {{ sort.indicator('estimatedReturnOE') }}
            </th>
            <th class="num sortable" (click)="toggleSort('revenueCagr')">
              Revenue CAGR {{ sort.indicator('revenueCagr') }}
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
        <app-pagination [page]="page()" [totalPages]="pagination()!.totalPages" [totalItems]="pagination()!.totalItems" (pageChange)="goToPage($event)" />
      }

      @if (computedAt()) {
        <p class="computed-at">Scores computed: {{ computedAt() }}</p>
      }
    }
  `,
  styleUrls: ['../../shared/styles/report-table.css']
})
export class MoatScoresReportComponent implements OnInit {
  page = signal(1);
  pageSize = 50;
  sort = new SortState('overallScore');
  minScore: number | null = null;
  exchange: string | null = null;

  items = signal<CompanyMoatScoreSummary[]>([]);
  pagination = signal<PaginationResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  computedAt = signal<string | null>(null);

  readonly fmtCurrency = (val: number | null | undefined) => fmtCurrencyFn(val, '');
  readonly fmtPct = (val: number | null | undefined) => fmtPctFn(val, '');
  readonly fmtPrice = (val: number | null | undefined) => fmtPriceFn(val, '');
  readonly scoreBadgeClass = scoreBadgeClassFn;
  readonly rowHighlightClass = rowHighlightClassFn;

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.fetchScores();
  }

  toggleSort(column: string): void {
    this.sort.toggle(column);
    this.page.set(1);
    this.fetchScores();
  }

  onFilterChange(): void {
    this.page.set(1);
    this.fetchScores();
  }

  goToPage(p: number): void {
    this.page.set(p);
    this.fetchScores();
  }

  fmtMarketCap(price: number | null, shares: number | null): string {
    if (price == null || shares == null) return '';
    return fmtCurrencyFn(price * shares, '');
  }

  private fetchScores(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.getMoatScoresReport({
      page: this.page(),
      pageSize: this.pageSize,
      sortBy: this.sort.sortBy,
      sortDir: this.sort.sortDir,
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
