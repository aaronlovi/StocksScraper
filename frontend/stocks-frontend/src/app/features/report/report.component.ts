import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService, StatementTreeNode } from '../../core/services/api.service';
import { TreeTableComponent } from '../../shared/components/tree-table/tree-table.component';

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

    @if (treeData()) {
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
      </div>

      <app-tree-table [nodes]="treeNodes()" />
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
    .controls {
      margin-bottom: 16px;
    }
    .controls select {
      padding: 4px 8px;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      margin-left: 8px;
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
  yearOptions = Array.from({ length: 15 }, (_, i) => 2025 - i);

  treeData = signal<StatementTreeNode | null>(null);
  treeNodes = signal<StatementTreeNode[]>([]);
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

    this.loadStatement();
  }

  onYearChange(): void {
    this.treeData.set(null);
    this.treeNodes.set([]);
    this.error.set(null);
    this.loadStatement();
  }

  private loadStatement(): void {
    this.api.getStatement(this.cik, this.submissionId, this.concept, this.selectedYear ?? undefined).subscribe({
      next: data => {
        this.treeData.set(data);
        this.treeNodes.set(data.children ?? [data]);
      },
      error: () => this.error.set('Failed to load statement.')
    });
  }
}
