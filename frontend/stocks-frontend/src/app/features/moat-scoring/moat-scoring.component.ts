import { Component, OnInit, computed, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import {
  ApiService,
  ArRevenueRow,
  CompanyDetail,
  MoatScoringResponse,
  MoatYearMetrics,
} from '../../core/services/api.service';
import { computeSparkline, SparklineData } from '../../shared/sparkline.utils';
import { SparklineChartComponent } from '../../shared/components/sparkline-chart/sparkline-chart.component';
import { fmtCurrency, fmtPct, fmtRatio, fmtPrice, formatAbbrev } from '../../shared/format.utils';
import { BreadcrumbComponent, BreadcrumbSegment } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyHeaderComponent, CompanyHeaderLink } from '../../shared/components/company-header/company-header.component';

@Component({
  selector: 'app-moat-scoring',
  standalone: true,
  imports: [DecimalPipe, SparklineChartComponent, BreadcrumbComponent, CompanyHeaderComponent],
  templateUrl: './moat-scoring.component.html',
  styleUrls: ['./moat-scoring.component.css', '../../shared/styles/info-tooltip.css']
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

  scoreBadge = computed(() => {
    const s = this.scoring();
    if (!s) return '';
    if (s.overallScore >= 10) return 'score-green';
    if (s.overallScore >= 7) return 'score-yellow';
    return 'score-red';
  });

  checkTooltips = computed<Record<number, string>>(() => {
    const s = this.scoring();
    if (!s) return {} as Record<number, string>;
    const m = s.metrics;
    const n = s.yearsOfData;
    return {
      1: 'Avg annual: Net Cash Flow / Equity (' + n + ' yrs) = ' + fmtPct(m.averageRoeCF),
      2: 'Avg annual: Owner Earnings / Equity (' + n + ' yrs) = ' + fmtPct(m.averageRoeOE),
      3: 'Avg annual: Gross Profit / Revenue (' + n + ' yrs) = ' + fmtPct(m.averageGrossMargin),
      4: 'Avg annual: Operating Income / Revenue (' + n + ' yrs) = ' + fmtPct(m.averageOperatingMargin),
      5: 'CAGR: (Latest Rev / Oldest Rev)^(1/' + n + ') − 1 = ' + fmtPct(m.revenueCagr),
      6: 'Owner Earnings > 0 in ' + m.positiveOeYears + ' of ' + m.totalOeYears + ' years',
      7: 'Avg CapEx / Avg Owner Earnings = ' + fmtPct(m.capexRatio),
      8: 'Dividends or buybacks in ' + m.capitalReturnYears + ' of ' + m.totalCapitalReturnYears + ' years',
      9: 'Debt / Equity = ' + fmtRatio(m.debtToEquityRatio),
      10: 'Operating Income / Interest Expense = ' + (m.interestCoverage != null ? m.interestCoverage.toFixed(2) + 'x' : 'N/A'),
      11: n + ' years of annual financial data available',
      12: '(Avg OE − Dividends) / Market Cap = ' + fmtPct(m.estimatedReturnOE),
      13: 'Same as #12 — checks return isn\'t unrealistically high',
    };
  });

  metricRows = computed<{ label: string; display: string; tooltip: string }[]>(() => {
    const m = this.scoring()?.metrics;
    const s = this.scoring();
    if (!m || !s) return [];
    const n = s.yearsOfData;
    const shares = formatAbbrev(s.sharesOutstanding).replace('$', '');
    return [
      { label: 'Avg Gross Margin', display: fmtPct(m.averageGrossMargin), tooltip: 'Avg annual: Gross Profit / Revenue (' + n + ' yrs)' },
      { label: 'Avg Operating Margin', display: fmtPct(m.averageOperatingMargin), tooltip: 'Avg annual: Operating Income / Revenue (' + n + ' yrs)' },
      { label: 'Avg ROE (CF)', display: fmtPct(m.averageRoeCF), tooltip: 'Avg annual: Net Cash Flow / Equity (' + n + ' yrs)' },
      { label: 'Avg ROE (OE)', display: fmtPct(m.averageRoeOE), tooltip: 'Avg annual: Owner Earnings / Equity (' + n + ' yrs)' },
      { label: 'Revenue CAGR', display: fmtPct(m.revenueCagr), tooltip: '(Latest Rev / Oldest Rev)^(1/' + n + ') − 1' },
      { label: 'CapEx Ratio', display: fmtPct(m.capexRatio), tooltip: 'Avg CapEx / Avg Owner Earnings' },
      { label: 'Interest Coverage', display: m.interestCoverage != null ? m.interestCoverage.toFixed(2) + 'x' : 'N/A', tooltip: 'Operating Income / Interest Expense (latest year)' },
      { label: 'Debt / Equity', display: fmtRatio(m.debtToEquityRatio), tooltip: 'Total Debt / Stockholders\' Equity' },
      { label: 'Est. Return (OE)', display: fmtPct(m.estimatedReturnOE), tooltip: '(Avg OE − Dividends) / Market Cap' },
      { label: 'Market Cap', display: fmtCurrency(m.marketCap), tooltip: 'Price × Shares = ' + fmtPrice(m.pricePerShare) + ' × ' + shares },
      { label: 'Current Dividends Paid', display: fmtCurrency(m.currentDividendsPaid), tooltip: 'Dividends from most recent fiscal year' },
      { label: 'Positive OE Years', display: m.positiveOeYears + ' / ' + m.totalOeYears, tooltip: 'Years where Owner Earnings > 0' },
      { label: 'Capital Return Years', display: m.capitalReturnYears + ' / ' + m.totalCapitalReturnYears, tooltip: 'Years with dividends or stock buybacks' },
    ];
  });

  yearKeys = computed<string[]>(() => {
    const raw = this.scoring()?.rawDataByYear;
    if (!raw) return [];
    return Object.keys(raw).sort().reverse();
  });

  rawRows = computed<{ concept: string; values: Record<string, number | null> }[]>(() => {
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
