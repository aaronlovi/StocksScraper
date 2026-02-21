import { Component, OnInit, computed, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ApiService,
  ArRevenueRow,
  CompanyDetail,
  SubmissionItem,
  StatementListItem
} from '../../core/services/api.service';

@Component({
  selector: 'app-company',
  standalone: true,
  imports: [RouterLink],
  template: `
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
        }
        <div class="company-links">
          <a [routerLink]="['/company', cik, 'scoring']" class="scoring-link">Value Score</a>
          @if (company()!.tickers.length > 0) {
            <a class="external-link" [href]="'https://finance.yahoo.com/quote/' + company()!.tickers[0].ticker" target="_blank" rel="noopener">Yahoo Finance</a>
            <a class="external-link" [href]="'https://www.google.com/finance/quote/' + company()!.tickers[0].ticker + ':' + company()!.tickers[0].exchange" target="_blank" rel="noopener">Google Finance</a>
          }
        </div>
      </div>

      @if (arRevenueRows().length > 0) {
        <div class="ar-revenue-section">
          <h3 class="collapsible-heading" (click)="arRevenueExpanded.set(!arRevenueExpanded())">
            <span class="collapse-indicator" [class.expanded]="arRevenueExpanded()">&#9654;</span>
            AR / Revenue Trend
          </h3>
          @if (arRevenueExpanded()) {
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
                    <td class="num">{{ row.ratio != null ? formatPct(row.ratio) : '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
            @if (sparklineData().points.length > 0) {
              <div class="sparkline-container">
                <svg viewBox="0 0 240 120" class="sparkline-svg">
                  <!-- Y-axis gridlines and labels -->
                  @for (tick of sparklineData().yTicks; track tick.label) {
                    <line [attr.x1]="sparklineData().axisLeft" [attr.y1]="tick.y"
                          [attr.x2]="sparklineData().axisRight" [attr.y2]="tick.y"
                          class="grid-line" />
                    <text [attr.x]="sparklineData().axisLeft - 4" [attr.y]="tick.y + 2.5"
                          text-anchor="end" class="axis-label">{{ tick.label }}</text>
                  }
                  <!-- Y-axis line -->
                  <line [attr.x1]="sparklineData().axisLeft" [attr.y1]="sparklineData().axisTop"
                        [attr.x2]="sparklineData().axisLeft" [attr.y2]="sparklineData().axisBottom"
                        class="axis-line" />
                  <!-- X-axis line -->
                  <line [attr.x1]="sparklineData().axisLeft" [attr.y1]="sparklineData().axisBottom"
                        [attr.x2]="sparklineData().axisRight" [attr.y2]="sparklineData().axisBottom"
                        class="axis-line" />
                  <!-- Data line -->
                  <polyline
                    [attr.points]="sparklineData().polyline"
                    fill="none"
                    stroke="#3b82f6"
                    stroke-width="2"
                    stroke-linejoin="round"
                    stroke-linecap="round" />
                  <!-- Data points and year labels -->
                  @for (pt of sparklineData().points; track pt.year) {
                    <circle [attr.cx]="pt.x" [attr.cy]="pt.y" r="3" fill="#3b82f6">
                      <title>{{ pt.year }}: {{ formatPct(pt.ratio) }}</title>
                    </circle>
                    <text [attr.x]="pt.x" [attr.y]="sparklineData().axisBottom + 12"
                          text-anchor="middle" class="axis-label">{{ pt.year }}</text>
                  }
                </svg>
              </div>
            }
          </div>
          }
        </div>
      }

      @if (submissions().length > 0) {
        <table>
          <thead>
            <tr>
              <th></th>
              <th>Report Date</th>
              <th>Filing Type</th>
              <th>Filing Category</th>
            </tr>
          </thead>
          <tbody>
            @for (sub of submissions(); track sub.submissionId) {
              <tr class="submission-row" (click)="toggleRow(sub.submissionId)">
                <td class="expand-cell">{{ expandedRow() === sub.submissionId ? '▾' : '▸' }}</td>
                <td>{{ sub.reportDate }}</td>
                <td>{{ sub.filingType }}</td>
                <td>{{ sub.filingCategory }}</td>
              </tr>
              @if (expandedRow() === sub.submissionId) {
                <tr class="detail-row">
                  <td colspan="4">
                    @if (statementsLoading()) {
                      <p class="loading-statements">Loading statements...</p>
                    } @else if (statements().length > 0) {
                      <table class="statement-table">
                        <thead>
                          <tr>
                            <th>Statement</th>
                            <th>Variant</th>
                          </tr>
                        </thead>
                        <tbody>
                          @for (st of statements(); track st.roleName) {
                            <tr>
                              <td>
                                <a [routerLink]="['/company', cik, 'report', sub.submissionId, st.rootConceptName]"
                                   [queryParams]="{ roleName: st.roleName }">
                                  {{ st.rootLabel }}
                                </a>
                              </td>
                              <td class="role-detail">{{ formatRoleName(st.roleName) }}</td>
                            </tr>
                          }
                        </tbody>
                      </table>
                    } @else {
                      <p class="no-statements">No statements available.</p>
                    }
                  </td>
                </tr>
              }
            }
          </tbody>
        </table>
      } @else {
        <p>No submissions found.</p>
      }
    } @else if (error()) {
      <p class="error">{{ error() }}</p>
    } @else {
      <p>Loading...</p>
    }
  `,
  styles: [`
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
    .cik-label {
      font-weight: 500;
    }
    .price-label {
      font-weight: 600;
      color: #059669;
    }
    .price-date {
      font-weight: 400;
      color: #94a3b8;
    }
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
    table {
      width: 100%;
      border-collapse: collapse;
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
    .submission-row {
      cursor: pointer;
    }
    .submission-row:hover {
      background: #f8fafc;
    }
    .expand-cell {
      width: 24px;
      text-align: center;
      color: #94a3b8;
    }
    .detail-row td {
      background: #f8fafc;
      padding: 6px 24px;
    }
    .statement-table {
      width: 100%;
      border-collapse: collapse;
      margin: 0;
    }
    .statement-table th {
      background: #e2e8f0;
      font-size: 12px;
      text-transform: uppercase;
      color: #475569;
    }
    .statement-table th, .statement-table td {
      padding: 2px 12px;
    }
    .statement-table td {
      border-bottom: 1px solid #e2e8f0;
    }
    .statement-table a {
      color: #3b82f6;
      text-decoration: none;
    }
    .statement-table a:hover {
      text-decoration: underline;
    }
    .role-detail {
      font-size: 13px;
      color: #64748b;
    }
    .loading-statements, .no-statements {
      color: #64748b;
      margin: 0;
    }
    .company-links {
      margin-top: 10px;
      display: flex;
      gap: 16px;
    }
    .scoring-link {
      color: #3b82f6;
      text-decoration: none;
      font-weight: 500;
      font-size: 14px;
    }
    .scoring-link:hover, .external-link:hover {
      text-decoration: underline;
    }
    .external-link {
      color: #3b82f6;
      text-decoration: none;
      font-weight: 500;
      font-size: 14px;
    }
    .ar-revenue-section {
      margin-bottom: 24px;
    }
    .collapsible-heading {
      font-size: 16px;
      margin-bottom: 8px;
      cursor: pointer;
      user-select: none;
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .collapsible-heading:hover {
      color: #3b82f6;
    }
    .collapse-indicator {
      font-size: 11px;
      color: #94a3b8;
      transition: transform 0.15s ease;
      display: inline-block;
    }
    .collapse-indicator.expanded {
      transform: rotate(90deg);
    }
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
    .sparkline-container {
      flex-shrink: 0;
      width: 300px;
      padding-top: 8px;
    }
    .sparkline-svg {
      width: 100%;
      height: auto;
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
    .error {
      color: #dc2626;
    }
  `]
})
export class CompanyComponent implements OnInit {
  cik = '';
  company = signal<CompanyDetail | null>(null);
  submissions = signal<SubmissionItem[]>([]);
  expandedRow = signal<number | null>(null);
  statements = signal<StatementListItem[]>([]);
  statementsLoading = signal(false);
  arRevenueRows = signal<ArRevenueRow[]>([]);
  arRevenueExpanded = signal(false);
  error = signal<string | null>(null);

  sparklineData = computed(() => {
    const rows = this.arRevenueRows();
    const chronological = [...rows].reverse();
    const withRatio: Array<{ year: number; ratio: number }> = [];
    for (const row of chronological) {
      if (row.ratio != null) {
        withRatio.push({ year: row.year, ratio: row.ratio });
      }
    }
    const empty = {
      polyline: '',
      points: [] as Array<{ x: number; y: number; year: number; ratio: number }>,
      yTicks: [] as Array<{ y: number; label: string }>,
      axisLeft: 0, axisRight: 0, axisTop: 0, axisBottom: 0
    };
    if (withRatio.length < 2) {
      return empty;
    }

    const padLeft = 35;
    const padRight = 10;
    const padTop = 10;
    const padBottom = 20;
    const width = 240;
    const height = 120;
    const plotW = width - padLeft - padRight;
    const plotH = height - padTop - padBottom;

    const minR = 0;
    let maxR = 0;
    for (const item of withRatio) {
      if (item.ratio > maxR) maxR = item.ratio;
    }
    if (maxR === 0) maxR = 0.1;

    // Round maxR up to a clean percentage for nice tick marks
    const maxPct = Math.ceil(maxR * 100);
    const niceMax = maxPct <= 5 ? maxPct : Math.ceil(maxPct / 5) * 5;
    const rangeR = niceMax / 100;

    // Generate y-axis ticks (0% to niceMax%, ~3-5 ticks)
    let tickStep = 1;
    if (niceMax > 10) tickStep = 5;
    if (niceMax > 30) tickStep = 10;
    const yTicks: Array<{ y: number; label: string }> = [];
    for (let pct = 0; pct <= niceMax; pct += tickStep) {
      const y = padTop + plotH - (pct / 100 / rangeR) * plotH;
      yTicks.push({ y: Math.round(y * 10) / 10, label: pct + '%' });
    }

    const points: Array<{ x: number; y: number; year: number; ratio: number }> = [];
    for (let i = 0; i < withRatio.length; i++) {
      const x = padLeft + (i / (withRatio.length - 1)) * plotW;
      const y = padTop + plotH - ((withRatio[i].ratio - minR) / rangeR) * plotH;
      points.push({ x: Math.round(x * 10) / 10, y: Math.round(y * 10) / 10, year: withRatio[i].year, ratio: withRatio[i].ratio });
    }

    let polyline = '';
    for (const pt of points) {
      if (polyline.length > 0) polyline += ' ';
      polyline += pt.x + ',' + pt.y;
    }

    return {
      polyline, points, yTicks,
      axisLeft: padLeft, axisRight: width - padRight,
      axisTop: padTop, axisBottom: padTop + plotH
    };
  });

  constructor(
    private route: ActivatedRoute,
    private api: ApiService,
    private titleService: Title
  ) {}

  ngOnInit(): void {
    this.cik = this.route.snapshot.paramMap.get('cik') ?? '';
    if (!this.cik) {
      this.error.set('No CIK provided.');
      return;
    }

    this.api.getCompany(this.cik).subscribe({
      next: data => {
        this.company.set(data);
        const ticker = data.tickers.length > 0 ? data.tickers[0].ticker : ('CIK ' + data.cik);
        this.titleService.setTitle('Stocks - ' + ticker);
      },
      error: () => this.error.set('Failed to load company.')
    });

    this.api.getSubmissions(this.cik).subscribe({
      next: data => this.submissions.set(data.items),
      error: () => this.error.set('Failed to load submissions.')
    });

    this.api.getArRevenue(this.cik).subscribe({
      next: data => this.arRevenueRows.set(data),
      error: () => {} // silently ignore — section just won't show
    });
  }

  formatRoleName(roleName: string): string {
    const match = roleName.match(/^\d+\s*-\s*Statement\s*-\s*(.+)$/);
    return match ? match[1] : roleName;
  }

  formatAbbrev(value: number): string {
    const abs = Math.abs(value);
    const sign = value < 0 ? '-' : '';
    if (abs >= 1e12) return sign + '$' + (abs / 1e12).toFixed(2) + 'T';
    if (abs >= 1e9) return sign + '$' + (abs / 1e9).toFixed(2) + 'B';
    if (abs >= 1e6) return sign + '$' + (abs / 1e6).toFixed(2) + 'M';
    if (abs >= 1e3) return sign + '$' + (abs / 1e3).toFixed(1) + 'K';
    return sign + '$' + abs.toFixed(0);
  }

  formatPct(value: number): string {
    return (value * 100).toFixed(1) + '%';
  }

  toggleRow(submissionId: number): void {
    if (this.expandedRow() === submissionId) {
      this.expandedRow.set(null);
      this.statements.set([]);
      return;
    }

    this.expandedRow.set(submissionId);
    this.statements.set([]);
    this.statementsLoading.set(true);

    this.api.listStatements(this.cik, submissionId).subscribe({
      next: data => {
        const sorted = [...data].sort((a, b) => {
          const labelCmp = a.rootLabel.localeCompare(b.rootLabel);
          if (labelCmp !== 0) return labelCmp;
          return this.formatRoleName(a.roleName).localeCompare(this.formatRoleName(b.roleName));
        });
        this.statements.set(sorted);
        this.statementsLoading.set(false);
      },
      error: () => {
        this.statements.set([]);
        this.statementsLoading.set(false);
      }
    });
  }
}
