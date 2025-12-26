-------------------------------------------------------------------------------

alter table taxonomy_presentation
add column role_name varchar(300) not null default '';
comment on column taxonomy_presentation.role_name is 'Presentation role name used to scope statement traversal.';
