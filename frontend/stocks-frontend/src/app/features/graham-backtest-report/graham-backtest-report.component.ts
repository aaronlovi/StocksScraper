import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  ApiService,
  GrahamBacktestPeriod,
  GrahamBacktestReport
} from '../../core/services/api.service';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { CsvExportButtonComponent } from '../../shared/components/csv-export-button/csv-export-button.component';
import { CsvColumn } from '../../shared/csv.utils';
import {
  fmtPrice as fmtPriceFn,
  fmtReturn as fmtReturnFn,
  fmtInvested as fmtInvestedFn,
  returnClass as returnClassFn
} from '../../shared/format.utils';

export interface BacktestChart {
  width: number;
  height: number;
  strategyPoints: string;
  benchmarkPoints: string | null;
  yTicks: { y: number; label: string }[];
  xLabels: { x: number; label: string; anchor: string }[];
}

@Component({
  selector: 'app-graham-backtest-report',
  standalone: true,
  imports: [RouterLink, FormsModule, LoadingOverlayComponent, CsvExportButtonComponent],
  templateUrl: './graham-backtest-report.component.html',
  styleUrls: [
    './graham-backtest-report.component.css',
    '../../shared/styles/report-table.css',
    '../../shared/styles/summary-section.css'
  ]
})
export class GrahamBacktestReportComponent implements OnInit {
  minScore = 15;
  interval: 'monthly' | 'weekly' = 'monthly';
  policy: 'all' | 'filing' | 'price' = 'all';
  confirm = false;

  report = signal<GrahamBacktestReport | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  expanded = signal<Set<number>>(new Set());

  chart = computed<BacktestChart | null>(() => buildBacktestChart(this.report()));

  // One flat row per holding per period, for CSV export
  holdingsRows = computed<Record<string, unknown>[]>(() => {
    const rpt = this.report();
    if (!rpt) return [];
    const rows: Record<string, unknown>[] = [];
    for (const period of rpt.periods) {
      for (const c of period.constituents) {
        rows.push({
          periodStart: period.startDate,
          periodEnd: period.endDate,
          ticker: c.ticker,
          companyName: c.companyName,
          cik: c.cik,
          exchange: c.exchange,
          buyPrice: c.startPrice,
          sellPrice: c.endPrice,
          periodReturnPct: c.periodReturnPct,
          entered: c.entered,
          enteredTrigger: c.enteredTrigger,
          left: c.left,
          leftTrigger: c.leftTrigger
        });
      }
    }
    return rows;
  });

  readonly periodsCsvColumns: CsvColumn[] = [
    { header: 'startDate', value: (p: GrahamBacktestPeriod) => p.startDate },
    { header: 'endDate', value: (p: GrahamBacktestPeriod) => p.endDate },
    { header: 'holdings', value: (p: GrahamBacktestPeriod) => p.constituentCount },
    { header: 'portfolioReturnPct', value: (p: GrahamBacktestPeriod) => p.portfolioReturnPct },
    { header: 'cumulativeValue', value: (p: GrahamBacktestPeriod) => p.cumulativeValue },
    { header: 'benchmarkReturnPct', value: (p: GrahamBacktestPeriod) => p.benchmarkReturnPct },
    { header: 'benchmarkCumulativeValue', value: (p: GrahamBacktestPeriod) => p.benchmarkCumulativeValue }
  ];

  readonly fmtPrice = (val: number | null | undefined) => fmtPriceFn(val, '');
  readonly fmtReturn = (val: number | null | undefined) => fmtReturnFn(val, '');
  readonly fmtInvested = (val: number | null | undefined) => fmtInvestedFn(val, '');
  readonly returnClass = returnClassFn;

  constructor(
    private api: ApiService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    if (params.get('interval') === 'weekly') {
      this.interval = 'weekly';
    }
    const policyParam = params.get('policy');
    if (policyParam === 'filing' || policyParam === 'price') {
      this.policy = policyParam;
    }
    if (params.get('confirm') === 'true') {
      this.confirm = true;
    }
    const minScoreParam = Number(params.get('minScore'));
    if (minScoreParam >= 13 && minScoreParam <= 15) {
      this.minScore = minScoreParam;
    }
    this.fetchBacktest();
  }

  onFilterChange(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { interval: this.interval, policy: this.policy, confirm: this.confirm, minScore: this.minScore },
      replaceUrl: true
    });
    this.fetchBacktest();
  }

  refresh(): void {
    this.fetchBacktest();
  }

  toggleExpand(index: number): void {
    const next = new Set(this.expanded());
    if (next.has(index)) {
      next.delete(index);
    } else {
      next.add(index);
    }
    this.expanded.set(next);
  }

  isExpanded(index: number): boolean {
    return this.expanded().has(index);
  }

  private fetchBacktest(): void {
    this.loading.set(true);
    this.error.set(null);
    this.expanded.set(new Set());

    this.api.getGrahamBacktest(this.minScore, this.interval, this.policy, this.confirm).subscribe({
      next: data => {
        this.report.set(data);
        this.loading.set(false);
      },
      error: err => {
        const message = err?.status === 404
          ? 'No score snapshots available. Run the snapshot backfill first.'
          : 'Failed to load backtest report.';
        this.error.set(message);
        this.loading.set(false);
      }
    });
  }
}

export function buildBacktestChart(report: GrahamBacktestReport | null): BacktestChart | null {
  if (!report || report.periods.length === 0) return null;

  const width = 820;
  const height = 280;
  const padLeft = 66;
  const padRight = 16;
  const padTop = 14;
  const padBottom = 30;

  const strategy: number[] = [1000];
  for (const p of report.periods) strategy.push(p.cumulativeValue);

  const hasBenchmark = report.summary.benchmarkFinalValue != null;
  const benchmark: number[] = [1000];
  if (hasBenchmark) {
    for (const p of report.periods) {
      benchmark.push(p.benchmarkCumulativeValue ?? benchmark[benchmark.length - 1]);
    }
  }

  let min = Number.MAX_VALUE;
  let max = -Number.MAX_VALUE;
  for (const v of strategy) {
    if (v < min) min = v;
    if (v > max) max = v;
  }
  if (hasBenchmark) {
    for (const v of benchmark) {
      if (v < min) min = v;
      if (v > max) max = v;
    }
  }
  if (max === min) max = min + 1;

  const n = strategy.length;
  const plotWidth = width - padLeft - padRight;
  const plotHeight = height - padTop - padBottom;
  const x = (i: number) => padLeft + (n === 1 ? 0 : (i * plotWidth) / (n - 1));
  const y = (v: number) => padTop + ((max - v) / (max - min)) * plotHeight;

  const strategyParts: string[] = [];
  for (let i = 0; i < strategy.length; i++) {
    strategyParts.push(`${x(i).toFixed(1)},${y(strategy[i]).toFixed(1)}`);
  }

  let benchmarkPoints: string | null = null;
  if (hasBenchmark) {
    const benchmarkParts: string[] = [];
    for (let i = 0; i < benchmark.length; i++) {
      benchmarkParts.push(`${x(i).toFixed(1)},${y(benchmark[i]).toFixed(1)}`);
    }
    benchmarkPoints = benchmarkParts.join(' ');
  }

  const fmtTick = (v: number) => '$' + Math.round(v).toLocaleString('en-US');
  const yTicks = [
    { y: y(max), label: fmtTick(max) },
    { y: y((max + min) / 2), label: fmtTick((max + min) / 2) },
    { y: y(min), label: fmtTick(min) }
  ];

  const xLabels = [
    { x: padLeft, label: report.summary.firstDate, anchor: 'start' },
    { x: width - padRight, label: report.summary.lastDate, anchor: 'end' }
  ];

  return {
    width,
    height,
    strategyPoints: strategyParts.join(' '),
    benchmarkPoints,
    yTicks,
    xLabels
  };
}
