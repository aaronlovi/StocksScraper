import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { ReportComponent } from './report.component';

const mockActivatedRoute = {
  snapshot: {
    paramMap: {
      get: (key: string) => {
        const map: Record<string, string> = {
          cik: '320193',
          submissionId: '100',
          concept: 'BalanceSheetAbstract'
        };
        return map[key] ?? null;
      }
    }
  }
};

const mockStatementResponse = {
  conceptName: 'BalanceSheetAbstract',
  label: 'Balance Sheet',
  value: null,
  children: [
    {
      conceptName: 'Assets',
      label: 'Total Assets',
      value: '352583000000',
      children: [
        { conceptName: 'CurrentAssets', label: 'Current Assets', value: '135405000000' }
      ]
    },
    {
      conceptName: 'Liabilities',
      label: 'Total Liabilities',
      value: '290437000000',
      children: []
    }
  ]
};

describe('ReportComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReportComponent],
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

  it('should render breadcrumb with CIK and concept', () => {
    const fixture = TestBed.createComponent(ReportComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(r => r.url.includes('/api/companies/320193/submissions/100/statements/BalanceSheetAbstract'));
    req.flush(mockStatementResponse);
    fixture.detectChanges();

    const breadcrumb = fixture.nativeElement.querySelector('.breadcrumb');
    expect(breadcrumb.textContent).toContain('320193');
    expect(breadcrumb.textContent).toContain('BalanceSheetAbstract');
  });

  it('should render tree table from statement data', () => {
    const fixture = TestBed.createComponent(ReportComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(r => r.url.includes('/statements/BalanceSheetAbstract'));
    req.flush(mockStatementResponse);
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.tree-row');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Total Assets');
    expect(rows[1].textContent).toContain('Total Liabilities');
  });

  it('should show error on failure', () => {
    const fixture = TestBed.createComponent(ReportComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(r => r.url.includes('/statements/BalanceSheetAbstract'));
    req.error(new ProgressEvent('error'));
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('.error');
    expect(error?.textContent).toContain('Failed to load');
  });

  it('should reload with taxonomy year when selector changes', () => {
    const fixture = TestBed.createComponent(ReportComponent);
    fixture.detectChanges();

    const req1 = httpMock.expectOne(r => r.url.includes('/statements/BalanceSheetAbstract') && !r.url.includes('taxonomyYear'));
    req1.flush(mockStatementResponse);
    fixture.detectChanges();

    // Change taxonomy year
    const component = fixture.componentInstance;
    component.selectedYear = 2024;
    component.onYearChange();

    const req2 = httpMock.expectOne(r => r.url.includes('taxonomyYear=2024'));
    req2.flush(mockStatementResponse);
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.tree-row');
    expect(rows.length).toBe(2);
  });
});
