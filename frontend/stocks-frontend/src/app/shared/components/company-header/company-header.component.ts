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
  template: `
    <div class="company-header">
      <h2>{{ company().companyName ?? ('CIK ' + company().cik) }}{{ titleSuffix() }}</h2>
      <div class="company-subtitle">
        <span class="cik-label">CIK {{ company().cik }}</span>
        @if (company().latestPrice != null) {
          <span class="price-label">\${{ company().latestPrice!.toFixed(2) }}</span>
          @if (company().latestPriceDate) {
            <span class="price-date">as of {{ company().latestPriceDate }}</span>
          }
        }
      </div>
      @if (company().tickers.length > 0) {
        <div class="tickers">
          @for (t of company().tickers; track (t.ticker + t.exchange)) {
            <span class="badge">{{ t.ticker }}<span class="exchange">{{ t.exchange }}</span></span>
          }
        </div>
      }
      @if (links().length > 0) {
        <div class="company-links">
          @for (link of links(); track link.label) {
            @if (link.routerLink) {
              <a [routerLink]="link.routerLink" class="external-link">{{ link.label }}</a>
            } @else if (link.href) {
              <a class="external-link" [href]="link.href" target="_blank" rel="noopener">{{ link.label }}</a>
            }
          }
        </div>
      }
    </div>
  `,
  styles: [`
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
    .company-links {
      margin-top: 10px;
      display: flex;
      gap: 16px;
    }
    .external-link {
      color: #3b82f6;
      text-decoration: none;
      font-weight: 500;
      font-size: 14px;
    }
    .external-link:hover {
      text-decoration: underline;
    }
  `]
})
export class CompanyHeaderComponent {
  company = input.required<CompanyDetail>();
  titleSuffix = input<string>('');
  links = input<CompanyHeaderLink[]>([]);
}
