import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService, CompanyDetail, StatementTreeNode } from '../../core/services/api.service';
import { TreeTableComponent } from '../../shared/components/tree-table/tree-table.component';

interface RoleOption {
  roleName: string;
  rootLabel: string;
}

@Component({
  selector: 'app-report',
  standalone: true,
  imports: [RouterLink, FormsModule, TreeTableComponent],
  template: `
    <nav class="breadcrumb">
      <a routerLink="/dashboard">Home</a>
      <span class="sep">/</span>
      <a [routerLink]="['/company', cik]">{{ cik }}</a>
      <span class="sep">/</span>
      <span>{{ concept }}</span>
    </nav>

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
      </div>
    }

    @if (availableRoles().length > 0) {
      <div class="role-picker">
        <p>Multiple presentation roles found. Please select one:</p>
        <ul>
          @for (role of availableRoles(); track role.roleName) {
            <li>
              <button (click)="selectRole(role.roleName)">{{ role.roleName }}</button>
            </li>
          }
        </ul>
      </div>
    } @else if (treeData()) {
      <div class="controls">
        <label>
          Taxonomy Year:
          <select [(ngModel)]="selectedYear" (ngModelChange)="onYearChange()">
            <option [ngValue]="null">Auto (from filing)</option>
            @for (y of yearOptions; track y) {
              <option [ngValue]="y">{{ y }}</option>
            }
          </select>
        </label>
        @if (selectedRole) {
          <span class="role-label">Role: {{ selectedRole }}</span>
        }
      </div>

      <app-tree-table [nodes]="treeNodes()" />
    } @else if (noData()) {
      <p class="no-data">No data reported for this statement in this filing.</p>
    } @else if (error()) {
      <p class="error">{{ error() }}</p>
    } @else {
      <p>Loading statement...</p>
    }
  `,
  styles: [`
    .breadcrumb {
      margin-bottom: 16px;
      font-size: 14px;
    }
    .breadcrumb a {
      color: #3b82f6;
      text-decoration: none;
    }
    .breadcrumb a:hover {
      text-decoration: underline;
    }
    .sep {
      margin: 0 6px;
      color: #94a3b8;
    }
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
    .controls {
      margin-bottom: 16px;
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .controls select {
      padding: 4px 8px;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      margin-left: 8px;
    }
    .role-label {
      font-size: 13px;
      color: #64748b;
    }
    .role-picker p {
      margin-bottom: 8px;
    }
    .role-picker ul {
      list-style: none;
      padding: 0;
    }
    .role-picker li {
      margin-bottom: 6px;
    }
    .role-picker button {
      padding: 6px 12px;
      background: #f1f5f9;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      cursor: pointer;
      text-align: left;
    }
    .role-picker button:hover {
      background: #e2e8f0;
    }
    .no-data {
      color: #64748b;
      font-style: italic;
    }
    .error {
      color: #dc2626;
    }
  `]
})
export class ReportComponent implements OnInit {
  cik = '';
  submissionId = 0;
  concept = '';
  selectedYear: number | null = null;
  selectedRole: string | null = null;
  yearOptions = Array.from({ length: 15 }, (_, i) => 2025 - i);

  company = signal<CompanyDetail | null>(null);
  treeData = signal<StatementTreeNode | null>(null);
  treeNodes = signal<StatementTreeNode[]>([]);
  availableRoles = signal<RoleOption[]>([]);
  noData = signal(false);
  error = signal<string | null>(null);

  constructor(
    private route: ActivatedRoute,
    private api: ApiService
  ) {}

  ngOnInit(): void {
    const params = this.route.snapshot.paramMap;
    this.cik = params.get('cik') ?? '';
    this.submissionId = parseInt(params.get('submissionId') ?? '0', 10);
    this.concept = params.get('concept') ?? '';

    const queryParams = this.route.snapshot.queryParamMap;
    const roleName = queryParams.get('roleName');
    if (roleName) {
      this.selectedRole = roleName;
    }

    this.api.getCompany(this.cik).subscribe({
      next: data => this.company.set(data)
    });

    this.loadStatement();
  }

  onYearChange(): void {
    this.treeData.set(null);
    this.treeNodes.set([]);
    this.noData.set(false);
    this.error.set(null);
    this.loadStatement();
  }

  selectRole(roleName: string): void {
    this.selectedRole = roleName;
    this.availableRoles.set([]);
    this.loadStatement();
  }

  private loadStatement(): void {
    this.api.getStatement(this.cik, this.submissionId, this.concept,
      this.selectedYear ?? undefined, this.selectedRole ?? undefined
    ).subscribe({
      next: data => {
        const isEmpty = !data || (!data.value && (!data.children || data.children.length === 0));
        if (isEmpty) {
          this.noData.set(true);
          return;
        }
        this.treeData.set(data);
        this.treeNodes.set(data.children ?? [data]);
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 300 && err.error?.roles) {
          this.availableRoles.set(err.error.roles);
        } else {
          this.error.set('Failed to load statement.');
        }
      }
    });
  }
}
