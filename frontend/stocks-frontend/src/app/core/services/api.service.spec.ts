import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ApiService } from './api.service';

describe('ApiService', () => {
  let service: ApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        ApiService
      ]
    });
    service = TestBed.inject(ApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should fetch company by CIK', () => {
    service.getCompany('320193').subscribe(data => {
      expect(data.cik).toBe(320193);
    });
    const req = httpMock.expectOne('/api/companies/320193');
    expect(req.request.method).toBe('GET');
    req.flush({ companyId: 1, cik: 320193, dataSource: 'EDGAR', tickers: [] });
  });

  it('should search companies with pagination', () => {
    service.searchCompanies('Apple', 1, 25).subscribe(data => {
      expect(data.items.length).toBe(1);
    });
    const req = httpMock.expectOne('/api/search?q=Apple&page=1&pageSize=25');
    expect(req.request.method).toBe('GET');
    req.flush({
      items: [{ companyId: 1, cik: '320193', companyName: 'Apple Inc', ticker: 'AAPL', exchange: 'NASDAQ' }],
      pagination: { pageNumber: 1, totalItems: 1, totalPages: 1 }
    });
  });

  it('should fetch dashboard stats', () => {
    service.getDashboardStats().subscribe(data => {
      expect(data.totalCompanies).toBe(100);
    });
    const req = httpMock.expectOne('/api/dashboard/stats');
    expect(req.request.method).toBe('GET');
    req.flush({
      totalCompanies: 100,
      totalSubmissions: 500,
      totalDataPoints: 10000,
      earliestFilingDate: '2020-01-01',
      latestFilingDate: '2024-12-31',
      companiesWithPriceData: 50,
      submissionsByFilingType: {}
    });
  });
});
