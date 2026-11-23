import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ScoresReportComponent } from './scores-report.component';
import { CompanyScoreSummary, PaginatedResponse } from '../../core/services/api.service';

function makeRow(id: number, ticker: string, sicCode: number | null, sicDescription: string | null,
                 isFinancial: boolean): CompanyScoreSummary {
  return {
    companyId: id, cik: String(id), companyName: `Company ${ticker}`, ticker, exchange: 'NYSE',
    overallScore: 13, computableChecks: 15, yearsOfData: 5,
    bookValue: null, marketCap: null, debtToEquityRatio: null, priceToBookRatio: null,
    debtToBookRatio: null, adjustedRetainedEarnings: null, averageNetCashFlow: null,
    averageOwnerEarnings: null, averageRoeCF: null, averageRoeOE: null,
    estimatedReturnCF: null, estimatedReturnOE: null, pricePerShare: null, priceDate: null,
    sharesOutstanding: null, currentDividendsPaid: null, maxBuyPrice: null, percentageUpside: null,
    computedAt: '2026-11-22T00:00:00Z', sicCode, sicDescription, isFinancial
  };
}

function makeResponse(): PaginatedResponse<CompanyScoreSummary> {
  return {
    items: [
      makeRow(1, 'BAC', 6021, 'National Commercial Banks', true),
      makeRow(2, 'AAPL', 3571, 'Electronic Computers', false),
    ],
    pagination: { pageNumber: 1, totalItems: 2, totalPages: 1 }
  };
}

describe('ScoresReportComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScoresReportComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should show the financial badge only on financial rows, with the SIC as tooltip', () => {
    const fixture = TestBed.createComponent(ScoresReportComponent);
    fixture.detectChanges();
    httpMock.expectOne(r => r.url.startsWith('/api/reports/scores?')).flush(makeResponse());
    fixture.detectChanges();

    const badges: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('.badge-financial'));
    expect(badges.length).toBe(1);
    expect(badges[0].textContent?.trim()).toBe('Financial');
    expect(badges[0].getAttribute('title')).toBe('SIC 6021 National Commercial Banks');
    expect(badges[0].closest('tr')?.textContent).toContain('BAC');
  });

  it('should not send the financials param by default', () => {
    const fixture = TestBed.createComponent(ScoresReportComponent);
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url.startsWith('/api/reports/scores?'));
    expect(req.request.url).not.toContain('financials=');
    req.flush(makeResponse());
  });

  it('should send the financials filter and go back to page 1', () => {
    const fixture = TestBed.createComponent(ScoresReportComponent);
    fixture.detectChanges();
    httpMock.expectOne(r => r.url.startsWith('/api/reports/scores?')).flush(makeResponse());

    const component = fixture.componentInstance;
    component.page.set(3);
    component.financials = 'exclude';
    component.onFilterChange();

    const req = httpMock.expectOne(r => r.url.startsWith('/api/reports/scores?'));
    expect(req.request.url).toContain('financials=exclude');
    expect(req.request.url).toContain('page=1');
    req.flush(makeResponse());
  });

  it('should offer the financials filter select', () => {
    const fixture = TestBed.createComponent(ScoresReportComponent);
    fixture.detectChanges();
    httpMock.expectOne(r => r.url.startsWith('/api/reports/scores?')).flush(makeResponse());
    fixture.detectChanges();

    const labels: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('.filters label'));
    const label = labels.find(l => l.textContent?.includes('Financials'));
    expect(label).toBeTruthy();
    const options = Array.from(label!.querySelectorAll('option')).map(o => o.textContent?.trim());
    expect(options).toEqual(['All', 'Only financials', 'Exclude financials']);
  });
});
