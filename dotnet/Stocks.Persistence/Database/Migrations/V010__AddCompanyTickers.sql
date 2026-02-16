create table if not exists company_tickers (
    company_id bigint not null references companies(company_id),
    ticker varchar(20) not null,
    exchange varchar(50),
    primary key (company_id, ticker)
);
comment on table company_tickers is 'Stores CIK-to-ticker mappings for companies.';
comment on column company_tickers.company_id is 'References the company that owns this ticker symbol.';
comment on column company_tickers.ticker is 'Ticker symbol (e.g., AAPL, MSFT).';
comment on column company_tickers.exchange is 'Exchange where the ticker is listed (e.g., NYSE, NASDAQ).';

create index idx_company_tickers_ticker on company_tickers (ticker);

create index idx_submissions_company_id on submissions (company_id);
