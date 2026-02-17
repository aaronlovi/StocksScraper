import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SearchComponent } from './search.component';

describe('SearchComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SearchComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(SearchComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render table rows for results', () => {
    const fixture = TestBed.createComponent(SearchComponent);
    const component = fixture.componentInstance;

    component.query = 'Apple';
    fixture.detectChanges();

    component.doSearch();

    const req = httpMock.expectOne(r => r.url.includes('/api/search'));
    req.flush({
      items: [
        { companyId: 1, cik: '320193', companyName: 'Apple Inc', ticker: 'AAPL', exchange: 'NASDAQ', latestPrice: 197.25, latestPriceDate: '2025-06-13' },
        { companyId: 2, cik: '789019', companyName: 'Apple Hospitality', ticker: null, exchange: null, latestPrice: null, latestPriceDate: null }
      ],
      pagination: { pageNumber: 1, totalItems: 2, totalPages: 1 }
    });

    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
  });

  it('should show no results message', () => {
    const fixture = TestBed.createComponent(SearchComponent);
    const component = fixture.componentInstance;

    component.query = 'NonExistent';
    fixture.detectChanges();

    component.doSearch();

    const req = httpMock.expectOne(r => r.url.includes('/api/search'));
    req.flush({
      items: [],
      pagination: { pageNumber: 1, totalItems: 0, totalPages: 0 }
    });

    fixture.detectChanges();

    const noResults = fixture.nativeElement.querySelector('.no-results');
    expect(noResults?.textContent).toContain('No results found');
  });
});
