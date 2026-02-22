import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CompanyDetail } from '../../../core/services/api.service';

export interface CompanyHeaderLink {
  label: string;
  routerLink?: string;
  href?: string;
}

@Component({
  selector: 'app-company-header',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './company-header.component.html',
  styleUrls: ['./company-header.component.css']
})
export class CompanyHeaderComponent {
  company = input.required<CompanyDetail>();
  titleSuffix = input<string>('');
  links = input<CompanyHeaderLink[]>([]);
}
