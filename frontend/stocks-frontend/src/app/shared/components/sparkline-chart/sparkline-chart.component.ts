import { Component, input } from '@angular/core';
import { SparklineData } from '../../sparkline.utils';

@Component({
  selector: 'app-sparkline-chart',
  standalone: true,
  template: `
    @if (data(); as d) {
      <div class="sparkline-container">
        <svg viewBox="0 0 240 120" class="sparkline-svg">
          @for (tick of d.yTicks; track tick.label) {
            <line [attr.x1]="d.axisLeft" [attr.y1]="tick.y"
                  [attr.x2]="d.axisRight" [attr.y2]="tick.y"
                  class="grid-line" />
            <text [attr.x]="d.axisLeft - 4" [attr.y]="tick.y + 2.5"
                  text-anchor="end" class="axis-label">{{ tick.label }}</text>
          }
          <line [attr.x1]="d.axisLeft" [attr.y1]="d.axisTop"
                [attr.x2]="d.axisLeft" [attr.y2]="d.axisBottom"
                class="axis-line" />
          <line [attr.x1]="d.axisLeft" [attr.y1]="d.axisBottom"
                [attr.x2]="d.axisRight" [attr.y2]="d.axisBottom"
                class="axis-line" />
          <polyline
            [attr.points]="d.polyline"
            fill="none"
            stroke="#3b82f6"
            stroke-width="2"
            stroke-linejoin="round"
            stroke-linecap="round" />
          @for (pt of d.points; track pt.label) {
            <circle [attr.cx]="pt.x" [attr.cy]="pt.y" r="3" fill="#3b82f6">
              <title>{{ pt.label }}: {{ formatTooltip() ? formatTooltip()!(pt.value) : pt.value }}</title>
            </circle>
            <text [attr.x]="pt.x" [attr.y]="d.axisBottom + 12"
                  text-anchor="middle" class="axis-label">{{ pt.label }}</text>
          }
        </svg>
      </div>
    } @else {
      <div class="sparkline-empty">Not enough data</div>
    }
  `,
  styles: [`
    .sparkline-container {
      flex-shrink: 0;
      width: 300px;
      padding-top: 8px;
    }
    .sparkline-svg {
      width: 100%;
      height: auto;
    }
    .sparkline-empty {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 300px;
      height: 120px;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      color: #94a3b8;
      font-size: 13px;
    }
    .axis-line {
      stroke: #94a3b8;
      stroke-width: 1;
    }
    .grid-line {
      stroke: #e2e8f0;
      stroke-width: 0.5;
    }
    .axis-label {
      font-size: 6.5px;
      fill: #64748b;
    }
  `]
})
export class SparklineChartComponent {
  data = input<SparklineData | null>(null);
  formatTooltip = input<((val: number) => string) | null>(null);
}
