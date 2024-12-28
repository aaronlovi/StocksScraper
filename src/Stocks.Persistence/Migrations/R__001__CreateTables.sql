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
    cik bigint not null,
    name varchar(200) not null,
    primary key (name_id)
);
comment on table company_names is 'Table containing company names associated with CIKs.';
comment on column company_names.name_id is 'Unique identifier for the company name';
comment on column company_names.cik is 'Central Index Key (CIK) for American stocks, sourced from EDGAR. Zero otherwise.';
comment on column company_names.name is 'Name of the company, limited to 200 characters';
create index idx_company_names_cik on company_names (cik);
