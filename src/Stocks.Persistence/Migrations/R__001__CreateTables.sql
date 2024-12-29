create or replace function now_utc() returns timestamp as $$
   select now() at time zone 'utc';
$$ language sql;

create table if not exists generator (
    last_reserved bigint not null
);
insert into generator (last_reserved) values (1);

create table if not exists companies (
    company_id bigint not null,
    cik bigint not null,
    data_source varchar(100) not null,
    primary key (company_id)
);
comment on table companies is 'Table containing company information, including CIK for American stocks and data source.';
comment on column companies.company_id is 'Unique identifier for the company';
comment on column companies.cik is 'Central Index Key (CIK) for American stocks, sourced from EDGAR. Zero otherwise.';
comment on column companies.data_source is 'Source of the data, currently ''EDGAR'' only, but will include other sources in the future';
create index idx_companies_cik on companies (cik);

create table if not exists company_names (
    name_id bigint not null,
    company_id bigint not null,
    name varchar(200) not null,
    primary key (name_id)
);
comment on table company_names is 'Table containing company names associated with CIKs.';
comment on column company_names.name_id is 'Unique identifier for the company name';
comment on column company_names.company_id is 'Foreign key to the company table';
comment on column company_names.name is 'Name of the company, limited to 200 characters';
create index idx_company_names_company_id on company_names (company_id);

create table if not exists units (
    unit_id bigint not null,
    unit_name varchar(100) not null,
    primary key (unit_id)
);
comment on table units is 'Table containing units of measurement for stock data.';
comment on column units.unit_id is 'Unique identifier for the unit';
comment on column units.unit_name is 'Name of the unit, limited to 100 characters';

create table if not exists data_points (
    data_point_id bigint not null,
    company_id bigint not null,
    unit_id int not null,
    fact_name varchar(255) not null,
    start_date date not null,
    end_date date not null,
    value decimal not null,
    filed_date date not null,
    primary key (data_point_id)
);
comment on table data_points is 'Table containing stock data points.';
comment on column data_points.data_point_id is 'Unique identifier for the data point';
comment on column data_points.company_id is 'Foreign key to the company table';
comment on column data_points.unit_id is 'Foreign key to the unit table';
comment on column data_points.fact_name is 'Name of the data point, limited to 255 characters';
comment on column data_points.start_date is 'Start date of the data point';
comment on column data_points.end_date is 'End date of the data point';
comment on column data_points.value is 'Value of the data point';
comment on column data_points.filed_date is 'Date the data point was filed';
