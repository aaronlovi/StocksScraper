import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  standalone: true,
  templateUrl: './pagination.component.html',
  styleUrls: ['./pagination.component.css']
})
export class PaginationComponent {
  page = input.required<number>();
  totalPages = input.required<number>();
  totalItems = input<number | null>(null);
  pageChange = output<number>();
}
