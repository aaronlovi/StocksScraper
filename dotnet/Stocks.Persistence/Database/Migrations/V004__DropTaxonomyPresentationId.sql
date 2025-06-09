-------------------------------------------------------------------------------
-- Idempotent migration to drop taxonomy_presentation_id from data_points if it exists
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name='data_points' AND column_name='taxonomy_presentation_id'
    ) THEN
        ALTER TABLE data_points DROP COLUMN taxonomy_presentation_id;
    END IF;
END $$;
-------------------------------------------------------------------------------