import { Component, OnInit, signal } from '@angular/core';
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
          <h3>AR / Revenue Trend</h3>
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
    .ar-revenue-section h3 {
      font-size: 16px;
      margin-bottom: 8px;
    }
    .ar-revenue-table {
      width: auto;
      min-width: 400px;
    }
    .ar-revenue-table .num {
      text-align: right;
      font-variant-numeric: tabular-nums;
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
  error = signal<string | null>(null);

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
