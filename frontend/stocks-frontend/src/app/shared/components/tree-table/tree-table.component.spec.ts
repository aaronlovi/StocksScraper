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

  it('should render all rows including nested children immediately', () => {
    const fixture = TestBed.createComponent(TestHostComponent);
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.tree-row');
    expect(rows.length).toBe(4);
    expect(rows[0].textContent).toContain('Total Assets');
    expect(rows[1].textContent).toContain('Current Assets');
    expect(rows[2].textContent).toContain('Non-Current Assets');
    expect(rows[3].textContent).toContain('Total Liabilities');
  });

  it('should format numeric values with commas', () => {
    const fixture = TestBed.createComponent(TestHostComponent);
    fixture.detectChanges();

    const valueCells = fixture.nativeElement.querySelectorAll('.value-col:not(th)');
    expect(valueCells[0].textContent.trim()).toBe('123,456,789');
  });

  it('should apply section-header class to parent rows without values', () => {
    const nodesWithSectionHeader: StatementTreeNode[] = [
      {
        conceptName: 'Section',
        label: 'Section Header',
        value: null as unknown as string,
        children: [
          { conceptName: 'Child', label: 'Child Item', value: '100', children: [] }
        ]
      }
    ];

    const fixture = TestBed.createComponent(TestHostComponent);
    fixture.componentInstance.nodes = nodesWithSectionHeader;
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.tree-row');
    expect(rows.length).toBe(2);
    expect(rows[0].classList.contains('section-header')).toBe(true);
    expect(rows[1].classList.contains('section-header')).toBe(false);
  });
});
