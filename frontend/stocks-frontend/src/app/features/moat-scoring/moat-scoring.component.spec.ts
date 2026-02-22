import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { MoatScoringComponent } from './moat-scoring.component';
import { MoatScoringResponse } from '../../core/services/api.service';

const mockActivatedRoute = {
  snapshot: {
    paramMap: {
      get: (key: string) => key === 'cik' ? '320193' : null
    }
  }
};

function makeMoatScoringResponse(): MoatScoringResponse {
  const scorecard = [];
  for (let i = 1; i <= 13; i++) {
    scorecard.push({
      checkNumber: i,
      name: `Check ${i}`,
      computedValue: i <= 10 ? 1.5 : null,
      threshold: i <= 4 ? '> 10%' : i <= 8 ? '> 1.0' : '< 1.5x',
      result: (i <= 9 ? 'pass' : i <= 11 ? 'fail' : 'na') as 'pass' | 'fail' | 'na'
    });
  }
  return {
    rawDataByYear: {
      '2023': { StockholdersEquity: 180000000000, Revenue: 380000000000 },
      '2024': { StockholdersEquity: 200000000000, Revenue: 400000000000 }
    },
    metrics: {
      averageGrossMargin: 45.2,
      averageOperatingMargin: 30.1,
      averageRoeCF: 15.5,
      averageRoeOE: 12.3,
      revenueCagr: 8.5,
      capexRatio: 22.0,
      interestCoverage: 29.5,
      debtToEquityRatio: 0.15,
      estimatedReturnOE: 4.6,
      currentDividendsPaid: 3000000000,
      marketCap: 2610000000000,
      pricePerShare: 174.0,
      positiveOeYears: 9,
      totalOeYears: 10,
      capitalReturnYears: 10,
      totalCapitalReturnYears: 10,
    },
    scorecard,
    trendData: [
      { year: 2023, grossMarginPct: 44.0, operatingMarginPct: 29.5, roeCfPct: 14.0, roeOePct: 11.0, revenue: 380000000000 },
      { year: 2024, grossMarginPct: 46.0, operatingMarginPct: 31.0, roeCfPct: 17.0, roeOePct: 13.0, revenue: 400000000000 },
    ],
    overallScore: 10,
    computableChecks: 13,
    yearsOfData: 10,
    pricePerShare: 174.0,
    priceDate: '2024-12-20',
    sharesOutstanding: 15000000000,
  };
}

describe('MoatScoringComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MoatScoringComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function flushRequests(scoringData?: MoatScoringResponse) {
    const companyReq = httpMock.expectOne('/api/companies/320193');
    companyReq.flush({
      companyId: 1,
      cik: 320193,
      dataSource: 'SEC',
      companyName: 'Apple Inc',
      latestPrice: 174.0,
      latestPriceDate: '2024-12-20',
      tickers: [{ ticker: 'AAPL', exchange: 'NASDAQ' }]
    });

    const scoringReq = httpMock.expectOne('/api/companies/320193/moat-scoring');
    scoringReq.flush(scoringData ?? makeMoatScoringResponse());

    const arRevenueReq = httpMock.expectOne('/api/companies/320193/ar-revenue');
    arRevenueReq.flush([]);
  }

  it('should create', () => {
    const fixture = TestBed.createComponent(MoatScoringComponent);
    fixture.detectChanges();
    flushRequests();
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should fetch moat scoring data on init', () => {
    const fixture = TestBed.createComponent(MoatScoringComponent);
    fixture.detectChanges();
    flushRequests();
    fixture.detectChanges();
    expect(fixture.componentInstance.scoring()).toBeTruthy();
    expect(fixture.componentInstance.scoring()!.overallScore).toBe(10);
  });

  it('should display score summary', () => {
    const fixture = TestBed.createComponent(MoatScoringComponent);
    fixture.detectChanges();
    flushRequests();
    fixture.detectChanges();

    const scoreValue = fixture.nativeElement.querySelector('.score-value');
    expect(scoreValue.textContent).toContain('10');
    const scoreTotal = fixture.nativeElement.querySelector('.score-total');
    expect(scoreTotal.textContent).toContain('13');
  });

  it('should display scorecard table with 13 rows', () => {
    const fixture = TestBed.createComponent(MoatScoringComponent);
    fixture.detectChanges();
    flushRequests();
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.scorecard-table tbody tr');
    expect(rows.length).toBe(13);
  });

  it('should show pass/fail/na indicators', () => {
    const fixture = TestBed.createComponent(MoatScoringComponent);
    fixture.detectChanges();
    flushRequests();
    fixture.detectChanges();

    const passIndicators = fixture.nativeElement.querySelectorAll('.indicator.pass');
    const failIndicators = fixture.nativeElement.querySelectorAll('.indicator.fail');
    const naIndicators = fixture.nativeElement.querySelectorAll('.indicator.na');
    expect(passIndicators.length).toBe(9);
    expect(failIndicators.length).toBe(2);
    expect(naIndicators.length).toBe(2);
  });

  it('should handle error state', () => {
    const fixture = TestBed.createComponent(MoatScoringComponent);
    fixture.detectChanges();

    const companyReq = httpMock.expectOne('/api/companies/320193');
    companyReq.flush({ companyId: 1, cik: 320193, dataSource: 'SEC', companyName: 'Apple', latestPrice: null, latestPriceDate: null, tickers: [] });

    const scoringReq = httpMock.expectOne('/api/companies/320193/moat-scoring');
    scoringReq.error(new ProgressEvent('error'));

    const arRevenueReq = httpMock.expectOne('/api/companies/320193/ar-revenue');
    arRevenueReq.flush([]);

    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('.error');
    expect(error?.textContent).toContain('Failed to load Buffett scoring data');
  });

  describe('scoreBadge computed', () => {
    it('should return score-green for score >= 10', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests();
      fixture.detectChanges();

      expect(fixture.componentInstance.scoreBadge()).toBe('score-green');
    });

    it('should return score-yellow for score 7-9', () => {
      const data = makeMoatScoringResponse();
      data.overallScore = 8;
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests(data);
      fixture.detectChanges();

      expect(fixture.componentInstance.scoreBadge()).toBe('score-yellow');
    });

    it('should return score-red for score < 7', () => {
      const data = makeMoatScoringResponse();
      data.overallScore = 4;
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests(data);
      fixture.detectChanges();

      expect(fixture.componentInstance.scoreBadge()).toBe('score-red');
    });

    it('should return empty string when no scoring data', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      expect(fixture.componentInstance.scoreBadge()).toBe('');
    });
  });

  describe('checkTooltips computed', () => {
    it('should return empty record when no scoring data', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      expect(fixture.componentInstance.checkTooltips()).toEqual({});
    });

    it('should return tooltips for all 13 checks', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests();
      fixture.detectChanges();

      const tooltips = fixture.componentInstance.checkTooltips();
      for (let i = 1; i <= 13; i++) {
        expect(tooltips[i]).toBeTruthy();
        expect(tooltips[i].length).toBeGreaterThan(0);
      }
    });

    it('should include years of data in tooltip text', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests();
      fixture.detectChanges();

      const tooltips = fixture.componentInstance.checkTooltips();
      expect(tooltips[1]).toContain('10 yrs');
    });
  });

  describe('metricRows computed', () => {
    it('should return empty array when no scoring data', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      expect(fixture.componentInstance.metricRows()).toEqual([]);
    });

    it('should return 13 metric rows', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests();
      fixture.detectChanges();

      const rows = fixture.componentInstance.metricRows();
      expect(rows.length).toBe(13);
    });

    it('should have label, display, and tooltip on each row', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests();
      fixture.detectChanges();

      for (const row of fixture.componentInstance.metricRows()) {
        expect(row.label).toBeTruthy();
        expect(row.display).toBeDefined();
        expect(row.tooltip).toBeTruthy();
      }
    });

    it('should render metrics table in DOM', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests();
      fixture.detectChanges();

      const tableRows = fixture.nativeElement.querySelectorAll('.metrics-table tbody tr');
      expect(tableRows.length).toBe(13);
    });
  });

  describe('yearKeys computed', () => {
    it('should return empty array when no scoring data', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      expect(fixture.componentInstance.yearKeys()).toEqual([]);
    });

    it('should return years in reverse order', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests();
      fixture.detectChanges();

      const keys = fixture.componentInstance.yearKeys();
      expect(keys).toEqual(['2024', '2023']);
    });
  });

  describe('rawRows computed', () => {
    it('should return empty array when no scoring data', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      expect(fixture.componentInstance.rawRows()).toEqual([]);
    });

    it('should return one row per concept', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests();
      fixture.detectChanges();

      const rows = fixture.componentInstance.rawRows();
      expect(rows.length).toBe(2);
      expect(rows[0].concept).toBe('Revenue');
      expect(rows[1].concept).toBe('StockholdersEquity');
    });

    it('should map values by year', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests();
      fixture.detectChanges();

      const rows = fixture.componentInstance.rawRows();
      const revenueRow = rows.find(r => r.concept === 'Revenue')!;
      expect(revenueRow.values['2024']).toBe(400000000000);
      expect(revenueRow.values['2023']).toBe(380000000000);
    });

    it('should render raw data table in DOM', () => {
      const fixture = TestBed.createComponent(MoatScoringComponent);
      fixture.detectChanges();
      flushRequests();
      fixture.detectChanges();

      const tableRows = fixture.nativeElement.querySelectorAll('.raw-table tbody tr');
      expect(tableRows.length).toBe(2);
    });
  });
});
