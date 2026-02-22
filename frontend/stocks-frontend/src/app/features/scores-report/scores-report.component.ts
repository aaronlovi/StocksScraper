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
  templateUrl: './scores-report.component.html',
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
