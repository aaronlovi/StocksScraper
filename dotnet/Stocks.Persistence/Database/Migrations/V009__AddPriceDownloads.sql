-------------------------------------------------------------------------------

create table if not exists price_downloads (
    cik bigint not null,
    ticker varchar(32) not null,
    exchange varchar(32) null,
    last_downloaded_utc timestamp with time zone not null,
    primary key (cik, ticker, exchange)
);
comment on table price_downloads is 'Tracks last downloaded timestamp per ticker.';
comment on column price_downloads.cik is 'CIK for the issuer.';
comment on column price_downloads.ticker is 'Normalized ticker symbol.';
comment on column price_downloads.exchange is 'Exchange name.';
comment on column price_downloads.last_downloaded_utc is 'Last successful download timestamp in UTC.';
