import { Component, OnInit, computed, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import {
  ApiService,
  ArRevenueRow,
  CompanyDetail,
  MoatScoringResponse,
  MoatYearMetrics
} from '../../core/services/api.service';
import { computeSparkline, SparklineData } from '../../shared/sparkline.utils';

@Component({
  selector: 'app-moat-scoring',
  standalone: true,
  imports: [RouterLink, DecimalPipe],
  template: `
    <nav class="breadcrumb">
      <a routerLink="/dashboard">Home</a>
      <span class="sep">/</span>
      <a [routerLink]="['/company', cik]">{{ cik }}</a>
      <span class="sep">/</span>
      <span>Buffett Score</span>
    </nav>

    @if (company()) {
      <div class="company-header">
        <h2>{{ company()!.companyName ?? ('CIK ' + company()!.cik) }} — Buffett Score</h2>
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
        }
        <div class="company-links">
          <a [routerLink]="['/company', cik]" class="external-link">Filings</a>
          <a [routerLink]="['/company', cik, 'scoring']" class="external-link">Graham Score</a>
          @if (company()!.tickers.length > 0) {
            <a class="external-link" [href]="'https://finance.yahoo.com/quote/' + company()!.tickers[0].ticker" target="_blank" rel="noopener">Yahoo Finance</a>
            <a class="external-link" [href]="'https://www.google.com/finance/quote/' + company()!.tickers[0].ticker + ':' + company()!.tickers[0].exchange" target="_blank" rel="noopener">Google Finance</a>
          }
        </div>
      </div>
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
          @if (chart.sparkline) {
            <div class="sparkline-container">
              <svg viewBox="0 0 240 120" class="sparkline-svg">
                @for (tick of chart.sparkline.yTicks; track tick.label) {
                  <line [attr.x1]="chart.sparkline.axisLeft" [attr.y1]="tick.y"
                        [attr.x2]="chart.sparkline.axisRight" [attr.y2]="tick.y"
                        class="grid-line" />
                  <text [attr.x]="chart.sparkline.axisLeft - 4" [attr.y]="tick.y + 2.5"
                        text-anchor="end" class="axis-label">{{ tick.label }}</text>
                }
                <line [attr.x1]="chart.sparkline.axisLeft" [attr.y1]="chart.sparkline.axisTop"
                      [attr.x2]="chart.sparkline.axisLeft" [attr.y2]="chart.sparkline.axisBottom"
                      class="axis-line" />
                <line [attr.x1]="chart.sparkline.axisLeft" [attr.y1]="chart.sparkline.axisBottom"
                      [attr.x2]="chart.sparkline.axisRight" [attr.y2]="chart.sparkline.axisBottom"
                      class="axis-line" />
                <polyline
                  [attr.points]="chart.sparkline.polyline"
                  fill="none"
                  stroke="#3b82f6"
                  stroke-width="2"
                  stroke-linejoin="round"
                  stroke-linecap="round" />
                @for (pt of chart.sparkline.points; track pt.label) {
                  <circle [attr.cx]="pt.x" [attr.cy]="pt.y" r="3" fill="#3b82f6">
                    <title>{{ pt.label }}: {{ chart.formatTooltip(pt.value) }}</title>
                  </circle>
                  <text [attr.x]="pt.x" [attr.y]="chart.sparkline.axisBottom + 12"
                        text-anchor="middle" class="axis-label">{{ pt.label }}</text>
                }
              </svg>
            </div>
          } @else {
            <div class="sparkline-empty">Not enough data</div>
          }
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
    .sparkline-container {
      flex-shrink: 0;
      width: 300px;
      padding-top: 8px;
    }
    .sparkline-svg {
      width: 100%;
      height: auto;
    }
    .sparkline-empty {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 300px;
      height: 120px;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      color: #94a3b8;
      font-size: 13px;
    }
    .axis-line {
      stroke: #94a3b8;
      stroke-width: 1;
    }
    .grid-line {
      stroke: #e2e8f0;
      stroke-width: 0.5;
    }
    .axis-label {
      font-size: 6.5px;
      fill: #64748b;
    }
    .raw-table tbody tr:hover {
      background: #f1f5f9;
    }
    .info-icon {
      position: relative;
      cursor: help;
      font-size: 14px;
      color: #94a3b8;
      margin-left: 4px;
    }
    .info-icon:hover {
      color: #64748b;
    }
    .info-icon:hover::after {
      content: attr(data-tooltip);
      position: absolute;
      left: 50%;
      transform: translateX(-50%);
      top: 100%;
      margin-top: 6px;
      background: #1e293b;
      color: #f8fafc;
      padding: 6px 10px;
      border-radius: 6px;
      font-size: 12px;
      font-weight: 400;
      white-space: normal;
      width: 260px;
      z-index: 10;
      line-height: 1.4;
      pointer-events: none;
    }
  `]
})
export class MoatScoringComponent implements OnInit {
  cik = '';
  company = signal<CompanyDetail | null>(null);
  scoring = signal<MoatScoringResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  arRevenueRows = signal<ArRevenueRow[]>([]);

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
        display: m.revenue != null ? formatAbbrevStatic(m.revenue) : '\u2014'
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
      formatTooltip: (v: number) => formatAbbrevStatic(v)
    });

    // Net Current Assets (Working Capital)
    const raw = s.rawDataByYear;
    if (raw) {
      const years = Object.keys(raw).sort();
      const wcData: { label: string; value: number }[] = [];
      const wcDisplayRows: { label: string; display: string }[] = [];
      for (const yr of years) {
        const assets = raw[yr]['AssetsCurrent'] ?? null;
        const liabilities = raw[yr]['LiabilitiesCurrent'] ?? null;
        if (assets != null && liabilities != null) {
          const wc = assets - liabilities;
          wcDisplayRows.push({ label: yr, display: formatAbbrevStatic(wc) });
          wcData.push({ label: yr, value: wc });
        } else {
          wcDisplayRows.push({ label: yr, display: '\u2014' });
        }
      }
      if (wcDisplayRows.length > 0) {
        charts.push({
          title: 'Net Current Assets Trend',
          tooltip: 'Current Assets minus Current Liabilities (working capital). Positive values mean the company can cover short-term obligations.',
          columnHeader: 'Net Current Assets',
          rows: wcDisplayRows,
          sparkline: computeSparkline(wcData, { yAxisFormat: 'currency' }),
          formatTooltip: (v: number) => formatAbbrevStatic(v)
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
      { label: 'Avg Gross Margin', display: this.fmtPct(m.averageGrossMargin) },
      { label: 'Avg Operating Margin', display: this.fmtPct(m.averageOperatingMargin) },
      { label: 'Avg ROE (CF)', display: this.fmtPct(m.averageRoeCF) },
      { label: 'Avg ROE (OE)', display: this.fmtPct(m.averageRoeOE) },
      { label: 'Revenue CAGR', display: this.fmtPct(m.revenueCagr) },
      { label: 'CapEx Ratio', display: this.fmtPct(m.capexRatio) },
      { label: 'Interest Coverage', display: m.interestCoverage != null ? m.interestCoverage.toFixed(2) + 'x' : 'N/A' },
      { label: 'Debt / Equity', display: this.fmtRatio(m.debtToEquityRatio) },
      { label: 'Est. Return (OE)', display: this.fmtPct(m.estimatedReturnOE) },
      { label: 'Market Cap', display: this.fmtCurrency(m.marketCap) },
      { label: 'Current Dividends Paid', display: this.fmtCurrency(m.currentDividendsPaid) },
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

function formatAbbrevStatic(value: number): string {
  const abs = Math.abs(value);
  const sign = value < 0 ? '-' : '';
  if (abs >= 1e12) return sign + '$' + (abs / 1e12).toFixed(2) + 'T';
  if (abs >= 1e9) return sign + '$' + (abs / 1e9).toFixed(2) + 'B';
  if (abs >= 1e6) return sign + '$' + (abs / 1e6).toFixed(2) + 'M';
  if (abs >= 1e3) return sign + '$' + (abs / 1e3).toFixed(1) + 'K';
  return sign + '$' + abs.toFixed(0);
}
