export interface SparklinePoint { x: number; y: number; label: string; value: number; }
export interface SparklineTick { y: number; label: string; }
export interface SparklineData {
  points: SparklinePoint[];
  yTicks: SparklineTick[];
  polyline: string;
  axisLeft: number;
  axisRight: number;
  axisTop: number;
  axisBottom: number;
}

export function computeSparkline(
  data: { label: string; value: number }[],
  options?: { yAxisFormat?: 'percent' | 'currency'; viewBoxWidth?: number; viewBoxHeight?: number }
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

  return {
    points,
    yTicks,
    polyline,
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
