import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService, CompanySearchResult, PaginationResponse } from '../../core/services/api.service';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-search',
  standalone: true,
  imports: [FormsModule, RouterLink, PaginationComponent],
  templateUrl: './search.component.html',
  styleUrls: ['./search.component.css']
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
