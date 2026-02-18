import { Component, input } from '@angular/core';
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
          <tr class="tree-row" [class.section-header]="row.hasChildren && !row.node.value">
            <td>
              <span class="indent" [style.padding-left.px]="row.depth * 16">
                {{ row.node.label }}
                @if (row.node.documentation) {
                  <span class="info-icon" [title]="row.node.documentation">&#9432;</span>
                }
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
      font-size: 13px;
    }
    th, td {
      padding: 3px 12px;
      border-bottom: 1px solid #e2e8f0;
      text-align: left;
    }
    th {
      background: #e2e8f0;
      font-weight: 600;
      font-size: 12px;
      text-transform: uppercase;
      color: #475569;
      border-bottom: 3px double #94a3b8;
    }
    .value-col {
      text-align: right;
      width: 200px;
    }
    .tree-row:nth-child(even) td {
      background: #f1f5f9;
    }
    .section-header td {
      font-weight: 600;
    }
    .indent {
      display: inline-block;
    }
    .info-icon {
      margin-left: 4px;
      color: #64748b;
      cursor: pointer;
      font-size: 12px;
    }
    .info-icon:hover {
      color: #3b82f6;
    }
  `]
})
export class TreeTableComponent {
  nodes = input<StatementTreeNode[]>([]);

  flatRows(): { node: StatementTreeNode; depth: number; hasChildren: boolean }[] {
    const rows: { node: StatementTreeNode; depth: number; hasChildren: boolean }[] = [];

    const walk = (nodeList: StatementTreeNode[], depth: number) => {
      for (const node of nodeList) {
        const hasChildren = !!node.children && node.children.length > 0;
        rows.push({ node, depth, hasChildren });
        if (hasChildren) {
          walk(node.children!, depth + 1);
        }
      }
    };

    walk(this.nodes(), 0);
    return rows;
  }

  formatValue(value: string): string {
    const num = parseFloat(value);
    if (isNaN(num)) return value;
    return num.toLocaleString('en-US');
  }
}
