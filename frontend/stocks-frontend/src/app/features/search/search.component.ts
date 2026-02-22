import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService, CompanySearchResult, PaginationResponse } from '../../core/services/api.service';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-search',
  standalone: true,
  imports: [FormsModule, RouterLink, PaginationComponent],
  template: `
    <h2>Search Companies</h2>
    <div class="search-bar">
      <input
        type="text"
        placeholder="Search by name, ticker, or CIK..."
        [(ngModel)]="query"
        (keydown.enter)="doSearch()"
      />
      <button (click)="doSearch()">Search</button>
    </div>

    @if (results().length > 0) {
      <table>
        <thead>
          <tr>
            <th>Company Name</th>
            <th class="right">CIK</th>
            <th>Ticker</th>
            <th>Exchange</th>
            <th class="right">Latest Price ($)</th>
            <th>Price Date</th>
          </tr>
        </thead>
        <tbody>
          @for (r of results(); track r.companyId) {
            <tr>
              <td><a [routerLink]="['/company', r.cik]">{{ r.companyName }}</a></td>
              <td class="right">{{ r.cik }}</td>
              <td>{{ r.ticker ?? '' }}</td>
              <td>{{ r.exchange ?? '' }}</td>
              <td class="right">{{ r.latestPrice != null ? r.latestPrice.toFixed(2) : '—' }}</td>
              <td>{{ r.latestPriceDate ?? '—' }}</td>
            </tr>
          }
        </tbody>
      </table>

      @if (pagination()) {
        <app-pagination [page]="page()" [totalPages]="pagination()!.totalPages" (pageChange)="goToPage($event)" />
      }
    } @else if (searched() && results().length === 0) {
      <p class="no-results">No results found.</p>
    }
  `,
  styles: [`
    .search-bar {
      display: flex;
      gap: 8px;
      margin-bottom: 16px;
    }
    .search-bar input {
      flex: 1;
      padding: 8px 12px;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
    }
    .search-bar button {
      padding: 8px 16px;
      background: #3b82f6;
      color: #fff;
      border: none;
      border-radius: 4px;
      cursor: pointer;
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
    a {
      color: #3b82f6;
      text-decoration: none;
    }
    a:hover {
      text-decoration: underline;
    }
    .right {
      text-align: right;
    }
    .no-results {
      color: #64748b;
    }
  `]
})
export class SearchComponent implements OnInit {
  query = '';
  page = signal(1);
  pageSize = 25;
  results = signal<CompanySearchResult[]>([]);
  pagination = signal<PaginationResponse | null>(null);
  searched = signal(false);

  constructor(
    private api: ApiService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const q = params['q'] ?? '';
      const p = parseInt(params['page'] ?? '1', 10);
      if (q) {
        this.query = q;
        this.page.set(p);
        this.fetchResults();
      }
    });
  }

  doSearch(): void {
    this.page.set(1);
    this.router.navigate([], {
      queryParams: { q: this.query, page: 1 },
      queryParamsHandling: 'merge'
    });
    this.fetchResults();
  }

  goToPage(p: number): void {
    this.page.set(p);
    this.router.navigate([], {
      queryParams: { page: p },
      queryParamsHandling: 'merge'
    });
    this.fetchResults();
  }

  private fetchResults(): void {
    this.api.searchCompanies(this.query, this.page(), this.pageSize).subscribe({
      next: data => {
        this.results.set(data.items);
        this.pagination.set(data.pagination);
        this.searched.set(true);
      }
    });
  }
}
