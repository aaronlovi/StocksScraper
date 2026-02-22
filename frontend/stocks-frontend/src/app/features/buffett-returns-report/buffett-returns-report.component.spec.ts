import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BuffettReturnsReportComponent } from './buffett-returns-report.component';
import { CompanyScoreReturnSummary, PaginatedResponse } from '../../core/services/api.service';

function makeReturnItems(): CompanyScoreReturnSummary[] {
  return [
    {
      companyId: 1, cik: '320193', companyName: 'Apple Inc', ticker: 'AAPL', exchange: 'NASDAQ',
      overallScore: 12, computableChecks: 13, pricePerShare: 174.0,
      totalReturnPct: 25.5, annualizedReturnPct: 48.3, currentValueOf1000: 1255,
      startDate: '2024-06-20', endDate: '2024-12-20', startPrice: 138.0, endPrice: 174.0,
      computedAt: '2024-12-20'
    },
    {
      companyId: 2, cik: '789019', companyName: 'Microsoft Corp', ticker: 'MSFT', exchange: 'NASDAQ',
      overallScore: 13, computableChecks: 13, pricePerShare: 420.0,
      totalReturnPct: -5.2, annualizedReturnPct: -10.1, currentValueOf1000: 948,
      startDate: '2024-06-20', endDate: '2024-12-20', startPrice: 442.0, endPrice: 420.0,
      computedAt: '2024-12-20'
    },
  ];
}

function makePaginatedResponse(): PaginatedResponse<CompanyScoreReturnSummary> {
  return {
    items: makeReturnItems(),
    pagination: { pageNumber: 1, totalItems: 2, totalPages: 1 }
  };
}

describe('BuffettReturnsReportComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BuffettReturnsReportComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ]
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function flushRequest(data?: PaginatedResponse<CompanyScoreReturnSummary>) {
    const req = httpMock.expectOne(r => r.url.startsWith('/api/reports/buffett-returns'));
    req.flush(data ?? makePaginatedResponse());
  }

  it('should create', () => {
    const fixture = TestBed.createComponent(BuffettReturnsReportComponent);
    fixture.detectChanges();
    flushRequest();
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should fetch returns on init', () => {
    const fixture = TestBed.createComponent(BuffettReturnsReportComponent);
    fixture.detectChanges();
    flushRequest();
    fixture.detectChanges();

    expect(fixture.componentInstance.items().length).toBe(2);
    expect(fixture.componentInstance.loading()).toBe(false);
  });

  it('should display table rows for each item', () => {
    const fixture = TestBed.createComponent(BuffettReturnsReportComponent);
    fixture.detectChanges();
    flushRequest();
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
  });

  it('should handle error state', () => {
    const fixture = TestBed.createComponent(BuffettReturnsReportComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(r => r.url.startsWith('/api/reports/buffett-returns'));
    req.error(new ProgressEvent('error'));
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('.error');
    expect(error?.textContent).toContain('Failed to load returns report');
  });

  describe('summary computed', () => {
    it('should return null when items are empty', () => {
      const fixture = TestBed.createComponent(BuffettReturnsReportComponent);
      expect(fixture.componentInstance.summary()).toBeNull();
    });

    it('should compute summary from items', () => {
      const fixture = TestBed.createComponent(BuffettReturnsReportComponent);
      fixture.detectChanges();
      flushRequest();
      fixture.detectChanges();

      const sum = fixture.componentInstance.summary()!;
      expect(sum).toBeTruthy();
      expect(sum.count).toBe(2);
      expect(sum.bestTicker).toBe('AAPL');
      expect(sum.worstTicker).toBe('MSFT');
    });

    it('should render summary section in DOM', () => {
      const fixture = TestBed.createComponent(BuffettReturnsReportComponent);
      fixture.detectChanges();
      flushRequest();
      fixture.detectChanges();

      const summaryItems = fixture.nativeElement.querySelectorAll('.summary-item');
      expect(summaryItems.length).toBe(8);
    });
  });

  describe('pagination', () => {
    it('should set pagination signal from response', () => {
      const fixture = TestBed.createComponent(BuffettReturnsReportComponent);
      fixture.detectChanges();
      flushRequest();
      fixture.detectChanges();

      const pg = fixture.componentInstance.pagination()!;
      expect(pg.totalItems).toBe(2);
      expect(pg.totalPages).toBe(1);
    });

    it('should render pagination component', () => {
      const fixture = TestBed.createComponent(BuffettReturnsReportComponent);
      fixture.detectChanges();
      flushRequest();
      fixture.detectChanges();

      const pagEl = fixture.nativeElement.querySelector('app-pagination');
      expect(pagEl).toBeTruthy();
    });
  });
});
