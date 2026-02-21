import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { ScoringComponent } from './scoring.component';
import { ScoringResponse } from '../../core/services/api.service';

const mockActivatedRoute = {
  snapshot: {
    paramMap: {
      get: (key: string) => key === 'cik' ? '320193' : null
    }
  }
};

function makeScoringResponse(): ScoringResponse {
  const scorecard = [];
  for (let i = 1; i <= 13; i++) {
    scorecard.push({
      checkNumber: i,
      name: `Check ${i}`,
      computedValue: i <= 10 ? 1.5 : null,
      threshold: '> 1.0',
      result: (i <= 8 ? 'pass' : i <= 10 ? 'fail' : 'na') as 'pass' | 'fail' | 'na'
    });
  }
  return {
    rawDataByYear: { '2024': { StockholdersEquity: 200000000000 } },
    metrics: {
      bookValue: 193000000000,
      marketCap: 2610000000000,
      debtToEquityRatio: 0.15,
      priceToBookRatio: 13.52,
      debtToBookRatio: 0.16,
      adjustedRetainedEarnings: 50000000000,
      oldestRetainedEarnings: 50000000000,
      averageNetCashFlow: 10000000000,
      averageOwnerEarnings: 12000000000,
      estimatedReturnCF: 0.0038,
      estimatedReturnOE: 0.0046,
      currentDividendsPaid: 3000000000,
    },
    scorecard,
    overallScore: 8,
    computableChecks: 10,
    yearsOfData: 1,
    pricePerShare: 174.0,
    priceDate: '2024-12-20',
    sharesOutstanding: 15000000000,
    maxBuyPrice: 12.50,
    percentageUpside: -92.82,
  };
}

describe('ScoringComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScoringComponent],
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

  function flushRequests(scoringData?: ScoringResponse) {
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

    const scoringReq = httpMock.expectOne('/api/companies/320193/scoring');
    scoringReq.flush(scoringData ?? makeScoringResponse());
  }

  it('should create', () => {
    const fixture = TestBed.createComponent(ScoringComponent);
    fixture.detectChanges();
    flushRequests();
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should fetch scoring data on init', () => {
    const fixture = TestBed.createComponent(ScoringComponent);
    fixture.detectChanges();
    flushRequests();
    fixture.detectChanges();
    expect(fixture.componentInstance.scoring()).toBeTruthy();
    expect(fixture.componentInstance.scoring()!.overallScore).toBe(8);
  });

  it('should display score summary', () => {
    const fixture = TestBed.createComponent(ScoringComponent);
    fixture.detectChanges();
    flushRequests();
    fixture.detectChanges();

    const scoreValue = fixture.nativeElement.querySelector('.score-value');
    expect(scoreValue.textContent).toContain('8');
    const scoreTotal = fixture.nativeElement.querySelector('.score-total');
    expect(scoreTotal.textContent).toContain('10');
  });

  it('should display scorecard table with 13 rows', () => {
    const fixture = TestBed.createComponent(ScoringComponent);
    fixture.detectChanges();
    flushRequests();
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.scorecard-table tbody tr');
    expect(rows.length).toBe(13);
  });

  it('should show pass/fail/na indicators', () => {
    const fixture = TestBed.createComponent(ScoringComponent);
    fixture.detectChanges();
    flushRequests();
    fixture.detectChanges();

    const passIndicators = fixture.nativeElement.querySelectorAll('.indicator.pass');
    const failIndicators = fixture.nativeElement.querySelectorAll('.indicator.fail');
    const naIndicators = fixture.nativeElement.querySelectorAll('.indicator.na');
    expect(passIndicators.length).toBe(8);
    expect(failIndicators.length).toBe(2);
    expect(naIndicators.length).toBe(3);
  });

  it('should handle error state', () => {
    const fixture = TestBed.createComponent(ScoringComponent);
    fixture.detectChanges();

    const companyReq = httpMock.expectOne('/api/companies/320193');
    companyReq.flush({ companyId: 1, cik: 320193, dataSource: 'SEC', companyName: 'Apple', latestPrice: null, latestPriceDate: null, tickers: [] });

    const scoringReq = httpMock.expectOne('/api/companies/320193/scoring');
    scoringReq.error(new ProgressEvent('error'));

    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('.error');
    expect(error?.textContent).toContain('Failed to load scoring data');
  });
});
