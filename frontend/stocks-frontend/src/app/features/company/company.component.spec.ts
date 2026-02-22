import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { CompanyComponent } from './company.component';

const mockActivatedRoute = {
  snapshot: {
    paramMap: {
      get: (key: string) => key === 'cik' ? '320193' : null
    }
  }
};

describe('CompanyComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompanyComponent],
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

  function flushInitialRequests() {
    const companyReq = httpMock.expectOne('/api/companies/320193');
    companyReq.flush({
      companyId: 1,
      cik: 320193,
      dataSource: 'SEC',
      companyName: 'Apple Inc',
      latestPrice: 191.50,
      latestPriceDate: '2025-06-13',
      tickers: [
        { ticker: 'AAPL', exchange: 'NASDAQ' },
        { ticker: 'AAPL', exchange: 'NYSE' }
      ]
    });

    const subsReq = httpMock.expectOne('/api/companies/320193/submissions');
    subsReq.flush({
      items: [
        { submissionId: 100, filingType: '10-K', filingCategory: 'Annual', reportDate: '2024-09-28', filingReference: 'ref1' },
        { submissionId: 101, filingType: '10-Q', filingCategory: 'Quarterly', reportDate: '2024-06-29', filingReference: 'ref2' }
      ]
    });
  }

  it('should create and fire initial requests', () => {
    const fixture = TestBed.createComponent(CompanyComponent);
    fixture.detectChanges();
    flushInitialRequests();
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.componentInstance.company()).toBeTruthy();
  });

  it('should render CIK heading and ticker badges', () => {
    const fixture = TestBed.createComponent(CompanyComponent);
    fixture.detectChanges();
    flushInitialRequests();
    fixture.detectChanges();

    const heading = fixture.nativeElement.querySelector('h2');
    expect(heading.textContent).toContain('Apple Inc');

    const badges = fixture.nativeElement.querySelectorAll('.badge');
    expect(badges.length).toBe(2);
    expect(badges[0].textContent).toContain('AAPL');
  });

  it('should render submissions table', () => {
    const fixture = TestBed.createComponent(CompanyComponent);
    fixture.detectChanges();
    flushInitialRequests();
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.submission-row');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('2024-09-28');
    expect(rows[0].textContent).toContain('10-K');
  });

  it('should expand row and load statements', () => {
    const fixture = TestBed.createComponent(CompanyComponent);
    fixture.detectChanges();
    flushInitialRequests();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.toggleRow(100);

    const stmtReq = httpMock.expectOne('/api/companies/320193/submissions/100/statements');
    stmtReq.flush([
      { roleName: 'BalanceSheet', rootConceptName: 'StatementOfFinancialPositionAbstract', rootLabel: 'Balance Sheet' },
      { roleName: 'IncomeStatement', rootConceptName: 'IncomeStatementAbstract', rootLabel: 'Income Statement' }
    ]);

    fixture.detectChanges();

    const stmtLinks = fixture.nativeElement.querySelectorAll('.statement-table a');
    expect(stmtLinks.length).toBe(2);
    expect(stmtLinks[0].textContent).toContain('Balance Sheet');
    expect(stmtLinks[1].textContent).toContain('Income Statement');
  });

  it('should render Graham Score link', () => {
    const fixture = TestBed.createComponent(CompanyComponent);
    fixture.detectChanges();
    flushInitialRequests();
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('.scoring-link');
    expect(link).toBeTruthy();
    expect(link.textContent).toContain('Graham Score');
  });

  it('should show error on company load failure', () => {
    const fixture = TestBed.createComponent(CompanyComponent);
    fixture.detectChanges();

    const companyReq = httpMock.expectOne('/api/companies/320193');
    companyReq.error(new ProgressEvent('error'));

    const subsReq = httpMock.expectOne('/api/companies/320193/submissions');
    subsReq.flush({ items: [] });

    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('.error');
    expect(error?.textContent).toContain('Failed to load');
  });
});
