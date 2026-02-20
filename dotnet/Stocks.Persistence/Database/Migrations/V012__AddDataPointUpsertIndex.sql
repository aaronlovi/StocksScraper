-- Unique index enabling ON CONFLICT upsert for data point insertion.
-- Used by the inline XBRL shares import to safely re-run without duplicates.
-- Includes unit_id because the same fact can be reported in different units.
CREATE UNIQUE INDEX IF NOT EXISTS idx_data_points_upsert_key
    ON data_points (company_id, fact_name, unit_id, start_date, end_date, submission_id);
