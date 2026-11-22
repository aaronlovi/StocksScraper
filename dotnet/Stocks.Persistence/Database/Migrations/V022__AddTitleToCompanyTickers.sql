-- The SEC's current company name per listed CIK (company_tickers.json "title"), written by
-- --import-sec-ticker-mappings. company_names lists every historical name alphabetically,
-- so scoring prefers this title for display. Null until the mappings are re-imported.
ALTER TABLE company_tickers ADD COLUMN title varchar(200);

COMMENT ON COLUMN company_tickers.title IS 'Current company name from the SEC ticker mappings file (title); null until re-imported.';
