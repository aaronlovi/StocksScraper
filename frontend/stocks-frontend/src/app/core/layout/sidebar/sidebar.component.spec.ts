import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SidebarComponent } from './sidebar.component';

describe('SidebarComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render Dashboard link', async () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    await fixture.whenStable();
    const links = fixture.nativeElement.querySelectorAll('a');
    const texts = Array.from(links).map((a: any) => a.textContent.trim());
    expect(texts).toContain('Dashboard');
  });

  it('should render Search link', async () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    await fixture.whenStable();
    const links = fixture.nativeElement.querySelectorAll('a');
    const texts = Array.from(links).map((a: any) => a.textContent.trim());
    expect(texts).toContain('Search');
  });
});
