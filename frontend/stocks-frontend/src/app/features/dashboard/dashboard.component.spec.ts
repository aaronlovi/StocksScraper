import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
    httpMock.expectOne('/api/dashboard/stats');
  });

  it('should display stats cards', async () => {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne('/api/dashboard/stats');
    req.flush({
      totalCompanies: 1500,
      totalSubmissions: 8000,
      totalDataPoints: 500000,
      earliestFilingDate: '2020-01-15',
      latestFilingDate: '2024-12-31',
      companiesWithPriceData: 200,
      submissionsByFilingType: {}
    });

    fixture.detectChanges();
    await fixture.whenStable();

    const values = fixture.nativeElement.querySelectorAll('.card-value');
    const texts = Array.from(values).map((el: any) => el.textContent.trim());
    expect(texts).toContain('1,500');
    expect(texts).toContain('8,000');
    expect(texts).toContain('500,000');
  });

  it('should show error on failure', async () => {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne('/api/dashboard/stats');
    req.error(new ProgressEvent('error'));

    fixture.detectChanges();
    await fixture.whenStable();

    const error = fixture.nativeElement.querySelector('.error');
    expect(error?.textContent).toContain('Failed to load');
  });
});
