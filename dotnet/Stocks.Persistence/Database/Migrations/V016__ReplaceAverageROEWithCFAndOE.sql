ALTER TABLE company_scores DROP COLUMN average_roe;
ALTER TABLE company_scores ADD COLUMN average_roe_cf decimal;
ALTER TABLE company_scores ADD COLUMN average_roe_oe decimal;
