import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  ApiService,
  PortfolioAdvisorReport,
  PortfolioRecommendation
} from '../../core/services/api.service';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import {
  fmtPrice as fmtPriceFn,
  scoreBadgeClass as scoreBadgeClassFn
} from '../../shared/format.utils';

const STORAGE_KEY = 'portfolio-advisor.tickers';

export function parseTickers(input: string): string[] {
  const tickers: string[] = [];
  const seen = new Set<string>();
  for (const line of input.split('\n')) {
    const trimmed = line.trim();
    if (trimmed.length === 0 || trimmed.startsWith('#')) continue;
    const token = trimmed.split(/[\s,;]+/)[0].toUpperCase();
    if (token.length === 0 || seen.has(token)) continue;
    seen.add(token);
    tickers.push(token);
  }
  return tickers;
}

@Component({
  selector: 'app-portfolio-advisor',
  standalone: true,
  imports: [RouterLink, FormsModule, LoadingOverlayComponent],
  templateUrl: './portfolio-advisor.component.html',
  styleUrls: [
    './portfolio-advisor.component.css',
    '../../shared/styles/report-table.css'
  ]
})
export class PortfolioAdvisorComponent implements OnInit {
  tickersInput = '';

  report = signal<PortfolioAdvisorReport | null>(null);
  loading = signal(false);
  error = signal<string | null>(null);

  readonly fmtPrice = (val: number | null | undefined) => fmtPriceFn(val, '');
  readonly scoreBadgeClass = scoreBadgeClassFn;

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      this.tickersInput = saved;
    }
  }

  analyze(): void {
    const tickers = parseTickers(this.tickersInput);
    localStorage.setItem(STORAGE_KEY, this.tickersInput);

    this.loading.set(true);
    this.error.set(null);
    this.report.set(null);

    this.api.getPortfolioAdvice(tickers).subscribe({
      next: data => {
        this.report.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load recommendations.');
        this.loading.set(false);
      }
    });
  }

  actionClass(rec: PortfolioRecommendation): string {
    return `action-badge action-${rec.action}`;
  }
}
