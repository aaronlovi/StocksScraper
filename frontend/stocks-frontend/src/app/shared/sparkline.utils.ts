export interface SparklinePoint { x: number; y: number; label: string; value: number; }
export interface SparklineTick { y: number; label: string; }
export interface SparklineTrend {
  trendLine: string;
  fitStart: number;
  fitEnd: number;
}
export interface SparklineData {
  points: SparklinePoint[];
  yTicks: SparklineTick[];
  polyline: string;
  trend: SparklineTrend;
  axisLeft: number;
  axisRight: number;
  axisTop: number;
  axisBottom: number;
}

export function computeSparkline(
  data: { label: string; value: number }[],
  options?: { yAxisFormat?: 'percent' | 'currency'; forceZero?: boolean; viewBoxWidth?: number; viewBoxHeight?: number }
): SparklineData | null {
  if (data.length < 2) return null;

  const width = options?.viewBoxWidth ?? 240;
  const height = options?.viewBoxHeight ?? 120;
  const padLeft = 35;
  const padRight = 10;
  const padTop = 10;
  const padBottom = 20;
  const plotW = width - padLeft - padRight;
  const plotH = height - padTop - padBottom;
  const format = options?.yAxisFormat ?? 'percent';

  let minV = data[0].value;
  let maxV = data[0].value;
  for (const item of data) {
    if (item.value < minV) minV = item.value;
    if (item.value > maxV) maxV = item.value;
  }

  // Ensure a non-zero range
  if (minV === maxV) {
    if (minV === 0) { maxV = 1; }
    else { minV = minV - Math.abs(minV) * 0.1; maxV = maxV + Math.abs(maxV) * 0.1; }
  }

  // Compute nice axis bounds
  let niceMin: number;
  let niceMax: number;
  let tickStep: number;

  if (format === 'currency') {
    // For currency (revenue), auto-scale
    const range = maxV - minV;
    const rawStep = range / 4;
    const magnitude = Math.pow(10, Math.floor(Math.log10(rawStep)));
    tickStep = Math.ceil(rawStep / magnitude) * magnitude;
    niceMin = Math.floor(minV / tickStep) * tickStep;
    niceMax = Math.ceil(maxV / tickStep) * tickStep;
    if (niceMin === niceMax) niceMax = niceMin + tickStep;
    if (options?.forceZero && niceMin > 0) niceMin = 0;
  } else {
    // For percent values, floor/ceil to integers
    const rawMin = Math.floor(minV);
    const rawMax = Math.ceil(maxV);
    const rawRange = rawMax - rawMin;
    if (rawRange <= 5) { tickStep = 1; }
    else if (rawRange <= 20) { tickStep = 5; }
    else { tickStep = 10; }
    niceMin = Math.floor(rawMin / tickStep) * tickStep;
    niceMax = Math.ceil(rawMax / tickStep) * tickStep;
    if (niceMin === niceMax) niceMax = niceMin + tickStep;

    // Force percent Y-axis to include zero for honest representation
    if (niceMin >= 0) {
      niceMin = 0;
    } else {
      const padded = minV * 1.2; // 20% beyond most negative value
      const paddedNice = Math.floor(padded / tickStep) * tickStep;
      niceMin = Math.max(-100, paddedNice);
    }
  }

  const rangeV = niceMax - niceMin;

  // Y-axis ticks
  const yTicks: SparklineTick[] = [];
  for (let v = niceMin; v <= niceMax + tickStep * 0.001; v += tickStep) {
    const y = padTop + plotH - ((v - niceMin) / rangeV) * plotH;
    const roundedY = Math.round(y * 10) / 10;
    let label: string;
    if (format === 'currency') {
      label = formatCurrencyTick(v);
    } else {
      label = Math.round(v) + '%';
    }
    yTicks.push({ y: roundedY, label });
  }

  // Data points
  const points: SparklinePoint[] = [];
  for (let i = 0; i < data.length; i++) {
    const x = padLeft + (i / (data.length - 1)) * plotW;
    const y = padTop + plotH - ((data[i].value - niceMin) / rangeV) * plotH;
    points.push({
      x: Math.round(x * 10) / 10,
      y: Math.round(y * 10) / 10,
      label: data[i].label,
      value: data[i].value
    });
  }

  let polyline = '';
  for (const pt of points) {
    if (polyline.length > 0) polyline += ' ';
    polyline += pt.x + ',' + pt.y;
  }

  // Linear regression (least squares)
  const n = data.length;
  let sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
  for (let i = 0; i < n; i++) {
    sumX += i;
    sumY += data[i].value;
    sumXY += i * data[i].value;
    sumX2 += i * i;
  }
  const slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
  const intercept = (sumY - slope * sumX) / n;
  const fitStart = intercept;
  const fitEnd = intercept + slope * (n - 1);

  const trendX1 = padLeft;
  const trendY1 = padTop + plotH - ((fitStart - niceMin) / rangeV) * plotH;
  const trendX2 = padLeft + plotW;
  const trendY2 = padTop + plotH - ((fitEnd - niceMin) / rangeV) * plotH;
  const trendLine = Math.round(trendX1 * 10) / 10 + ',' + Math.round(trendY1 * 10) / 10 +
    ' ' + Math.round(trendX2 * 10) / 10 + ',' + Math.round(trendY2 * 10) / 10;

  return {
    points,
    yTicks,
    polyline,
    trend: { trendLine, fitStart, fitEnd },
    axisLeft: padLeft,
    axisRight: width - padRight,
    axisTop: padTop,
    axisBottom: padTop + plotH
  };
}

function formatCurrencyTick(value: number): string {
  const abs = Math.abs(value);
  const sign = value < 0 ? '-' : '';
  if (abs >= 1e12) return sign + '$' + (abs / 1e12).toFixed(1) + 'T';
  if (abs >= 1e9) return sign + '$' + (abs / 1e9).toFixed(1) + 'B';
  if (abs >= 1e6) return sign + '$' + (abs / 1e6).toFixed(0) + 'M';
  if (abs >= 1e3) return sign + '$' + (abs / 1e3).toFixed(0) + 'K';
  return sign + '$' + abs.toFixed(0);
}
