-- Stores the 15 value-check results with each score row, so readers (the Portfolio
-- Advisor) use the real results instead of re-deriving them from the metric columns,
-- which lack equity and the ROE sign flags. Null for rows written before this migration;
-- --compute-all-scores and --compute-score-snapshots fill every row.
ALTER TABLE company_scores ADD COLUMN check_results varchar(15);
ALTER TABLE graham_score_snapshots ADD COLUMN check_results varchar(15);

COMMENT ON COLUMN company_scores.check_results IS 'The 15 value-check results in check-number order: P pass, F fail, N not available. Null for rows written before V021.';
COMMENT ON COLUMN graham_score_snapshots.check_results IS 'The 15 value-check results in check-number order: P pass, F fail, N not available. Null for rows written before V021.';
