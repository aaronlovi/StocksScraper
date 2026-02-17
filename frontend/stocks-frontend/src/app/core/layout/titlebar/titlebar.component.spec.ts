import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TitlebarComponent } from './titlebar.component';

describe('TitlebarComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TitlebarComponent],
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
    const fixture = TestBed.createComponent(TitlebarComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render search input', async () => {
    const fixture = TestBed.createComponent(TitlebarComponent);
    await fixture.whenStable();
    const input = fixture.nativeElement.querySelector('input');
    expect(input).toBeTruthy();
    expect(input.placeholder).toBe('Search companies...');
  });

  it('should emit search event on enter', () => {
    const fixture = TestBed.createComponent(TitlebarComponent);
    const component = fixture.componentInstance;

    let emitted = '';
    component.search.subscribe((value: string) => emitted = value);

    component.searchText = 'Apple';
    component.onSearch();

    expect(emitted).toBe('Apple');
  });

  it('should select suggestion and navigate', () => {
    const fixture = TestBed.createComponent(TitlebarComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    component.selectSuggestion({ text: 'Apple Inc', type: 'company', cik: '320193' });

    expect(component.searchText).toBe('Apple Inc');
    expect(component.suggestions()).toEqual([]);
  });
});
