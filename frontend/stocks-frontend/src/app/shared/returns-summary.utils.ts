import { CompanyScoreReturnSummary } from '../core/services/api.service';

export interface ReturnsSummary {
  count: number;
  avgTotalReturn: number;
  medianTotalReturn: number;
  avgAnnualizedReturn: number | null;
  avgValueOf1000: number;
  bestTicker: string;
  bestReturn: number;
  worstTicker: string;
  worstReturn: number;
}

export function computeReturnsSummary(rows: CompanyScoreReturnSummary[]): ReturnsSummary | null {
  const withData: CompanyScoreReturnSummary[] = [];
  for (const r of rows) {
    if (r.totalReturnPct != null && r.currentValueOf1000 != null && r.startDate && r.endDate) {
      withData.push(r);
    }
  }
  if (withData.length === 0) return null;

  let sumTotal = 0;
  let sumValue = 0;
  let sumDays = 0;
  let best = withData[0];
  let worst = withData[0];
  for (const r of withData) {
    sumTotal += r.totalReturnPct!;
    sumValue += r.currentValueOf1000!;
    const days = (new Date(r.endDate!).getTime() - new Date(r.startDate!).getTime()) / 86_400_000;
    sumDays += days;
    if (r.totalReturnPct! > best.totalReturnPct!) best = r;
    if (r.totalReturnPct! < worst.totalReturnPct!) worst = r;
  }

  const avgTotalReturn = sumTotal / withData.length;
  const avgDays = sumDays / withData.length;
  let avgAnnualizedReturn: number | null = null;
  if (avgDays > 0) {
    avgAnnualizedReturn = (Math.pow(1 + avgTotalReturn / 100, 365.25 / avgDays) - 1) * 100;
    avgAnnualizedReturn = Math.round(avgAnnualizedReturn * 100) / 100;
  }

  const sorted = withData.map(r => r.totalReturnPct!).sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  const median = sorted.length % 2 === 0
    ? (sorted[mid - 1] + sorted[mid]) / 2
    : sorted[mid];

  return {
    count: withData.length,
    avgTotalReturn: Math.round(avgTotalReturn * 100) / 100,
    medianTotalReturn: Math.round(median * 100) / 100,
    avgAnnualizedReturn: avgAnnualizedReturn,
    avgValueOf1000: Math.round(sumValue / withData.length * 100) / 100,
    bestTicker: best.ticker ?? best.cik,
    bestReturn: best.totalReturnPct!,
    worstTicker: worst.ticker ?? worst.cik,
    worstReturn: worst.totalReturnPct!,
  };
}

export function defaultStartDate(): string {
  const d = new Date();
  d.setMonth(d.getMonth() - 6);
  return d.toISOString().slice(0, 10);
}

export function yahooFinanceSP500Url(startDate: string): string {
  const period1 = Math.floor(new Date(startDate + 'T00:00:00').getTime() / 1000);
  const period2 = Math.floor(Date.now() / 1000);
  return `https://finance.yahoo.com/quote/%5EGSPC/chart/?period1=${period1}&period2=${period2}`;
}

export function googleFinanceSP500Url(): string {
  return `https://www.google.com/finance/quote/.INX:INDEXSP`;
}
