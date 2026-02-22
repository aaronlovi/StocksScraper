import { Component, OnInit, computed, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ApiService,
  CompanyDetail,
  SubmissionItem,
  StatementListItem
} from '../../core/services/api.service';
import { CompanyHeaderComponent, CompanyHeaderLink } from '../../shared/components/company-header/company-header.component';

@Component({
  selector: 'app-company',
  standalone: true,
  imports: [RouterLink, CompanyHeaderComponent],
  template: `
    @if (company()) {
      <app-company-header
        [company]="company()!"
        titleSuffix=" — Filings"
        [links]="headerLinks()" />

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
  error = signal<string | null>(null);

  headerLinks = computed<CompanyHeaderLink[]>(() => {
    const c = this.company();
    if (!c) return [];
    const links: CompanyHeaderLink[] = [
      { label: 'Graham Score', routerLink: '/company/' + this.cik + '/scoring' },
      { label: 'Buffett Score', routerLink: '/company/' + this.cik + '/moat-scoring' },
    ];
    if (c.tickers.length > 0) {
      links.push({ label: 'Yahoo Finance', href: 'https://finance.yahoo.com/quote/' + c.tickers[0].ticker });
      links.push({ label: 'Google Finance', href: 'https://www.google.com/finance/quote/' + c.tickers[0].ticker + ':' + c.tickers[0].exchange });
    }
    return links;
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
  }

  formatRoleName(roleName: string): string {
    const match = roleName.match(/^\d+\s*-\s*Statement\s*-\s*(.+)$/);
    return match ? match[1] : roleName;
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
