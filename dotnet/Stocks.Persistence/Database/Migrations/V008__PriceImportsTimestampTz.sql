-------------------------------------------------------------------------------

alter table price_imports
alter column last_imported_utc
type timestamp with time zone
using last_imported_utc at time zone 'UTC';
