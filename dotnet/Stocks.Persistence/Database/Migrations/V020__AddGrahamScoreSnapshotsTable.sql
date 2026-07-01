-- Historical point-in-time Graham score snapshots.
-- Populated by the --compute-score-snapshots CLI command (delete + bulk insert per as_of_date).
-- Each row is a company's score computed using only information available on as_of_date:
-- filings with acceptance_datetime <= as_of_date and the price at/just before as_of_date.
-- Queried by the graham-snapshot and graham-backtest report endpoints.
CREATE TABLE graham_score_snapshots (
    as_of_date date NOT NULL,
    company_id bigint NOT NULL,
    cik bigint NOT NULL,
    company_name varchar(200),
    ticker varchar(20),
    exchange varchar(50),
    overall_score int NOT NULL,
    computable_checks int NOT NULL,
    years_of_data int NOT NULL,
    book_value decimal,
    market_cap decimal,
    debt_to_equity_ratio decimal,
    price_to_book_ratio decimal,
    debt_to_book_ratio decimal,
    adjusted_retained_earnings decimal,
    average_net_cash_flow decimal,
    average_owner_earnings decimal,
    average_roe_cf decimal,
    average_roe_oe decimal,
    estimated_return_cf decimal,
    estimated_return_oe decimal,
    price_per_share decimal,
    price_date date,
    shares_outstanding bigint,
    current_dividends_paid decimal,
    max_buy_price decimal,
    percentage_upside decimal,
    computed_at timestamptz NOT NULL DEFAULT NOW(),
    PRIMARY KEY (as_of_date, company_id)
);

CREATE INDEX idx_graham_score_snapshots_date_score
    ON graham_score_snapshots (as_of_date, overall_score DESC);

COMMENT ON TABLE graham_score_snapshots IS 'Point-in-time Graham 15-point scores per company per as-of date, computed without look-ahead (only filings accepted by, and prices at, the as-of date). Written by --compute-score-snapshots.';
COMMENT ON COLUMN graham_score_snapshots.as_of_date IS 'Historical date the score is computed as of; only information public by this date is used.';
COMMENT ON COLUMN graham_score_snapshots.company_id IS 'Internal company id (FK to companies).';
COMMENT ON COLUMN graham_score_snapshots.cik IS 'SEC Central Index Key of the company.';
COMMENT ON COLUMN graham_score_snapshots.company_name IS 'Company display name at compute time.';
COMMENT ON COLUMN graham_score_snapshots.ticker IS 'Primary ticker symbol used for price lookups.';
COMMENT ON COLUMN graham_score_snapshots.exchange IS 'Listing exchange of the ticker.';
COMMENT ON COLUMN graham_score_snapshots.overall_score IS 'Number of the 15 Graham checks that passed.';
COMMENT ON COLUMN graham_score_snapshots.computable_checks IS 'Number of the 15 checks that had enough data to evaluate.';
COMMENT ON COLUMN graham_score_snapshots.years_of_data IS 'Count of annual (10-K) report years available as of the as-of date.';
COMMENT ON COLUMN graham_score_snapshots.book_value IS 'Equity minus goodwill and intangibles, from the most recent filing as of the as-of date.';
COMMENT ON COLUMN graham_score_snapshots.market_cap IS 'Price per share times shares outstanding at the as-of date.';
COMMENT ON COLUMN graham_score_snapshots.debt_to_equity_ratio IS 'Long-term debt divided by equity.';
COMMENT ON COLUMN graham_score_snapshots.price_to_book_ratio IS 'Market cap divided by book value.';
COMMENT ON COLUMN graham_score_snapshots.debt_to_book_ratio IS 'Long-term debt divided by book value.';
COMMENT ON COLUMN graham_score_snapshots.adjusted_retained_earnings IS 'Retained earnings adjusted for dividends and net stock issuance over the covered years.';
COMMENT ON COLUMN graham_score_snapshots.average_net_cash_flow IS 'Average annual net cash flow excluding financing effects.';
COMMENT ON COLUMN graham_score_snapshots.average_owner_earnings IS 'Average annual owner earnings (Buffett formula).';
COMMENT ON COLUMN graham_score_snapshots.average_roe_cf IS 'Average annual return on equity using net cash flow.';
COMMENT ON COLUMN graham_score_snapshots.average_roe_oe IS 'Average annual return on equity using owner earnings.';
COMMENT ON COLUMN graham_score_snapshots.estimated_return_cf IS 'Estimated annual return from net cash flow relative to market cap, percent.';
COMMENT ON COLUMN graham_score_snapshots.estimated_return_oe IS 'Estimated annual return from owner earnings relative to market cap, percent.';
COMMENT ON COLUMN graham_score_snapshots.price_per_share IS 'Closing price at/just before the as-of date used for the score.';
COMMENT ON COLUMN graham_score_snapshots.price_date IS 'Actual trading date of price_per_share (may precede as_of_date on non-trading days).';
COMMENT ON COLUMN graham_score_snapshots.shares_outstanding IS 'Shares outstanding from the most recent filing as of the as-of date.';
COMMENT ON COLUMN graham_score_snapshots.current_dividends_paid IS 'Dividends paid in the most recent annual year as of the as-of date.';
COMMENT ON COLUMN graham_score_snapshots.max_buy_price IS 'Maximum buy price per share from the TSX formula.';
COMMENT ON COLUMN graham_score_snapshots.percentage_upside IS 'Percent difference between max buy price and price per share.';
COMMENT ON COLUMN graham_score_snapshots.computed_at IS 'Wall-clock time the snapshot batch computed this row.';
