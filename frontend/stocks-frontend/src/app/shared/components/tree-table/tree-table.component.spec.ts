import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { TreeTableComponent } from './tree-table.component';
import { StatementTreeNode } from '../../../core/services/api.service';

const mockNodes: StatementTreeNode[] = [
  {
    conceptName: 'Assets',
    label: 'Total Assets',
    value: '123456789',
    children: [
      { conceptName: 'CurrentAssets', label: 'Current Assets', value: '50000000', children: [] },
      { conceptName: 'NonCurrentAssets', label: 'Non-Current Assets', value: '73456789', children: [] }
    ]
  },
  {
    conceptName: 'Liabilities',
    label: 'Total Liabilities',
    value: '80000000',
    children: []
  }
];

@Component({
  standalone: true,
  imports: [TreeTableComponent],
  template: '<app-tree-table [nodes]="nodes" />'
})
class TestHostComponent {
  nodes: StatementTreeNode[] = mockNodes;
}

describe('TreeTableComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHostComponent]
    }).compileComponents();
  });

  it('should render root nodes as top-level rows', () => {
    const fixture = TestBed.createComponent(TestHostComponent);
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.tree-row');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Total Assets');
    expect(rows[1].textContent).toContain('Total Liabilities');
  });

  it('should format numeric values with commas', () => {
    const fixture = TestBed.createComponent(TestHostComponent);
    fixture.detectChanges();

    const valueCells = fixture.nativeElement.querySelectorAll('.value-col:not(th)');
    expect(valueCells[0].textContent.trim()).toBe('123,456,789');
  });

  it('should expand parent row to show children', () => {
    const fixture = TestBed.createComponent(TestHostComponent);
    fixture.detectChanges();

    // Initially 2 rows (collapsed)
    let rows = fixture.nativeElement.querySelectorAll('.tree-row');
    expect(rows.length).toBe(2);

    // Click "Total Assets" to expand
    const treeTable = fixture.debugElement.children[0].componentInstance as TreeTableComponent;
    treeTable.toggle('Assets');
    fixture.detectChanges();

    // Now should show 4 rows (Assets + 2 children + Liabilities)
    rows = fixture.nativeElement.querySelectorAll('.tree-row');
    expect(rows.length).toBe(4);
    expect(rows[1].textContent).toContain('Current Assets');
    expect(rows[2].textContent).toContain('Non-Current Assets');
  });

  it('should collapse expanded parent row', () => {
    const fixture = TestBed.createComponent(TestHostComponent);
    fixture.detectChanges();

    const treeTable = fixture.debugElement.children[0].componentInstance as TreeTableComponent;

    // Expand then collapse
    treeTable.toggle('Assets');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.tree-row').length).toBe(4);

    treeTable.toggle('Assets');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.tree-row').length).toBe(2);
  });
});
