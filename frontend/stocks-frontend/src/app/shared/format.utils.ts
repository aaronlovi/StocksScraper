export function fmtCurrency(val: number | null | undefined, fallback = 'N/A'): string {
  if (val == null) return fallback;
  const sign = val < 0 ? '-' : '';
  const abs = Math.abs(val);
  if (abs >= 1_000_000_000_000) return sign + '$' + (abs / 1_000_000_000_000).toFixed(2) + 'T';
  if (abs >= 1_000_000_000) return sign + '$' + (abs / 1_000_000_000).toFixed(2) + 'B';
  if (abs >= 1_000_000) return sign + '$' + (abs / 1_000_000).toFixed(2) + 'M';
  return sign + '$' + abs.toFixed(2);
}

export function fmtPct(val: number | null | undefined, fallback = 'N/A'): string {
  if (val == null) return fallback;
  return val.toFixed(2) + '%';
}

export function fmtRatio(val: number | null | undefined, fallback = 'N/A'): string {
  if (val == null) return fallback;
  return val.toFixed(2);
}

export function fmtPrice(val: number | null | undefined, fallback = 'N/A'): string {
  if (val == null) return fallback;
  return '$' + val.toFixed(2);
}

export function formatAbbrev(value: number | null | undefined, fallback = 'N/A'): string {
  if (value == null) return fallback;
  const abs = Math.abs(value);
  const sign = value < 0 ? '-' : '';
  if (abs >= 1e12) return sign + '$' + (abs / 1e12).toFixed(2) + 'T';
  if (abs >= 1e9) return sign + '$' + (abs / 1e9).toFixed(2) + 'B';
  if (abs >= 1e6) return sign + '$' + (abs / 1e6).toFixed(2) + 'M';
  if (abs >= 1e3) return sign + '$' + (abs / 1e3).toFixed(1) + 'K';
  return sign + '$' + abs.toFixed(0);
}

export function fmtReturn(val: number | null | undefined, fallback = 'N/A'): string {
  if (val == null) return fallback;
  const sign = val > 0 ? '+' : '';
  return sign + val.toFixed(2) + '%';
}

export function fmtInvested(val: number | null | undefined, fallback = 'N/A'): string {
  if (val == null) return fallback;
  return '$' + val.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

export function returnClass(val: number | null | undefined): string {
  if (val == null) return '';
  if (val > 0) return 'positive';
  if (val < 0) return 'negative';
  return '';
}

export function scoreBadgeClass(score: number | null | undefined): string {
  if (score == null) return '';
  if (score >= 10) return 'score-green';
  if (score >= 7) return 'score-yellow';
  return 'score-red';
}

export function rowHighlightClass(score: number | null | undefined, computableChecks: number | null | undefined, maxChecks?: number): string {
  if (score == null || computableChecks == null) return '';
  if (maxChecks != null && computableChecks !== maxChecks) return '';
  if (score === computableChecks) return 'row-perfect';
  if (score === computableChecks - 1) return 'row-near-perfect';
  return '';
}
