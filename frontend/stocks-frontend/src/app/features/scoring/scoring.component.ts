import { Component, OnInit, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import {
  ApiService,
  CompanyDetail,
  ScoringResponse,
  ScoringCheckResponse
} from '../../core/services/api.service';

@Component({
  selector: 'app-scoring',
  standalone: true,
  imports: [RouterLink, DecimalPipe],
  template: `
    <nav class="breadcrumb">
      <a routerLink="/dashboard">Home</a>
      <span class="sep">/</span>
      <a [routerLink]="['/company', cik]">{{ cik }}</a>
      <span class="sep">/</span>
      <span>Value Score</span>
    </nav>

    @if (company()) {
      <div class="company-header">
        <h2>{{ company()!.companyName ?? ('CIK ' + company()!.cik) }}</h2>
        <div class="company-subtitle">
          <span class="cik-label">CIK {{ company()!.cik }}</span>
          @if (company()!.latestPrice != null) {
            <span class="price-label">\${{ company()!.latestPrice!.toFixed(2) }}</span>
            @if (company()!.latestPriceDate) {
              <span class="price-date">as of {{ company()!.latestPriceDate }}</span>
            }
          }
        </div>
        @if (company()!.tickers.length > 0) {
          <div class="tickers">
            @for (t of company()!.tickers; track (t.ticker + t.exchange)) {
              <span class="badge">{{ t.ticker }}<span class="exchange">{{ t.exchange }}</span></span>
            }
          </div>
          <div class="company-links">
            <a class="external-link" [href]="'https://finance.yahoo.com/quote/' + company()!.tickers[0].ticker" target="_blank" rel="noopener">Yahoo Finance</a>
            <a class="external-link" [href]="'https://www.google.com/finance/quote/' + company()!.tickers[0].ticker + ':' + company()!.tickers[0].exchange" target="_blank" rel="noopener">Google Finance</a>
          </div>
        }
      </div>
    }

    @if (loading()) {
      <p>Loading scoring data...</p>
    } @else if (error()) {
      <p class="error">{{ error() }}</p>
    } @else if (scoring()) {
      <div class="score-summary" [class]="scoreBadgeClass()">
        <span class="score-value">{{ scoring()!.overallScore }}</span>
        <span class="score-sep">/</span>
        <span class="score-total">{{ scoring()!.computableChecks }}</span>
      </div>
      <p class="score-caption">
        {{ scoring()!.yearsOfData }} year{{ scoring()!.yearsOfData === 1 ? '' : 's' }} of data
        @if (scoring()!.pricePerShare != null) {
          &middot; Price: \${{ scoring()!.pricePerShare! | number:'1.2-2' }}
          @if (scoring()!.priceDate) {
            ({{ scoring()!.priceDate }})
          }
        }
        @if (scoring()!.sharesOutstanding != null) {
          &middot; Shares: {{ scoring()!.sharesOutstanding! | number:'1.0-0' }}
        }
      </p>

      <h3>Scorecard</h3>
      <table class="scorecard-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Check</th>
            <th>Value</th>
            <th>Threshold</th>
            <th>Result</th>
          </tr>
        </thead>
        <tbody>
          @for (check of scoring()!.scorecard; track check.checkNumber) {
            <tr [class]="'check-' + check.result">
              <td>{{ check.checkNumber }}</td>
              <td>{{ check.name }}</td>
              <td class="num">{{ formatValue(check.computedValue, check.threshold) }}</td>
              <td>{{ check.threshold }}</td>
              <td class="result-cell">
                @if (check.result === 'pass') {
                  <span class="indicator pass">&#10003;</span>
                } @else if (check.result === 'fail') {
                  <span class="indicator fail">&#10007;</span>
                } @else {
                  <span class="indicator na">&mdash;</span>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>

      <h3>Derived Metrics</h3>
      <table class="metrics-table">
        <tbody>
          @for (m of metricRows(); track m.label) {
            <tr>
              <td class="metric-label">{{ m.label }}</td>
              <td class="num">{{ m.display }}</td>
            </tr>
          }
        </tbody>
      </table>

      @if (yearKeys().length > 0) {
        <h3>Raw Data</h3>
        <table class="raw-table">
          <thead>
            <tr>
              <th>Concept</th>
              @for (yr of yearKeys(); track yr) {
                <th class="num">{{ yr }}</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (row of rawRows(); track row.concept) {
              <tr>
                <td>{{ row.concept }}</td>
                @for (yr of yearKeys(); track yr) {
                  <td class="num">{{ row.values[yr] != null ? (row.values[yr]! | number:'1.0-0') : '' }}</td>
                }
              </tr>
            }
          </tbody>
        </table>
      }
    }
  `,
  styles: [`
    .breadcrumb {
      font-size: 13px;
      margin-bottom: 12px;
      color: #64748b;
    }
    .breadcrumb a {
      color: #3b82f6;
      text-decoration: none;
    }
    .breadcrumb a:hover {
      text-decoration: underline;
    }
    .sep {
      margin: 0 6px;
    }
    .company-header {
      margin-bottom: 16px;
    }
    .company-subtitle {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-top: 4px;
      font-size: 14px;
      color: #64748b;
    }
    .cik-label { font-weight: 500; }
    .price-label { font-weight: 600; color: #059669; }
    .price-date { font-weight: 400; color: #94a3b8; }
    .tickers {
      display: flex;
      gap: 8px;
      margin-top: 8px;
    }
    .badge {
      background: #3b82f6;
      color: #fff;
      padding: 4px 10px;
      border-radius: 12px;
      font-size: 13px;
      font-weight: 600;
    }
    .badge .exchange {
      margin-left: 4px;
      font-weight: 400;
      opacity: 0.8;
    }
    .company-links {
      margin-top: 10px;
      display: flex;
      gap: 16px;
    }
    .external-link {
      color: #3b82f6;
      text-decoration: none;
      font-weight: 500;
      font-size: 14px;
    }
    .external-link:hover {
      text-decoration: underline;
    }
    .score-summary {
      display: inline-flex;
      align-items: baseline;
      gap: 4px;
      padding: 12px 24px;
      border-radius: 12px;
      margin: 16px 0 4px;
    }
    .score-summary.score-green { background: #dcfce7; color: #166534; }
    .score-summary.score-yellow { background: #fef9c3; color: #854d0e; }
    .score-summary.score-red { background: #fee2e2; color: #991b1b; }
    .score-value { font-size: 36px; font-weight: 700; }
    .score-sep { font-size: 24px; font-weight: 400; opacity: 0.5; }
    .score-total { font-size: 24px; font-weight: 400; }
    .score-caption {
      font-size: 13px;
      color: #64748b;
      margin: 4px 0 20px;
    }
    h3 {
      margin: 24px 0 8px;
      font-size: 16px;
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
    }
    .num { text-align: right; }
    .scorecard-table .check-pass { background: #f0fdf4; }
    .scorecard-table .check-fail { background: #fef2f2; }
    .scorecard-table .check-na { background: #f8fafc; }
    .result-cell { text-align: center; }
    .indicator.pass { color: #16a34a; font-weight: 700; }
    .indicator.fail { color: #dc2626; font-weight: 700; }
    .indicator.na { color: #94a3b8; }
    .metrics-table { max-width: 500px; }
    .metric-label { font-weight: 500; }
    .raw-table { font-size: 12px; }
    .raw-table th { font-size: 11px; text-transform: uppercase; }
    .error { color: #dc2626; }
  `]
})
export class ScoringComponent implements OnInit {
  cik = '';
  company = signal<CompanyDetail | null>(null);
  scoring = signal<ScoringResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);

  constructor(
    private route: ActivatedRoute,
    private api: ApiService,
    private titleService: Title
  ) {}

  ngOnInit(): void {
    this.cik = this.route.snapshot.paramMap.get('cik') ?? '';
    if (!this.cik) {
      this.loading.set(false);
      this.error.set('No CIK provided.');
      return;
    }

    this.api.getCompany(this.cik).subscribe({
      next: data => {
        this.company.set(data);
        const ticker = data.tickers.length > 0 ? data.tickers[0].ticker : ('CIK ' + data.cik);
        this.titleService.setTitle('Stocks - ' + ticker);
      },
      error: () => {}
    });

    this.api.getScoring(this.cik).subscribe({
      next: data => {
        this.scoring.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load scoring data.');
        this.loading.set(false);
      }
    });
  }

  scoreBadgeClass(): string {
    const s = this.scoring();
    if (!s) return '';
    if (s.overallScore >= 10) return 'score-green';
    if (s.overallScore >= 7) return 'score-yellow';
    return 'score-red';
  }

  formatValue(val: number | null, threshold?: string): string {
    if (val == null) return '';
    if (threshold && threshold.includes('%')) return val.toFixed(2) + '%';
    if (Math.abs(val) >= 1_000_000_000_000) return (val / 1_000_000_000_000).toFixed(2) + 'T';
    if (Math.abs(val) >= 1_000_000_000) return (val / 1_000_000_000).toFixed(2) + 'B';
    if (Math.abs(val) >= 1_000_000) return (val / 1_000_000).toFixed(2) + 'M';
    if (Math.abs(val) >= 1_000) return (val / 1_000).toFixed(2) + 'K';
    return val.toFixed(4);
  }

  metricRows(): { label: string; display: string }[] {
    const m = this.scoring()?.metrics;
    if (!m) return [];
    return [
      { label: 'Book Value', display: this.fmtCurrency(m.bookValue) },
      { label: 'Market Cap', display: this.fmtCurrency(m.marketCap) },
      { label: 'Debt / Equity', display: this.fmtRatio(m.debtToEquityRatio) },
      { label: 'Price / Book', display: this.fmtRatio(m.priceToBookRatio) },
      { label: 'Debt / Book', display: this.fmtRatio(m.debtToBookRatio) },
      { label: 'Adjusted Retained Earnings', display: this.fmtCurrency(m.adjustedRetainedEarnings) },
      { label: 'Oldest Retained Earnings', display: this.fmtCurrency(m.oldestRetainedEarnings) },
      { label: 'Avg Net Cash Flow', display: this.fmtCurrency(m.averageNetCashFlow) },
      { label: 'Avg Owner Earnings', display: this.fmtCurrency(m.averageOwnerEarnings) },
      { label: 'Est. Return (CF)', display: this.fmtPct(m.estimatedReturnCF) },
      { label: 'Est. Return (OE)', display: this.fmtPct(m.estimatedReturnOE) },
      { label: 'Current Dividends Paid', display: this.fmtCurrency(m.currentDividendsPaid) },
      { label: 'Max Buy Price', display: this.fmtPrice(this.scoring()!.maxBuyPrice) },
      { label: '% Upside', display: this.fmtPct(this.scoring()!.percentageUpside) },
    ];
  }

  yearKeys(): string[] {
    const raw = this.scoring()?.rawDataByYear;
    if (!raw) return [];
    return Object.keys(raw).sort().reverse();
  }

  rawRows(): { concept: string; values: Record<string, number | null> }[] {
    const raw = this.scoring()?.rawDataByYear;
    if (!raw) return [];
    const years = this.yearKeys();
    const conceptSet = new Set<string>();
    for (const yr of years) {
      for (const key of Object.keys(raw[yr])) {
        conceptSet.add(key);
      }
    }
    const concepts = Array.from(conceptSet).sort();
    return concepts.map(concept => {
      const values: Record<string, number | null> = {};
      for (const yr of years) {
        values[yr] = raw[yr][concept] ?? null;
      }
      return { concept, values };
    });
  }

  private fmtPrice(val: number | null | undefined): string {
    if (val == null) return 'N/A';
    return '$' + val.toFixed(2);
  }

  private fmtCurrency(val: number | null | undefined): string {
    if (val == null) return 'N/A';
    const sign = val < 0 ? '-' : '';
    const abs = Math.abs(val);
    if (abs >= 1_000_000_000_000) return sign + '$' + (abs / 1_000_000_000_000).toFixed(2) + 'T';
    if (abs >= 1_000_000_000) return sign + '$' + (abs / 1_000_000_000).toFixed(2) + 'B';
    if (abs >= 1_000_000) return sign + '$' + (abs / 1_000_000).toFixed(2) + 'M';
    return sign + '$' + abs.toFixed(2);
  }

  private fmtRatio(val: number | null | undefined): string {
    if (val == null) return 'N/A';
    return val.toFixed(2);
  }

  private fmtPct(val: number | null | undefined): string {
    if (val == null) return 'N/A';
    return val.toFixed(2) + '%';
  }
}
