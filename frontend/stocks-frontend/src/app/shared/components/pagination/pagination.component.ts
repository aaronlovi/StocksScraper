import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  standalone: true,
  template: `
    <div class="pagination">
      <button [disabled]="page() <= 1" (click)="pageChange.emit(page() - 1)">Previous</button>
      <span>
        Page {{ page() }} of {{ totalPages() }}
        @if (totalItems() != null) {
          ({{ totalItems() }} companies)
        }
      </span>
      <button [disabled]="page() >= totalPages()" (click)="pageChange.emit(page() + 1)">Next</button>
    </div>
  `,
  styles: [`
    .pagination {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-top: 16px;
    }
    .pagination button {
      padding: 6px 12px;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      background: #fff;
      cursor: pointer;
    }
    .pagination button:disabled {
      opacity: 0.5;
      cursor: default;
    }
  `]
})
export class PaginationComponent {
  page = input.required<number>();
  totalPages = input.required<number>();
  totalItems = input<number | null>(null);
  pageChange = output<number>();
}
