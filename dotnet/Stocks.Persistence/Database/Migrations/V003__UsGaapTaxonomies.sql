-------------------------------------------------------------------------------

create table taxonomy_period_types (
    taxonomy_period_type_id int primary key,
    taxonomy_period_type_name varchar(20) not null
);
comment on table taxonomy_period_types is 'Enumerates the period types for US-GAAP data points, such as duration or instant.';
comment on column taxonomy_period_types.taxonomy_period_type_id is 'Unique ID for the period type.';
comment on column taxonomy_period_types.taxonomy_period_type_name is 'Name of the period type (e.g., duration, instant).';

insert into taxonomy_period_types values
(1, 'duration'),
(2, 'instant');

-------------------------------------------------------------------------------

create table taxonomy_balance_types (
    taxonomy_balance_type_id int primary key,
    taxonomy_balance_type_name varchar(20) not null
);
comment on table taxonomy_balance_types is 'Enumerates the balance types for US-GAAP data points, such as credit, debit, or empty.';
comment on column taxonomy_balance_types.taxonomy_balance_type_id is 'Unique ID for the balance type.';
comment on column taxonomy_balance_types.taxonomy_balance_type_name is 'Name of the balance type (e.g., credit, debit, or empty for none).';

insert into taxonomy_balance_types values
(1, 'credit'),
(2, 'debit'),
(3, '');

-------------------------------------------------------------------------------

create table taxonomy_types (
    taxonomy_type_id int primary key,
    taxonomy_type_name varchar(50) not null,
    taxonomy_type_version int not null
);
comment on table taxonomy_types is 'Enumerates the types of taxonomies supported, such as US-GAAP.';
comment on column taxonomy_types.taxonomy_type_id is 'Unique ID for the taxonomy type.';
comment on column taxonomy_types.taxonomy_type_name is 'Name of the taxonomy type (e.g., us-gaap).';
comment on column taxonomy_types.taxonomy_type_version is 'Version identifier for the taxonomy type. For us-gaap, this is typically a year (e.g., 2025, 2024). For other taxonomies, it may be a different versioning scheme.';

insert into taxonomy_types values
(1, 'us-gaap', 2025);

-------------------------------------------------------------------------------

create table taxonomy_concepts (
    taxonomy_concept_id bigint primary key,
    taxonomy_type_id int not null,
    taxonomy_period_type_id int not null,
    taxonomy_balance_type_id int not null,
    is_abstract boolean not null default false,
    name varchar(250),
    label varchar(300),
    documentation varchar(4000)
);
comment on table taxonomy_concepts is 'Stores taxonomy concepts, including type, period type, balance type, abstract flag, name, label, and documentation.';
comment on column taxonomy_concepts.taxonomy_concept_id is 'Primary key for the taxonomy concept.';
comment on column taxonomy_concepts.taxonomy_type_id is 'Enumerated value for the taxonomy type (e.g., us-gaap).';
comment on column taxonomy_concepts.taxonomy_period_type_id is 'Enumerated value for the period type (duration or instant) of the concept.';
comment on column taxonomy_concepts.taxonomy_balance_type_id is 'Enumerated value for the balance type (credit, debit, or empty) of the concept.';
comment on column taxonomy_concepts.is_abstract is 'Indicates if the concept is abstract (true) or not (false).';
comment on column taxonomy_concepts.name is 'Unique name for the taxonomy concept, suitable for programmatic reference. Max length at least 212 characters.';
comment on column taxonomy_concepts.label is 'Display label for the taxonomy concept, suitable for UI display.';
comment on column taxonomy_concepts.documentation is 'Detailed documentation or description for the taxonomy concept, suitable for tooltips or footnotes.';

-------------------------------------------------------------------------------

create table taxonomy_presentation (
    taxonomy_presentation_id bigint primary key,
    taxonomy_concept_id bigint not null,
    depth int not null,
    order_in_depth int not null,
    parent_concept_id bigint,
    parent_presentation_id bigint
);
comment on table taxonomy_presentation is 'Stores the presentation hierarchy and relationships between taxonomy concepts.';
comment on column taxonomy_presentation.taxonomy_presentation_id is 'Primary key for the taxonomy presentation row.';
comment on column taxonomy_presentation.taxonomy_concept_id is 'References the taxonomy concept represented in this presentation row (no foreign key constraint).';
comment on column taxonomy_presentation.depth is 'Depth of the concept in the presentation hierarchy.';
comment on column taxonomy_presentation.order_in_depth is 'Order of the concept at its depth in the hierarchy.';
comment on column taxonomy_presentation.parent_concept_id is 'Concept ID of the parent in the hierarchy (no foreign key constraint).';
comment on column taxonomy_presentation.parent_presentation_id is 'Presentation table ID of the parent row (no foreign key constraint).';

-------------------------------------------------------------------------------

alter table data_points
add column taxonomy_concept_id bigint not null;
comment on column data_points.taxonomy_concept_id is 'References the taxonomy concept for the data point.';

-------------------------------------------------------------------------------

alter table data_points
add column taxonomy_presentation_id bigint;
comment on column data_points.taxonomy_presentation_id is 'References the taxonomy presentation row for this data point (no foreign key constraint).';

-------------------------------------------------------------------------------
