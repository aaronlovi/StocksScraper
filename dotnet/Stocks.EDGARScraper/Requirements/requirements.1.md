# Requirements: Data Point Taxonomy Concept Assignment and Schema Update

| ID | Requirement | Description | Status | Notes |
|----|-------------|-------------|--------|-------|
| 1  | Assign taxonomy_concept_id | When inserting data points, join fact_name (trimmed, case-insensitive) to taxonomy_concepts.name and set taxonomy_concept_id. | Not Started | Load all taxonomy_concepts into memory at start. |
| 2  | Log unmatched fact_names | Collect all unmatched fact_names (with context) and log them together at the end of ETL. | Not Started | Do not insert unmatched data points. |
| 3  | Migration: Drop taxonomy_presentation_id | Write an idempotent migration to drop taxonomy_presentation_id from data_points. | Not Started | Remove all code/DTO references to this column. |
| 4  | Robustness & Logging | Ensure robust error handling and logging for all changes. | Not Started | Compatible with .NET 8 and current structure. |

- Update this table as requirements are implemented.
- Each requirement should be implemented in a way that does not introduce regressions to other ETL or data loading operations.

---

## General Notes & Implementation Context

- **Where to Implement taxonomy_concept_id Assignment:**
  - Assignment should occur before batching and inserting DataPoint objects in the ETL (see `ProcessDataPoint` and batching logic in `Program.cs`).
  - The `DataPoint` record in `Stocks.DataModels` will need a `taxonomy_concept_id` property. Update all relevant code paths and DTOs.
  - Update the bulk insert statement (`BulkInsertDataPointsStmt`) to include `taxonomy_concept_id`.

- **How to Load taxonomy_concepts:**
  - Use `IDbmService` to load all `taxonomy_concepts` at the start of the ETL process.
  - Store in a dictionary for fast, case-insensitive, trimmed lookup by `name`.

- **How to Join:**
  - For each DataPoint, match `fact_name` (trimmed, case-insensitive) to `taxonomy_concepts.name` (trimmed, case-insensitive).
  - If no match, collect a warning with context: `fact_name`, company, file, and any other useful info.

- **Logging:**
  - Collect all unmatched fact_names and log them together at the end of the ETL process.
  - Do not insert unmatched data points.

- **Migration:**
  - Ensure `taxonomy_presentation_id` is dropped from the schema and not referenced anywhere in code or DTOs.

- **General:**
  - All changes must be robust, with error handling and logging.
  - Ensure compatibility with .NET 8 and the current project structure.
  - Update the requirements table as you implement each requirement.
  - If you add or change any DTOs or database statements, ensure all usages are updated.
  - If you encounter any ambiguity in matching, prefer to log and skip rather than guess.
  - Document any assumptions or edge cases in the code or in the requirements table.
