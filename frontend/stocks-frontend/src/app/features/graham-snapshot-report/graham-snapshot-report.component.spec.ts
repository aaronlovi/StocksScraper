import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { GrahamSnapshotReportComponent, pickDefaultSnapshotDate } from './graham-snapshot-report.component';
import { CompanyScoreReturnSummary, PaginatedResponse } from '../../core/services/api.service';

function makeReturnItems(): CompanyScoreReturnSummary[] {
  return [
    {
      companyId: 1, cik: '320193', companyName: 'Apple Inc', ticker: 'AAPL', exchange: 'NASDAQ',
      overallScore: 15, computableChecks: 15, pricePerShare: 138.0,
      totalReturnPct: 25.5, annualizedReturnPct: 48.3, currentValueOf1000: 1255,
      startDate: '2025-12-31', endDate: '2026-06-25', startPrice: 138.0, endPrice: 174.0,
      computedAt: '2026-06-30'
    },
    {
      companyId: 2, cik: '789019', companyName: 'Microsoft Corp', ticker: 'MSFT', exchange: 'NASDAQ',
      overallScore: 15, computableChecks: 15, pricePerShare: 442.0,
      totalReturnPct: -5.2, annualizedReturnPct: -10.1, currentValueOf1000: 948,
      startDate: '2025-12-31', endDate: '2026-06-25', startPrice: 442.0, endPrice: 420.0,
      computedAt: '2026-06-30'
    },
  ];
}

function makePaginatedResponse(): PaginatedResponse<CompanyScoreReturnSummary> {
  return {
    items: makeReturnItems(),
    pagination: { pageNumber: 1, totalItems: 2, totalPages: 1 }
  };
}

describe('GrahamSnapshotReportComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GrahamSnapshotReportComponent],
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

  function flushDates(dates: string[] = ['2025-06-30', '2025-12-31']) {
    const req = httpMock.expectOne('/api/reports/graham-snapshot-dates');
    req.flush(dates);
  }

  function flushReturns(data?: PaginatedResponse<CompanyScoreReturnSummary>) {
    const req = httpMock.expectOne(r => r.url.startsWith('/api/reports/graham-snapshot?'));
    req.flush(data ?? makePaginatedResponse());
  }

  it('should create and fetch dates then returns', () => {
    const fixture = TestBed.createComponent(GrahamSnapshotReportComponent);
    fixture.detectChanges();
    flushDates();
    flushReturns();
    fixture.detectChanges();

    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.componentInstance.snapshotDates().length).toBe(2);
    expect(fixture.componentInstance.items().length).toBe(2);
    expect(fixture.componentInstance.loading()).toBe(false);
  });

  it('should show a message when no snapshots exist', () => {
    const fixture = TestBed.createComponent(GrahamSnapshotReportComponent);
    fixture.detectChanges();
    flushDates([]);
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toContain('No score snapshots');
  });

  it('should display table rows for each item', () => {
    const fixture = TestBed.createComponent(GrahamSnapshotReportComponent);
    fixture.detectChanges();
    flushDates();
    flushReturns();
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
  });

  it('should handle returns error state', () => {
    const fixture = TestBed.createComponent(GrahamSnapshotReportComponent);
    fixture.detectChanges();
    flushDates();

    const req = httpMock.expectOne(r => r.url.startsWith('/api/reports/graham-snapshot?'));
    req.error(new ProgressEvent('error'));
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('.error');
    expect(error?.textContent).toContain('Failed to load snapshot report');
  });

  it('should refetch when the snapshot date changes', () => {
    const fixture = TestBed.createComponent(GrahamSnapshotReportComponent);
    fixture.detectChanges();
    flushDates();
    flushReturns();
    fixture.detectChanges();

    fixture.componentInstance.onDateChange('2025-06-30');
    const req = httpMock.expectOne(r => r.url.startsWith('/api/reports/graham-snapshot?') && r.url.includes('asOfDate=2025-06-30'));
    req.flush(makePaginatedResponse());
    fixture.detectChanges();

    expect(fixture.componentInstance.asOfDate).toBe('2025-06-30');
  });
});

describe('pickDefaultSnapshotDate', () => {
  it('should pick the date closest to six months ago', () => {
    const now = new Date();
    const sixMonthsAgo = new Date(now);
    sixMonthsAgo.setMonth(sixMonthsAgo.getMonth() - 6);
    const closeIso = sixMonthsAgo.toISOString().slice(0, 10);

    const dates = ['2021-07-31', closeIso, now.toISOString().slice(0, 10)];
    expect(pickDefaultSnapshotDate(dates)).toBe(closeIso);
  });

  it('should fall back to the only date available', () => {
    expect(pickDefaultSnapshotDate(['2024-01-31'])).toBe('2024-01-31');
  });
});
