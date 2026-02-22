import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  ApiService,
  CompanyScoreSummary,
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
  selector: 'app-scores-report',
  standalone: true,
  imports: [RouterLink, FormsModule, LoadingOverlayComponent, PaginationComponent],
  template: `
    <h2>Company Graham Scores Report</h2>

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
            <th class="num sortable" (click)="toggleSort('maxBuyPrice')">
              Max Buy Price {{ sort.indicator('maxBuyPrice') }}
            </th>
            <th class="num sortable" (click)="toggleSort('percentageUpside')">
              % Upside {{ sort.indicator('percentageUpside') }}
            </th>
            <th class="num sortable" (click)="toggleSort('marketCap')">
              Market Cap {{ sort.indicator('marketCap') }}
            </th>
            <th class="num sortable" (click)="toggleSort('estimatedReturnCF')">
              Est. Return (CF) {{ sort.indicator('estimatedReturnCF') }}
            </th>
            <th class="num sortable" (click)="toggleSort('estimatedReturnOE')">
              Est. Return (OE) {{ sort.indicator('estimatedReturnOE') }}
            </th>
            <th class="num sortable" (click)="toggleSort('averageRoeCF')">
              Avg ROE (CF) {{ sort.indicator('averageRoeCF') }}
            </th>
            <th class="num sortable" (click)="toggleSort('averageRoeOE')">
              Avg ROE (OE) {{ sort.indicator('averageRoeOE') }}
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
                <a [routerLink]="['/company', row.cik, 'scoring']" target="_blank">{{ row.companyName ?? ('CIK ' + row.cik) }}</a>
              </td>
              <td>{{ row.ticker ?? '' }}</td>
              <td>{{ row.exchange ?? '' }}</td>
              <td class="num">{{ fmtPrice(row.pricePerShare) }}</td>
              <td class="num">{{ fmtPrice(row.maxBuyPrice) }}</td>
              <td class="num">{{ fmtPct(row.percentageUpside) }}</td>
              <td class="num">{{ fmtCurrency(row.marketCap) }}</td>
              <td class="num">{{ fmtPct(row.estimatedReturnCF) }}</td>
              <td class="num">{{ fmtPct(row.estimatedReturnOE) }}</td>
              <td class="num">{{ fmtPct(row.averageRoeCF) }}</td>
              <td class="num">{{ fmtPct(row.averageRoeOE) }}</td>
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
export class ScoresReportComponent implements OnInit {
  page = signal(1);
  pageSize = 50;
  sort = new SortState('overallScore');
  minScore: number | null = null;
  exchange: string | null = null;

  items = signal<CompanyScoreSummary[]>([]);
  pagination = signal<PaginationResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  computedAt = signal<string | null>(null);

  readonly fmtCurrency = (val: number | null | undefined) => fmtCurrencyFn(val, '');
  readonly fmtPct = (val: number | null | undefined) => fmtPctFn(val, '');
  readonly fmtPrice = (val: number | null | undefined) => fmtPriceFn(val, '');
  readonly scoreBadgeClass = scoreBadgeClassFn;
  readonly rowHighlightClass = (score: number, computableChecks: number) => rowHighlightClassFn(score, computableChecks, 15);

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

  private fetchScores(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.getScoresReport({
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
        this.error.set('Failed to load scores report.');
        this.loading.set(false);
      }
    });
  }
}
