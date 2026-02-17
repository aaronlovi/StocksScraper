import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ApiService,
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
        <h2>{{ company()!.cik }}</h2>
        @if (company()!.tickers.length > 0) {
          <div class="tickers">
            @for (t of company()!.tickers; track (t.ticker + t.exchange)) {
              <span class="badge">{{ t.ticker }}<span class="exchange">{{ t.exchange }}</span></span>
            }
          </div>
        }
      </div>

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
                      <ul class="statement-list">
                        @for (st of statements(); track st.rootConceptName) {
                          <li>
                            <a [routerLink]="['/company', cik, 'report', sub.submissionId, st.rootConceptName]">
                              {{ st.rootLabel }}
                            </a>
                          </li>
                        }
                      </ul>
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
      padding: 8px 12px;
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
      padding: 12px 24px;
    }
    .statement-list {
      list-style: none;
      padding: 0;
      margin: 0;
    }
    .statement-list li {
      padding: 4px 0;
    }
    .statement-list a {
      color: #3b82f6;
      text-decoration: none;
    }
    .statement-list a:hover {
      text-decoration: underline;
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

  constructor(
    private route: ActivatedRoute,
    private api: ApiService
  ) {}

  ngOnInit(): void {
    this.cik = this.route.snapshot.paramMap.get('cik') ?? '';
    if (!this.cik) {
      this.error.set('No CIK provided.');
      return;
    }

    this.api.getCompany(this.cik).subscribe({
      next: data => this.company.set(data),
      error: () => this.error.set('Failed to load company.')
    });

    this.api.getSubmissions(this.cik).subscribe({
      next: data => this.submissions.set(data.items),
      error: () => this.error.set('Failed to load submissions.')
    });
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
        this.statements.set(data);
        this.statementsLoading.set(false);
      },
      error: () => {
        this.statements.set([]);
        this.statementsLoading.set(false);
      }
    });
  }
}
