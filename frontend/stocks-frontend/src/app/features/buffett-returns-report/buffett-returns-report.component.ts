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
  templateUrl: './buffett-returns-report.component.html',
  styleUrls: [
    './buffett-returns-report.component.css',
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
