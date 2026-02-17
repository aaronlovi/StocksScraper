import { TestBed } from '@angular/core/testing';
import { TitlebarComponent } from './titlebar.component';

describe('TitlebarComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TitlebarComponent]
    }).compileComponents();
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

  it('should emit search event on enter', async () => {
    const fixture = TestBed.createComponent(TitlebarComponent);
    const component = fixture.componentInstance;
    await fixture.whenStable();

    let emitted = '';
    component.search.subscribe((value: string) => emitted = value);

    component.searchText = 'Apple';
    component.onSearch();

    expect(emitted).toBe('Apple');
  });
});
