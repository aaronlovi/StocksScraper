import { Component, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ApiService, DashboardStats } from '../../core/services/api.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DecimalPipe],
  template: `
    @if (stats()) {
      <h2>Dashboard</h2>
      <div class="cards">
        <div class="card">
          <div class="card-label">Total Companies</div>
          <div class="card-value">{{ stats()!.totalCompanies | number }}</div>
        </div>
        <div class="card">
          <div class="card-label">Total Filings</div>
          <div class="card-value">{{ stats()!.totalSubmissions | number }}</div>
        </div>
        <div class="card">
          <div class="card-label">Total Data Points</div>
          <div class="card-value">{{ stats()!.totalDataPoints | number }}</div>
        </div>
        <div class="card">
          <div class="card-label">Filing Date Range</div>
          <div class="card-value">{{ stats()!.earliestFilingDate ?? 'N/A' }} — {{ stats()!.latestFilingDate ?? 'N/A' }}</div>
        </div>
        <div class="card">
          <div class="card-label">Companies with Price Data</div>
          <div class="card-value">{{ stats()!.companiesWithPriceData | number }}</div>
        </div>
      </div>
    } @else if (error()) {
      <p class="error">{{ error() }}</p>
    } @else {
      <p>Loading...</p>
    }
  `,
  styles: [`
    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 16px;
      margin-top: 16px;
    }
    .card {
      background: #fff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 20px;
    }
    .card-label {
      font-size: 13px;
      color: #64748b;
      margin-bottom: 4px;
    }
    .card-value {
      font-size: 24px;
      font-weight: 600;
      color: #0f172a;
    }
    .error {
      color: #dc2626;
    }
  `]
})
export class DashboardComponent implements OnInit {
  stats = signal<DashboardStats | null>(null);
  error = signal<string | null>(null);

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.api.getDashboardStats().subscribe({
      next: data => this.stats.set(data),
      error: () => this.error.set('Failed to load dashboard stats.')
    });
  }
}
