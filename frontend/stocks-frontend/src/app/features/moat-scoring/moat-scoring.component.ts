import { Component, OnInit, computed, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import {
  ApiService,
  ArRevenueRow,
  CompanyDetail,
  MoatScoringResponse,
  MoatYearMetrics
} from '../../core/services/api.service';
import { computeSparkline, SparklineData } from '../../shared/sparkline.utils';
import { SparklineChartComponent } from '../../shared/components/sparkline-chart/sparkline-chart.component';
import { fmtCurrency, fmtPct, fmtRatio, formatAbbrev } from '../../shared/format.utils';
import { BreadcrumbComponent, BreadcrumbSegment } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyHeaderComponent, CompanyHeaderLink } from '../../shared/components/company-header/company-header.component';

@Component({
  selector: 'app-moat-scoring',
  standalone: true,
  imports: [DecimalPipe, SparklineChartComponent, BreadcrumbComponent, CompanyHeaderComponent],
  template: `
    <app-breadcrumb [segments]="breadcrumbSegments" />

    @if (company()) {
      <app-company-header
        [company]="company()!"
        titleSuffix=" — Buffett Score"
        [links]="headerLinks()" />
    }

    @if (loading()) {
      <p>Loading Buffett scoring data...</p>
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

      <!-- Trend Charts -->
      @for (chart of trendCharts(); track chart.title) {
        <h3>{{ chart.title }} <span class="info-icon" [attr.data-tooltip]="chart.tooltip">&#9432;</span></h3>
        <div class="trend-content">
          <table class="trend-table">
            <thead>
              <tr>
                <th>Year</th>
                <th class="num">{{ chart.columnHeader }}</th>
              </tr>
            </thead>
            <tbody>
              @for (row of chart.rows; track row.label) {
                <tr>
                  <td>{{ row.label }}</td>
                  <td class="num">{{ row.display }}</td>
                </tr>
              }
            </tbody>
          </table>
          <app-sparkline-chart [data]="chart.sparkline" [formatTooltip]="chart.formatTooltip" />
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
    .trend-content {
      display: flex;
      align-items: flex-start;
      gap: 24px;
    }
    .trend-table {
      width: auto;
      min-width: 200px;
    }
    .trend-table .num {
      text-align: right;
      font-variant-numeric: tabular-nums;
    }
    .raw-table tbody tr:hover {
      background: #f1f5f9;
    }
  `],
  styleUrls: ['../../shared/styles/info-tooltip.css']
})
export class MoatScoringComponent implements OnInit {
  cik = '';
  breadcrumbSegments: BreadcrumbSegment[] = [];
  company = signal<CompanyDetail | null>(null);
  scoring = signal<MoatScoringResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  arRevenueRows = signal<ArRevenueRow[]>([]);

  headerLinks = computed<CompanyHeaderLink[]>(() => {
    const c = this.company();
    if (!c) return [];
    const links: CompanyHeaderLink[] = [
      { label: 'Filings', routerLink: '/company/' + this.cik },
      { label: 'Graham Score', routerLink: '/company/' + this.cik + '/scoring' },
    ];
    if (c.tickers.length > 0) {
      links.push({ label: 'Yahoo Finance', href: 'https://finance.yahoo.com/quote/' + c.tickers[0].ticker });
      links.push({ label: 'Google Finance', href: 'https://www.google.com/finance/quote/' + c.tickers[0].ticker + ':' + c.tickers[0].exchange });
    }
    return links;
  });

  trendCharts = computed(() => {
    const s = this.scoring();
    const charts: TrendChart[] = [];

    // AR / Revenue %
    const arRows = this.arRevenueRows();
    const arChronological = [...arRows].reverse();
    const arData: { label: string; value: number }[] = [];
    const arDisplayRows: { label: string; display: string }[] = [];
    for (const row of arChronological) {
      arDisplayRows.push({
        label: '' + row.year,
        display: row.ratio != null ? (row.ratio * 100).toFixed(1) + '%' : '\u2014'
      });
      if (row.ratio != null) {
        arData.push({ label: '' + row.year, value: row.ratio * 100 });
      }
    }
    if (arDisplayRows.length > 0) {
      charts.push({
        title: 'AR / Revenue Trend',
        tooltip: 'Accounts Receivable as a percentage of Revenue. Rising AR/Revenue may indicate customers are slower to pay or aggressive revenue recognition.',
        columnHeader: 'AR / Revenue',
        rows: arDisplayRows,
        sparkline: computeSparkline(arData, { yAxisFormat: 'percent' }),
        formatTooltip: (v: number) => v.toFixed(1) + '%'
      });
    }

    if (!s) return charts;

    // Gross Margin %
    charts.push(buildPctChart(
      'Gross Margin Trend', 'Gross Margin',
      'Revenue minus Cost of Goods Sold, divided by Revenue. Measures pricing power and production efficiency.',
      s.trendData, m => m.grossMarginPct));

    // Operating Margin %
    charts.push(buildPctChart(
      'Operating Margin Trend', 'Op. Margin',
      'Operating Income divided by Revenue. Shows profitability after operating expenses but before interest and taxes.',
      s.trendData, m => m.operatingMarginPct));

    // ROE (CF) %
    charts.push(buildPctChart(
      'ROE (CF) Trend', 'ROE (CF)',
      'Return on Equity using Cash Flow. Net cash from operations divided by average stockholders\' equity.',
      s.trendData, m => m.roeCfPct));

    // ROE (OE) %
    charts.push(buildPctChart(
      'ROE (OE) Trend', 'ROE (OE)',
      'Return on Equity using Owner Earnings. Owner earnings (net income + depreciation - capex) divided by average stockholders\' equity.',
      s.trendData, m => m.roeOePct));

    // Revenue
    const revData: { label: string; value: number }[] = [];
    const revDisplayRows: { label: string; display: string }[] = [];
    for (const m of s.trendData) {
      revDisplayRows.push({
        label: '' + m.year,
        display: m.revenue != null ? formatAbbrev(m.revenue) : '\u2014'
      });
      if (m.revenue != null) {
        revData.push({ label: '' + m.year, value: m.revenue });
      }
    }
    charts.push({
      title: 'Revenue Trend',
      tooltip: 'Total revenue (net sales) reported each fiscal year. Consistent growth indicates a durable competitive advantage.',
      columnHeader: 'Revenue',
      rows: revDisplayRows,
      sparkline: computeSparkline(revData, { yAxisFormat: 'currency' }),
      formatTooltip: (v: number) => formatAbbrev(v)
    });

    // Net Current Assets (Working Capital)
    const raw = s.rawDataByYear;
    if (raw) {
      const years = Object.keys(raw).sort();
      const wcData: { label: string; value: number }[] = [];
      const wcDisplayRows: { label: string; display: string }[] = [];
      const hasCurrent = years.some(yr => raw[yr]['AssetsCurrent'] != null && raw[yr]['LiabilitiesCurrent'] != null);
      const assetsKey = hasCurrent ? 'AssetsCurrent' : 'Assets';
      const liabilitiesKey = hasCurrent ? 'LiabilitiesCurrent' : 'Liabilities';
      for (const yr of years) {
        const assets = raw[yr][assetsKey] ?? null;
        const liabilities = raw[yr][liabilitiesKey] ?? null;
        if (assets != null && liabilities != null) {
          const wc = assets - liabilities;
          wcDisplayRows.push({ label: yr, display: formatAbbrev(wc) });
          wcData.push({ label: yr, value: wc });
        } else {
          wcDisplayRows.push({ label: yr, display: '\u2014' });
        }
      }
      if (wcDisplayRows.length > 0) {
        const wcLabel = hasCurrent ? 'Net Assets (Current)' : 'Net Assets (Non-Current)';
        const wcTooltip = hasCurrent
          ? 'Current Assets minus Current Liabilities (working capital). Positive values mean the company can cover short-term obligations.'
          : 'Total Assets minus Total Liabilities (net assets). Falls back to totals when current/non-current breakdown is not reported.';
        charts.push({
          title: wcLabel + ' Trend',
          tooltip: wcTooltip,
          columnHeader: wcLabel,
          rows: wcDisplayRows,
          sparkline: computeSparkline(wcData, { yAxisFormat: 'currency', forceZero: !hasCurrent }),
          formatTooltip: (v: number) => formatAbbrev(v)
        });
      }
    }

    // Show newest year first in tables
    for (const chart of charts) {
      chart.rows.reverse();
    }

    return charts;
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
      { label: 'Buffett Score' }
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
        this.titleService.setTitle('Stocks - ' + ticker + ' Buffett');
      },
      error: () => {}
    });

    this.api.getMoatScoring(this.cik).subscribe({
      next: data => {
        this.scoring.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load Buffett scoring data.');
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
    if (threshold && threshold.includes('x')) return val.toFixed(2) + 'x';
    if (threshold && threshold.includes('year')) return '' + Math.round(val);
    if (threshold && threshold.includes('failing')) return '' + Math.round(val);
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
      { label: 'Avg Gross Margin', display: fmtPct(m.averageGrossMargin) },
      { label: 'Avg Operating Margin', display: fmtPct(m.averageOperatingMargin) },
      { label: 'Avg ROE (CF)', display: fmtPct(m.averageRoeCF) },
      { label: 'Avg ROE (OE)', display: fmtPct(m.averageRoeOE) },
      { label: 'Revenue CAGR', display: fmtPct(m.revenueCagr) },
      { label: 'CapEx Ratio', display: fmtPct(m.capexRatio) },
      { label: 'Interest Coverage', display: m.interestCoverage != null ? m.interestCoverage.toFixed(2) + 'x' : 'N/A' },
      { label: 'Debt / Equity', display: fmtRatio(m.debtToEquityRatio) },
      { label: 'Est. Return (OE)', display: fmtPct(m.estimatedReturnOE) },
      { label: 'Market Cap', display: fmtCurrency(m.marketCap) },
      { label: 'Current Dividends Paid', display: fmtCurrency(m.currentDividendsPaid) },
      { label: 'Positive OE Years', display: m.positiveOeYears + ' / ' + m.totalOeYears },
      { label: 'Capital Return Years', display: m.capitalReturnYears + ' / ' + m.totalCapitalReturnYears },
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

interface TrendChart {
  title: string;
  tooltip: string;
  columnHeader: string;
  rows: { label: string; display: string }[];
  sparkline: SparklineData | null;
  formatTooltip: (value: number) => string;
}

function buildPctChart(
  title: string,
  columnHeader: string,
  tooltip: string,
  trendData: MoatYearMetrics[],
  accessor: (m: MoatYearMetrics) => number | null
): TrendChart {
  const sparkData: { label: string; value: number }[] = [];
  const displayRows: { label: string; display: string }[] = [];
  for (const m of trendData) {
    const v = accessor(m);
    displayRows.push({
      label: '' + m.year,
      display: v != null ? v.toFixed(2) + '%' : '\u2014'
    });
    if (v != null) {
      sparkData.push({ label: '' + m.year, value: v });
    }
  }
  return {
    title,
    tooltip,
    columnHeader,
    rows: displayRows,
    sparkline: computeSparkline(sparkData, { yAxisFormat: 'percent' }),
    formatTooltip: (v: number) => v.toFixed(2) + '%'
  };
}

