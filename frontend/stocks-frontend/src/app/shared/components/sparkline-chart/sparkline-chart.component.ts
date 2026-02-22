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
  templateUrl: './sparkline-chart.component.html',
  styleUrls: ['./sparkline-chart.component.css', '../../styles/info-tooltip.css']
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
