import { Component, OnInit, computed, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import {
  ApiService,
  ArRevenueRow,
  CompanyDetail,
  ScoringResponse,
  ScoringCheckResponse
} from '../../core/services/api.service';
import { computeSparkline, SparklineData } from '../../shared/sparkline.utils';
import { SparklineChartComponent } from '../../shared/components/sparkline-chart/sparkline-chart.component';
import { fmtCurrency, fmtPct, fmtRatio, fmtPrice, formatAbbrev } from '../../shared/format.utils';
import { BreadcrumbComponent, BreadcrumbSegment } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyHeaderComponent, CompanyHeaderLink } from '../../shared/components/company-header/company-header.component';

@Component({
  selector: 'app-scoring',
  standalone: true,
  imports: [DecimalPipe, SparklineChartComponent, BreadcrumbComponent, CompanyHeaderComponent],
  template: `
    <app-breadcrumb [segments]="breadcrumbSegments" />

    @if (company()) {
      <app-company-header
        [company]="company()!"
        titleSuffix=" — Graham Score"
        [links]="headerLinks()" />
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
            <th class="result-cell">Result</th>
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

      @if (arRevenueRows().length > 0) {
        <h3>AR / Revenue Trend <span class="info-icon" data-tooltip="Accounts Receivable as a percentage of Revenue. Rising AR/Revenue may indicate customers are slower to pay or aggressive revenue recognition.">&#9432;</span></h3>
        <div class="ar-revenue-content">
          <table class="ar-revenue-table">
            <thead>
              <tr>
                <th>Year</th>
                <th class="num">AR</th>
                <th class="num">Revenue</th>
                <th class="num">AR / Revenue</th>
              </tr>
            </thead>
            <tbody>
              @for (row of arRevenueRows(); track row.year) {
                <tr>
                  <td>{{ row.year }}</td>
                  <td class="num">{{ row.accountsReceivable != null ? formatAbbrev(row.accountsReceivable) : '—' }}</td>
                  <td class="num">{{ row.revenue != null ? formatAbbrev(row.revenue) : '—' }}</td>
                  <td class="num">{{ row.ratio != null ? formatArPct(row.ratio) : '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
          <app-sparkline-chart [data]="sparklineData()" [formatTooltip]="formatArPctTooltip" />
        </div>
      }

      @if (workingCapital()) {
        <h3>{{ workingCapital()!.label }} Trend <span class="info-icon" [attr.data-tooltip]="workingCapital()!.tooltip">&#9432;</span></h3>
        <div class="ar-revenue-content">
          <table class="ar-revenue-table">
            <thead>
              <tr>
                <th>Year</th>
                <th class="num">{{ workingCapital()!.label }}</th>
              </tr>
            </thead>
            <tbody>
              @for (row of workingCapital()!.rows; track row.year) {
                <tr>
                  <td>{{ row.year }}</td>
                  <td class="num">{{ row.display }}</td>
                </tr>
              }
            </tbody>
          </table>
          <app-sparkline-chart [data]="workingCapital()!.sparkline" [formatTooltip]="formatAbbrevTooltip" />
        </div>
      }

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
    .scorecard-table tbody tr:hover { filter: brightness(0.95); }
    .result-cell { text-align: center; }
    .indicator.pass { color: #16a34a; font-weight: 700; }
    .indicator.fail { color: #dc2626; font-weight: 700; }
    .indicator.na { color: #94a3b8; }
    .metrics-table { max-width: 500px; }
    .metric-label { font-weight: 500; }
    .raw-table { font-size: 12px; }
    .raw-table th { font-size: 11px; text-transform: uppercase; }
    .error { color: #dc2626; }
    .ar-revenue-content {
      display: flex;
      align-items: flex-start;
      gap: 24px;
    }
    .ar-revenue-table {
      width: auto;
      min-width: 400px;
    }
    .ar-revenue-table .num {
      text-align: right;
      font-variant-numeric: tabular-nums;
    }
    .raw-table tbody tr:hover {
      background: #f1f5f9;
    }
  `],
  styleUrls: ['../../shared/styles/info-tooltip.css']
})
export class ScoringComponent implements OnInit {
  cik = '';
  breadcrumbSegments: BreadcrumbSegment[] = [];
  company = signal<CompanyDetail | null>(null);
  scoring = signal<ScoringResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  arRevenueRows = signal<ArRevenueRow[]>([]);

  headerLinks = computed<CompanyHeaderLink[]>(() => {
    const c = this.company();
    if (!c) return [];
    const links: CompanyHeaderLink[] = [
      { label: 'Filings', routerLink: '/company/' + this.cik },
      { label: 'Buffett Score', routerLink: '/company/' + this.cik + '/moat-scoring' },
    ];
    if (c.tickers.length > 0) {
      links.push({ label: 'Yahoo Finance', href: 'https://finance.yahoo.com/quote/' + c.tickers[0].ticker });
      links.push({ label: 'Google Finance', href: 'https://www.google.com/finance/quote/' + c.tickers[0].ticker + ':' + c.tickers[0].exchange });
    }
    return links;
  });

  readonly formatAbbrev = formatAbbrev;
  readonly formatArPctTooltip = (v: number) => v.toFixed(1) + '%';
  readonly formatArPct = (value: number) => (value * 100).toFixed(1) + '%';
  readonly formatAbbrevTooltip = (v: number) => formatAbbrev(v);

  workingCapital = computed(() => {
    const s = this.scoring();
    if (!s?.rawDataByYear) return null;
    const raw = s.rawDataByYear;
    const years = Object.keys(raw).sort();
    const sparkData: { label: string; value: number }[] = [];
    const rows: { year: string; display: string }[] = [];
    const hasCurrent = years.some(yr => raw[yr]['AssetsCurrent'] != null && raw[yr]['LiabilitiesCurrent'] != null);
    const assetsKey = hasCurrent ? 'AssetsCurrent' : 'Assets';
    const liabilitiesKey = hasCurrent ? 'LiabilitiesCurrent' : 'Liabilities';
    for (const yr of years) {
      const assets = raw[yr][assetsKey] ?? null;
      const liabilities = raw[yr][liabilitiesKey] ?? null;
      if (assets != null && liabilities != null) {
        const wc = assets - liabilities;
        rows.push({ year: yr, display: formatAbbrev(wc) });
        sparkData.push({ label: yr, value: wc });
      } else {
        rows.push({ year: yr, display: '\u2014' });
      }
    }
    rows.reverse();
    if (rows.length === 0) return null;
    const label = hasCurrent ? 'Net Assets (Current)' : 'Net Assets (Non-Current)';
    const tooltip = hasCurrent
      ? 'Current Assets minus Current Liabilities (working capital). Positive values mean the company can cover short-term obligations.'
      : 'Total Assets minus Total Liabilities (net assets). Falls back to totals when current/non-current breakdown is not reported.';
    return {
      label,
      tooltip,
      rows,
      sparkline: computeSparkline(sparkData, { yAxisFormat: 'currency', forceZero: !hasCurrent })
    };
  });

  sparklineData = computed<SparklineData | null>(() => {
    const rows = this.arRevenueRows();
    const chronological = [...rows].reverse();
    const sparkData: { label: string; value: number }[] = [];
    for (const row of chronological) {
      if (row.ratio != null) {
        sparkData.push({ label: '' + row.year, value: row.ratio * 100 });
      }
    }
    return computeSparkline(sparkData, { yAxisFormat: 'percent' });
  });

  constructor(
    private route: ActivatedRoute,
    private api: ApiService,
    private titleService: Title
  ) {}

  ngOnInit(): void {
    this.cik = this.route.snapshot.paramMap.get('cik') ?? '';
    this.breadcrumbSegments = [
      { label: 'Home', route: '/dashboard' },
      { label: this.cik, route: ['/company', this.cik] },
      { label: 'Graham Score' }
    ];
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

    this.api.getArRevenue(this.cik).subscribe({
      next: data => this.arRevenueRows.set(data),
      error: () => {}
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
      { label: 'Book Value', display: fmtCurrency(m.bookValue) },
      { label: 'Market Cap', display: fmtCurrency(m.marketCap) },
      { label: 'Debt / Equity', display: fmtRatio(m.debtToEquityRatio) },
      { label: 'Price / Book', display: fmtRatio(m.priceToBookRatio) },
      { label: 'Debt / Book', display: fmtRatio(m.debtToBookRatio) },
      { label: 'Adjusted Retained Earnings', display: fmtCurrency(m.adjustedRetainedEarnings) },
      { label: 'Oldest Retained Earnings', display: fmtCurrency(m.oldestRetainedEarnings) },
      { label: 'Avg Net Cash Flow', display: fmtCurrency(m.averageNetCashFlow) },
      { label: 'Avg Owner Earnings', display: fmtCurrency(m.averageOwnerEarnings) },
      { label: 'Avg ROE (CF)', display: fmtPct(m.averageRoeCF) },
      { label: 'Avg ROE (OE)', display: fmtPct(m.averageRoeOE) },
      { label: 'Est. Return (CF)', display: fmtPct(m.estimatedReturnCF) },
      { label: 'Est. Return (OE)', display: fmtPct(m.estimatedReturnOE) },
      { label: 'Current Dividends Paid', display: fmtCurrency(m.currentDividendsPaid) },
      { label: 'Max Buy Price', display: fmtPrice(this.scoring()!.maxBuyPrice) },
      { label: '% Upside', display: fmtPct(this.scoring()!.percentageUpside) },
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

}
