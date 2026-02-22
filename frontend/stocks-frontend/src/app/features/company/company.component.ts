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
  templateUrl: './company.component.html',
  styleUrls: ['./company.component.css']
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
