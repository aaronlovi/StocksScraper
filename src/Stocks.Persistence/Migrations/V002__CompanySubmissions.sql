create table filing_types (
    filing_type_id int primary key,
    filing_type_name varchar(50) not null
);
comment on table filing_types is 'Defines enumerated values for filing types, such as 10-K or 10-Q.';
comment on column filing_types.filing_type_id is 'Unique ID for the filing type.';
comment on column filing_types.filing_type_name is 'Name of the filing type (e.g., 10-K, 10-Q).';

insert into filing_types values
(1, '10-K'),
(2, '10-Q'),
(3, '8-K');

-------------------------------------------------------------------------------

create table filing_categories (
    filing_category_id int primary key,
    filing_category_name varchar(50) not null
);
comment on table filing_categories is 'Defines enumerated values for filing categories, such as Annual or Quarterly.';
comment on column filing_categories.filing_category_id is 'Unique ID for the filing category.';
comment on column filing_categories.filing_category_name is 'Name of the filing category (e.g., Annual, Quarterly).';

insert into filing_categories values
(1, 'Annual'),
(2, 'Quarterly'),
(3, 'Other');

-------------------------------------------------------------------------------

create table if not exists submissions (
    submission_id bigint not null,
    company_id bigint not null,
    filing_reference varchar(100) not null,
    filing_type int not null,
    filing_category int not null,
    report_date date not null,
    acceptance_datetime timestamptz,
    primary key (submission_id),
    unique (company_id, filing_reference)
);
comment on table submissions is 'Stores submission metadata for filings from various companies and jurisdictions.';
comment on column submissions.submission_id is 'Primary key identifying a unique submission.';
comment on column submissions.company_id is 'References the company that submitted the filing.';
comment on column submissions.filing_reference is 'Unique identifier for the filing (e.g., SEC accession number).';
comment on column submissions.filing_type is 'Enumerated filing type, referencing public.filing_types.';
comment on column submissions.filing_category is 'Enumerated filing category, referencing public.filing_categories.';
comment on column submissions.report_date is 'End date of the period covered by the filing.';
comment on column submissions.acceptance_datetime is 'Date and time the submission was accepted by the regulatory body.';

-------------------------------------------------------------------------------

alter table data_points
add column submission_id bigint not null;
comment on column data_points.submission_id is 'Links the data point to the associated submission, such as a 10-K or 10-Q filing.';

-------------------------------------------------------------------------------
