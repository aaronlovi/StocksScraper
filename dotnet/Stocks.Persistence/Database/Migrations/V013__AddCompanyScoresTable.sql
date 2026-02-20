-- Summary table for pre-computed 13-point value scores.
-- Populated by the --compute-all-scores CLI command (truncate + bulk insert).
-- Queried by the GET /api/reports/scores endpoint with pagination/sorting/filtering.
CREATE TABLE company_scores (
    company_id bigint PRIMARY KEY,
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
    estimated_return_cf decimal,
    estimated_return_oe decimal,
    price_per_share decimal,
    price_date date,
    shares_outstanding bigint,
    computed_at timestamptz NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_company_scores_score ON company_scores (overall_score DESC);
CREATE INDEX idx_company_scores_exchange ON company_scores (exchange);
