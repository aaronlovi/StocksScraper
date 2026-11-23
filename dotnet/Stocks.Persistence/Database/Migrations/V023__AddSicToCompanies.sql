-- SEC Standard Industrial Classification per company, from the EDGAR submissions bulk
-- file (CIK##########.json "sic" and "sicDescription"), written by --import-company-sic.
-- Score reports join it to flag financials (SIC 6000-6499); scoring itself ignores it.
ALTER TABLE companies ADD COLUMN sic_code int;
ALTER TABLE companies ADD COLUMN sic_description varchar(200);

CREATE INDEX idx_companies_sic_code ON companies (sic_code);

COMMENT ON COLUMN companies.sic_code IS 'SEC Standard Industrial Classification code from the EDGAR submissions file (sic); null when the SEC lists none or before --import-company-sic.';
COMMENT ON COLUMN companies.sic_description IS 'SEC description of sic_code (sicDescription); null with sic_code.';
