import { TestBed } from '@angular/core/testing';
import { CsvExportButtonComponent } from './csv-export-button.component';
import { autoColumns, toCsv } from '../../csv.utils';

describe('csv.utils', () => {
  it('should derive columns from the first row', () => {
    const cols = autoColumns([{ ticker: 'AAPL', price: 100 }]);
    expect(cols.map(c => c.header)).toEqual(['ticker', 'price']);
  });

  it('should escape commas, quotes, and newlines', () => {
    const rows = [{ name: 'Say "hi", world', note: 'line1\nline2', plain: 5 }];
    const csv = toCsv(rows, autoColumns(rows));
    expect(csv).toBe('name,note,plain\r\n"Say ""hi"", world","line1\nline2",5\r\n');
  });

  it('should render nulls as empty cells and arrays joined with semicolons', () => {
    const rows = [{ a: null, b: ['x', 'y'], c: undefined }];
    const csv = toCsv(rows, autoColumns(rows));
    expect(csv).toBe('a,b,c\r\n,x; y,\r\n');
  });

  it('should apply explicit column accessors', () => {
    const rows = [{ nested: { value: 42 } }];
    const csv = toCsv(rows, [{ header: 'The Value', value: (r: never) => (r as { nested: { value: number } }).nested.value }]);
    expect(csv).toBe('The Value\r\n42\r\n');
  });
});

describe('CsvExportButtonComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CsvExportButtonComponent]
    }).compileComponents();
  });

  it('should be disabled with no rows', () => {
    const fixture = TestBed.createComponent(CsvExportButtonComponent);
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.disabled).toBe(true);
  });

  it('should be enabled with rows and show the label', () => {
    const fixture = TestBed.createComponent(CsvExportButtonComponent);
    fixture.componentInstance.rows = [{ a: 1 }];
    fixture.componentInstance.label = 'Export holdings CSV';
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.disabled).toBe(false);
    expect(button.textContent).toContain('Export holdings CSV');
  });

  it('should build a blob and trigger a download on click', () => {
    const fixture = TestBed.createComponent(CsvExportButtonComponent);
    fixture.componentInstance.rows = [{ ticker: 'AAPL', price: 100 }];
    fixture.componentInstance.filename = 'test-table';
    fixture.detectChanges();

    const createSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fake');
    const revokeSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);

    fixture.componentInstance.export();

    expect(createSpy).toHaveBeenCalledOnce();
    expect(clickSpy).toHaveBeenCalledOnce();
    expect(revokeSpy).toHaveBeenCalledWith('blob:fake');

    createSpy.mockRestore();
    revokeSpy.mockRestore();
    clickSpy.mockRestore();
  });
});
