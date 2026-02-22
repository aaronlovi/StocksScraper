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
  templateUrl: './breadcrumb.component.html',
  styleUrls: ['./breadcrumb.component.css']
})
export class BreadcrumbComponent {
  segments = input.required<BreadcrumbSegment[]>();
}
