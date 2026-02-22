import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

export interface BreadcrumbSegment {
  label: string;
  route?: string | string[];
}

@Component({
  selector: 'app-breadcrumb',
  standalone: true,
  imports: [RouterLink],
  template: `
    <nav class="breadcrumb">
      @for (segment of segments(); track $index; let last = $last) {
        @if (!last && segment.route) {
          <a [routerLink]="segment.route">{{ segment.label }}</a>
        } @else {
          <span>{{ segment.label }}</span>
        }
        @if (!last) {
          <span class="sep">/</span>
        }
      }
    </nav>
  `,
  styles: [`
    .breadcrumb {
      font-size: 13px;
      margin-bottom: 12px;
      color: #64748b;
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
    }
  `]
})
export class BreadcrumbComponent {
  segments = input.required<BreadcrumbSegment[]>();
}
