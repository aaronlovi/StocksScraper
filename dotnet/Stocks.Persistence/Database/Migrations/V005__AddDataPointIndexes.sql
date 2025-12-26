-------------------------------------------------------------------------------

create index if not exists idx_data_points_company_submission
    on data_points (company_id, submission_id);

create index if not exists idx_data_points_company_submission_concept
    on data_points (company_id, submission_id, taxonomy_concept_id);
