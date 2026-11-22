import { yahooFinanceSP500Url } from './returns-summary.utils';

describe('yahooFinanceSP500Url', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('is stable across calls in the same day', () => {
    // Change detection evaluates the template binding twice in dev mode; a URL that
    // moves with the clock throws NG0100 when a second boundary falls in between.
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-22T14:00:00.999Z'));
    const first = yahooFinanceSP500Url('2026-05-22');
    vi.setSystemTime(new Date('2026-11-22T14:00:01.001Z'));
    const second = yahooFinanceSP500Url('2026-05-22');

    expect(second).toBe(first);
  });

  it('ends the chart at the end of the current UTC day', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-22T14:00:00Z'));

    const url = yahooFinanceSP500Url('2026-05-22');

    const endOfDay = Date.UTC(2026, 10, 23) / 1000;
    expect(url).toContain(`period2=${endOfDay}`);
  });
});
