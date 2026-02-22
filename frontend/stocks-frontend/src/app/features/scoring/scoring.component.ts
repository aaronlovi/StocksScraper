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
  templateUrl: './scoring.component.html',
  styleUrls: ['./scoring.component.css', '../../shared/styles/info-tooltip.css']
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
