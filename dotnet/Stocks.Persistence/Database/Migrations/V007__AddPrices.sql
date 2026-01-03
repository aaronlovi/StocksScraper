-------------------------------------------------------------------------------

create table if not exists prices (
    price_id bigint not null,
    cik bigint not null,
    ticker varchar(32) not null,
    exchange varchar(32) null,
    stooq_symbol varchar(32) not null,
    price_date date not null,
    open numeric not null,
    high numeric not null,
    low numeric not null,
    close numeric not null,
    volume bigint not null,
    primary key (price_id)
);
comment on table prices is 'Daily price data imported from external sources.';
comment on column prices.price_id is 'Unique identifier for the price row.';
comment on column prices.cik is 'CIK for the issuer.';
comment on column prices.ticker is 'Normalized ticker symbol.';
comment on column prices.exchange is 'Exchange name.';
comment on column prices.stooq_symbol is 'Source-specific symbol used for download.';
comment on column prices.price_date is 'Price date (daily).';
comment on column prices.open is 'Open price.';
comment on column prices.high is 'High price.';
comment on column prices.low is 'Low price.';
comment on column prices.close is 'Close price.';
comment on column prices.volume is 'Volume.';
create index idx_prices_ticker on prices (ticker);
create index idx_prices_price_date on prices (price_date);
create unique index idx_prices_ticker_date on prices (ticker, price_date);

-------------------------------------------------------------------------------

create table if not exists price_imports (
    cik bigint not null,
    ticker varchar(32) not null,
    exchange varchar(32) null,
    last_imported_utc timestamp not null,
    primary key (cik, ticker, exchange)
);
comment on table price_imports is 'Tracks last imported timestamp per ticker.';
comment on column price_imports.cik is 'CIK for the issuer.';
comment on column price_imports.ticker is 'Normalized ticker symbol.';
comment on column price_imports.exchange is 'Exchange name.';
comment on column price_imports.last_imported_utc is 'Last successful import timestamp in UTC.';
