import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PortfolioAdvisorComponent, parseTickers } from './portfolio-advisor.component';
import { PortfolioAdvisorReport } from '../../core/services/api.service';

function makeReport(): PortfolioAdvisorReport {
  return {
    scoresComputedAt: '2026-07-03T12:00:00Z',
    baselineSnapshotDate: '2026-06-26',
    sells: [{
      ticker: 'PIPR', companyId: 2, cik: '1230245', companyName: 'Piper Jaffray Companies',
      action: 'sell', overallScore: 14, computableChecks: 15, pricePerShare: 26.76, priceDate: '2026-07-02',
      trigger: 'price',
      reasons: ['Dropped from 15/15 (as of 2026-06-26) to 14/15.', 'Est. Return (CF) Not Too Big (< 40%) now FAILS: 36.8 → 40.1.']
    }],
    buys: [{
      ticker: 'TOL', companyId: 3, cik: '794170', companyName: 'Toll Brothers Inc',
      action: 'buy', overallScore: 15, computableChecks: 15, pricePerShare: 135.22, priceDate: '2026-07-02',
      trigger: 'filing',
      reasons: ['Newly qualified: was 14/15 as of 2026-06-26.']
    }],
    holds: [{
      ticker: 'LEN', companyId: 4, cik: '920760', companyName: 'Lennar Corp',
      action: 'hold', overallScore: 15, computableChecks: 15, pricePerShare: 102.8, priceDate: '2026-07-02',
      trigger: null,
      reasons: ['Still 15/15 (unchanged since 2026-06-26).']
    }],
    unknowns: [{
      ticker: 'XYZ', companyId: 0, cik: '0', companyName: null,
      action: 'unknown', overallScore: null, computableChecks: null, pricePerShare: null, priceDate: null,
      trigger: null,
      reasons: ['No Graham score found for this ticker.']
    }]
  };
}

describe('parseTickers', () => {
  it('should parse one ticker per line, ignoring extra tokens, comments, and duplicates', () => {
    const input = 'aapl\nPIPR 100 shares\n# a comment\n\nTOL, 50\npipr\n';
    expect(parseTickers(input)).toEqual(['AAPL', 'PIPR', 'TOL']);
  });

  it('should return empty for blank input', () => {
    expect(parseTickers('   \n \n')).toEqual([]);
  });
});

describe('PortfolioAdvisorComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [PortfolioAdvisorComponent],
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

  it('should create without firing a request', () => {
    const fixture = TestBed.createComponent(PortfolioAdvisorComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should post parsed tickers and render grouped results', () => {
    const fixture = TestBed.createComponent(PortfolioAdvisorComponent);
    fixture.detectChanges();

    fixture.componentInstance.tickersInput = 'PIPR\nLEN\nXYZ';
    fixture.componentInstance.analyze();

    const req = httpMock.expectOne('/api/reports/portfolio-advisor');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.tickers).toEqual(['PIPR', 'LEN', 'XYZ']);
    req.flush(makeReport());
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Sell (1)');
    expect(text).toContain('Buy (1)');
    expect(text).toContain('Hold (1)');
    expect(text).toContain('Unrecognized (1)');
    expect(text).toContain('Est. Return (CF) Not Too Big');
  });

  it('should persist the input in localStorage', () => {
    const fixture = TestBed.createComponent(PortfolioAdvisorComponent);
    fixture.detectChanges();

    fixture.componentInstance.tickersInput = 'AAPL';
    fixture.componentInstance.analyze();
    httpMock.expectOne('/api/reports/portfolio-advisor').flush(makeReport());

    expect(localStorage.getItem('portfolio-advisor.tickers')).toBe('AAPL');
  });

  it('should handle error state', () => {
    const fixture = TestBed.createComponent(PortfolioAdvisorComponent);
    fixture.detectChanges();

    fixture.componentInstance.analyze();
    httpMock.expectOne('/api/reports/portfolio-advisor').error(new ProgressEvent('error'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.error')?.textContent).toContain('Failed to load recommendations');
  });
});
