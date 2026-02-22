import { Component, computed, input } from '@angular/core';
import { SparklineData } from '../../sparkline.utils';

function pctChange(start: number, end: number): string {
  if (start === 0) return end > 0 ? '+\u221E%' : end < 0 ? '-\u221E%' : '0.0%';
  const pct = ((end - start) / Math.abs(start)) * 100;
  const sign = pct > 0 ? '+' : '';
  return sign + pct.toFixed(1) + '%';
}

function pctClass(start: number, end: number): string {
  if (end > start) return 'positive';
  if (end < start) return 'negative';
  return 'neutral';
}

@Component({
  selector: 'app-sparkline-chart',
  standalone: true,
  template: `
    @if (data(); as d) {
      <div class="sparkline-wrapper">
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
              [attr.points]="d.trend.trendLine"
              fill="none"
              stroke="#f59e0b"
              stroke-width="1.5"
              stroke-dasharray="4 3" />
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
        <div class="change-badges">
          <div class="change-badge" [class]="totalClass()">
            <span class="change-label">Total</span>
            <span>{{ totalLabel() }}</span>
          </div>
          <div class="change-badge" [class]="trendClass()">
            <span class="change-label trend-label">Trend
              <span class="info-icon" data-tooltip="Least-squares linear regression (best-fit straight line) through all data points. The percentage reflects the change from the fitted start value to the fitted end value.">&#9432;</span>
            </span>
            <span>{{ trendLabel() }}</span>
          </div>
        </div>
      </div>
    } @else {
      <div class="sparkline-wrapper">
        <div class="sparkline-empty">Insufficient data</div>
      </div>
    }
  `,
  styles: [`
    .sparkline-wrapper {
      display: flex;
      align-items: center;
      gap: 12px;
      flex-shrink: 0;
    }
    .sparkline-container {
      width: 360px;
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
    .change-badges {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .change-badge {
      display: flex;
      flex-direction: column;
      align-items: center;
      white-space: nowrap;
      font-size: 15px;
      font-weight: 600;
      line-height: 1.3;
    }
    .change-label {
      font-size: 10px;
      font-weight: 500;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      color: #64748b;
    }
    .trend-label {
      color: #f59e0b;
    }
    .change-badge.positive { color: #16a34a; }
    .change-badge.negative { color: #dc2626; }
    .change-badge.neutral { color: #64748b; }
    .trend-label .info-icon {
      font-size: 12px;
      vertical-align: middle;
    }
    .trend-label .info-icon:hover::after {
      text-transform: none;
    }
  `],
  styleUrls: ['../../styles/info-tooltip.css']
})
export class SparklineChartComponent {
  data = input<SparklineData | null>(null);
  formatTooltip = input<((val: number) => string) | null>(null);

  totalLabel = computed(() => {
    const d = this.data();
    if (!d || d.points.length < 2) return '';
    return pctChange(d.points[0].value, d.points[d.points.length - 1].value);
  });

  totalClass = computed(() => {
    const d = this.data();
    if (!d || d.points.length < 2) return 'neutral';
    return pctClass(d.points[0].value, d.points[d.points.length - 1].value);
  });

  trendLabel = computed(() => {
    const d = this.data();
    if (!d || d.points.length < 2) return '';
    return pctChange(d.trend.fitStart, d.trend.fitEnd);
  });

  trendClass = computed(() => {
    const d = this.data();
    if (!d || d.points.length < 2) return 'neutral';
    return pctClass(d.trend.fitStart, d.trend.fitEnd);
  });
}
