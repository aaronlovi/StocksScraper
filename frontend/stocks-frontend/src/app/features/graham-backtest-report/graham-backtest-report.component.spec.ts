import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { GrahamBacktestReportComponent, buildBacktestChart } from './graham-backtest-report.component';
import { GrahamBacktestReport } from '../../core/services/api.service';

function makeReport(): GrahamBacktestReport {
  return {
    summary: {
      firstDate: '2025-10-31', lastDate: '2026-06-25', periodCount: 2, averageConstituents: 2,
      totalReturnPct: 10.25, annualizedReturnPct: 16.4, finalValue: 1102.5,
      benchmarkTotalReturnPct: 4.0, benchmarkAnnualizedReturnPct: 6.2, benchmarkFinalValue: 1040,
      benchmarkTicker: 'SPY', minScore: 15
    },
    periods: [
      {
        startDate: '2025-10-31', endDate: '2025-11-30', constituentCount: 2,
        portfolioReturnPct: 5.0, cumulativeValue: 1050,
        benchmarkReturnPct: 2.0, benchmarkCumulativeValue: 1020,
        constituents: [
          {
            companyId: 1, cik: '320193', companyName: 'Apple Inc', ticker: 'AAPL', exchange: 'NASDAQ',
            startPrice: 100, startPriceDate: '2025-10-31', endPrice: 110, endPriceDate: '2025-11-28',
            periodReturnPct: 10, entered: false, left: false, enteredTrigger: null, leftTrigger: null
          },
          {
            companyId: 2, cik: '789019', companyName: 'Microsoft Corp', ticker: 'MSFT', exchange: 'NASDAQ',
            startPrice: 400, startPriceDate: '2025-10-31', endPrice: 400, endPriceDate: '2025-11-28',
            periodReturnPct: 0, entered: false, left: true, enteredTrigger: null, leftTrigger: 'price'
          }
        ]
      },
      {
        startDate: '2025-11-30', endDate: '2026-06-25', constituentCount: 1,
        portfolioReturnPct: 5.0, cumulativeValue: 1102.5,
        benchmarkReturnPct: 1.96, benchmarkCumulativeValue: 1040,
        constituents: [
          {
            companyId: 1, cik: '320193', companyName: 'Apple Inc', ticker: 'AAPL', exchange: 'NASDAQ',
            startPrice: 110, startPriceDate: '2025-11-28', endPrice: 115.5, endPriceDate: '2026-06-25',
            periodReturnPct: 5, entered: false, left: false, enteredTrigger: null, leftTrigger: null
          }
        ]
      }
    ]
  };
}

describe('GrahamBacktestReportComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GrahamBacktestReportComponent],
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

  function flushRequest(data?: GrahamBacktestReport) {
    const req = httpMock.expectOne(r => r.url.startsWith('/api/reports/graham-backtest'));
    req.flush(data ?? makeReport());
  }

  it('should create and fetch the backtest', () => {
    const fixture = TestBed.createComponent(GrahamBacktestReportComponent);
    fixture.detectChanges();
    flushRequest();
    fixture.detectChanges();

    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.componentInstance.report()?.periods.length).toBe(2);
    expect(fixture.componentInstance.loading()).toBe(false);
  });

  it('should render one row per period', () => {
    const fixture = TestBed.createComponent(GrahamBacktestReportComponent);
    fixture.detectChanges();
    flushRequest();
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr.period-row');
    expect(rows.length).toBe(2);
  });

  it('should expand a period to show constituents', () => {
    const fixture = TestBed.createComponent(GrahamBacktestReportComponent);
    fixture.detectChanges();
    flushRequest();
    fixture.detectChanges();

    fixture.componentInstance.toggleExpand(0);
    fixture.detectChanges();

    const constituentRows = fixture.nativeElement.querySelectorAll('.constituents-table tbody tr');
    expect(constituentRows.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Apple Inc');
  });

  it('should show the trigger on flow badges', () => {
    const fixture = TestBed.createComponent(GrahamBacktestReportComponent);
    fixture.detectChanges();
    flushRequest();
    fixture.detectChanges();

    fixture.componentInstance.toggleExpand(0);
    fixture.detectChanges();

    const leftBadge = fixture.nativeElement.querySelector('.flow-badge.left') as HTMLElement;
    expect(leftBadge.textContent).toContain('out next · price');
    expect(leftBadge.classList.contains('trigger-price')).toBe(true);
  });

  it('should render the chart with strategy and benchmark lines', () => {
    const fixture = TestBed.createComponent(GrahamBacktestReportComponent);
    fixture.detectChanges();
    flushRequest();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('polyline.strategy-line')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('polyline.benchmark-line')).toBeTruthy();
  });

  it('should handle error state', () => {
    const fixture = TestBed.createComponent(GrahamBacktestReportComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(r => r.url.startsWith('/api/reports/graham-backtest'));
    req.error(new ProgressEvent('error'), { status: 500 });
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('.error');
    expect(error?.textContent).toContain('Failed to load backtest report');
  });

  it('should refetch when the refresh button is clicked', () => {
    const fixture = TestBed.createComponent(GrahamBacktestReportComponent);
    fixture.detectChanges();
    flushRequest();
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.refresh-btn') as HTMLButtonElement;
    button.click();
    fixture.detectChanges();

    flushRequest();
    fixture.detectChanges();
    expect(fixture.componentInstance.loading()).toBe(false);
  });

  it('should restore the interval from query params', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [GrahamBacktestReportComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap({ interval: 'weekly', minScore: '14' }) }
          }
        }
      ]
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(GrahamBacktestReportComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(r =>
      r.url.startsWith('/api/reports/graham-backtest') && r.url.includes('interval=weekly') && r.url.includes('minScore=14'));
    req.flush(makeReport());
    fixture.detectChanges();

    expect(fixture.componentInstance.interval).toBe('weekly');
    expect(fixture.componentInstance.minScore).toBe(14);
  });

  it('should show the backfill hint on 404', () => {
    const fixture = TestBed.createComponent(GrahamBacktestReportComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(r => r.url.startsWith('/api/reports/graham-backtest'));
    req.flush({ error: 'no snapshots' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toContain('No score snapshots');
  });
});

describe('buildBacktestChart', () => {
  it('should return null for an empty report', () => {
    expect(buildBacktestChart(null)).toBeNull();
  });

  it('should include the starting point plus one point per period', () => {
    const chart = buildBacktestChart(makeReport())!;
    expect(chart.strategyPoints.split(' ').length).toBe(3);
    expect(chart.benchmarkPoints!.split(' ').length).toBe(3);
  });

  it('should omit the benchmark line when benchmark data is missing', () => {
    const report = makeReport();
    report.summary.benchmarkFinalValue = null;
    const chart = buildBacktestChart(report)!;
    expect(chart.benchmarkPoints).toBeNull();
  });
});
