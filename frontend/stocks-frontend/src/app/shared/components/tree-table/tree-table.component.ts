import { Component, input, signal } from '@angular/core';
import { StatementTreeNode } from '../../../core/services/api.service';

@Component({
  selector: 'app-tree-table',
  standalone: true,
  template: `
    <table>
      <thead>
        <tr>
          <th>Label</th>
          <th class="value-col">Value</th>
        </tr>
      </thead>
      <tbody>
        @for (row of flatRows(); track row.node.conceptName + row.depth) {
          <tr class="tree-row" [class.has-children]="row.hasChildren" (click)="toggle(row.node.conceptName)">
            <td>
              <span class="indent" [style.padding-left.px]="row.depth * 20">
                @if (row.hasChildren) {
                  <span class="toggle">{{ isExpanded(row.node.conceptName) ? '▾' : '▸' }}</span>
                }
                {{ row.node.label }}
              </span>
            </td>
            <td class="value-col">
              @if (row.node.value !== null && row.node.value !== undefined) {
                {{ formatValue(row.node.value) }}
              }
            </td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styles: [`
    table {
      width: 100%;
      border-collapse: collapse;
    }
    th, td {
      padding: 4px 12px;
      border-bottom: 1px solid #e2e8f0;
      text-align: left;
    }
    th {
      background: #f1f5f9;
      font-weight: 600;
    }
    .value-col {
      text-align: right;
      width: 200px;
    }
    .tree-row.has-children {
      cursor: pointer;
    }
    .tree-row.has-children:hover {
      background: #f8fafc;
    }
    .indent {
      display: inline-block;
    }
    .toggle {
      color: #94a3b8;
      margin-right: 4px;
    }
  `]
})
export class TreeTableComponent {
  nodes = input<StatementTreeNode[]>([]);
  expandedNodes = signal<Set<string>>(new Set());

  flatRows(): { node: StatementTreeNode; depth: number; hasChildren: boolean }[] {
    const rows: { node: StatementTreeNode; depth: number; hasChildren: boolean }[] = [];
    const expanded = this.expandedNodes();

    const walk = (nodeList: StatementTreeNode[], depth: number) => {
      for (const node of nodeList) {
        const hasChildren = !!node.children && node.children.length > 0;
        rows.push({ node, depth, hasChildren });
        if (hasChildren && expanded.has(node.conceptName)) {
          walk(node.children!, depth + 1);
        }
      }
    };

    walk(this.nodes(), 0);
    return rows;
  }

  isExpanded(conceptName: string): boolean {
    return this.expandedNodes().has(conceptName);
  }

  toggle(conceptName: string): void {
    const current = new Set(this.expandedNodes());
    if (current.has(conceptName)) {
      current.delete(conceptName);
    } else {
      current.add(conceptName);
    }
    this.expandedNodes.set(current);
  }

  formatValue(value: string): string {
    const num = parseFloat(value);
    if (isNaN(num)) return value;
    return num.toLocaleString('en-US');
  }
}
