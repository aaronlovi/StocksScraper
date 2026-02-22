import { Component, OnInit, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService, CompanyDetail, StatementTreeNode } from '../../core/services/api.service';
import { TreeTableComponent } from '../../shared/components/tree-table/tree-table.component';
import { BreadcrumbComponent, BreadcrumbSegment } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyHeaderComponent } from '../../shared/components/company-header/company-header.component';

interface RoleOption {
  roleName: string;
  rootLabel: string;
}

@Component({
  selector: 'app-report',
  standalone: true,
  imports: [FormsModule, TreeTableComponent, BreadcrumbComponent, CompanyHeaderComponent],
  template: `
    <app-breadcrumb [segments]="breadcrumbSegments" />

    @if (company()) {
      <app-company-header [company]="company()!" />
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
  breadcrumbSegments: BreadcrumbSegment[] = [];
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
    private api: ApiService,
    private titleService: Title
  ) {}

  ngOnInit(): void {
    const params = this.route.snapshot.paramMap;
    this.cik = params.get('cik') ?? '';
    this.submissionId = parseInt(params.get('submissionId') ?? '0', 10);
    this.concept = params.get('concept') ?? '';
    this.breadcrumbSegments = [
      { label: 'Home', route: '/dashboard' },
      { label: this.cik, route: ['/company', this.cik] },
      { label: this.concept }
    ];

    const queryParams = this.route.snapshot.queryParamMap;
    const roleName = queryParams.get('roleName');
    if (roleName) {
      this.selectedRole = roleName;
    }

    this.api.getCompany(this.cik).subscribe({
      next: data => {
        this.company.set(data);
        const ticker = data.tickers.length > 0 ? data.tickers[0].ticker : ('CIK ' + data.cik);
        this.titleService.setTitle('Stocks - ' + ticker);
      }
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
