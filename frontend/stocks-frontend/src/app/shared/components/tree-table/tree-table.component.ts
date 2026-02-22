import { Component, input } from '@angular/core';
import { StatementTreeNode } from '../../../core/services/api.service';

@Component({
  selector: 'app-tree-table',
  standalone: true,
  templateUrl: './tree-table.component.html',
  styleUrls: ['./tree-table.component.css']
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
