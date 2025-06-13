# Requirements: Prototype Financial Statement Viewer

| ID | Requirement | Description | Status | Notes |
|----|-------------|-------------|--------|-------|
| 1  | List available statements | List all available balance sheets, income statements, cash flow statements, or any top-level/abstract taxonomy concept for a specific company. | Not Started | Use command-line args to specify company (by CIK or name) and statement type or concept. |
| 2  | Display statement hierarchy | Display a specific statement or any abstract taxonomy concept for a company, starting from the selected concept. | Not Started | Recursively traverse taxonomy presentation tree, output as CSV. |
| 3  | Output format | Output should be in CSV format, suitable for piping to file or further processing. | Not Started | Columns: Concept Name, Label, Value, Depth, Parent Concept, etc. |
| 4  | Recursion limit | Implement a reasonable recursion limit to avoid runaway output. | Not Started | Configurable, default to 10 levels. |
| 5  | Integration | Integrate as a new command in Stocks.EDGARScraper (console app). | Not Started | Add new command-line switch, e.g. --print-statement. |
| 6  | Data access | Use IDbmService for all data access. | Not Started | Use GetTaxonomyConceptsByTaxonomyType, GetAllCompaniesByDataSource, GetSubmissions, etc. |
| 7  | Robustness | Handle errors gracefully; log and continue where possible. | Not Started | Prototype quality, but avoid crashes. |
| 8  | Filter by date | Allow user to specify a date to select the set of data points (e.g., display balance sheet as of 2019-03-01). | Not Started | Use command-line arg to filter by report date. |

---

## General Notes & Implementation Context

- **Where to Implement:**
  - Main entry point: `Program.cs` in `Stocks.EDGARScraper`.
  - Add a new command-line switch, e.g. `--print-statement`.
  - Place statement traversal and CSV output logic in a new class, e.g. `StatementPrinter`.

- **How to List Statements/Concepts:**
  - Use `IDbmService.GetAllCompaniesByDataSource` to find the company.
  - Use `IDbmService.GetSubmissions` to list available filings for the company.
  - Use taxonomy concepts and presentations to determine available statement types or any top-level/abstract concept (`IsAbstract == true`).
  - Allow user to list all available abstract concepts for selection.

- **How to Display a Statement or Concept:**
  - Use `IDbmService.GetTaxonomyConceptsByTaxonomyType` to load concepts.
  - Use a (to be implemented) method to load the presentation tree (hierarchy) for the selected concept.
  - Start from the selected abstract concept (specified by command-line switch, e.g., --concept "Assets").
  - Recursively traverse child concepts (using presentation hierarchy), for each:
    - Query for data points for the company and submission.
    - Output each concept and value as a row in CSV.
    - Include columns: Concept Name, Label, Value, Depth, Parent Concept, etc.
  - Stop recursion at the configured limit.

- **CSV Output:**
  - Output to console (stdout) in CSV format.
  - Use indentation or a "Depth" column to indicate hierarchy.
  - Example columns: `ConceptName,Label,Value,Depth,ParentConceptName`

- **DTOs and Services:**
  - Use `ConceptDetailsDTO` and `PresentationDetailsDTO` for taxonomy navigation.
  - Use `DataPoint` for actual values.
  - All DTOs are in `Stocks.Persistence.Database.DTO.Taxonomies` or `Stocks.DataModels`.

- **Database Statements:**
  - Use or extend existing statements in `Stocks.Persistence\Database\Statements\` as needed.
  - If a new query is needed (e.g., to get children of a concept, or data points for a concept/company/submission/date), add it in `Stocks.Persistence`.

- **Recursion Limit:**
  - Default to 10 levels; make configurable via command-line arg.

- **Date Filtering:**
  - Allow user to specify a date (e.g., --date "2019-03-01") to select the set of data points for that report date.
  - Filter submissions and data points by the specified date.

- **Error Handling:**
  - Log errors and continue where possible.
  - If a concept or data point is missing, log a warning and skip.

- **Assumptions:**
  - Only US-GAAP 2025 taxonomy is supported for this prototype.
  - Company is specified by CIK or name (resolve to CompanyId).
  - Statement/concept is specified by name (e.g., "BalanceSheet", "Assets", etc.).
  - Output is to console; user may redirect to file.

---

## Implementation Hints

- **Finding Abstract Concepts:**  
  Use `ConceptDetailsDTO.IsAbstract == true` to find top-level statement concepts.
- **Presentation Hierarchy:**  
  Use `PresentationDetailsDTO` to navigate parent/child relationships (ParentConceptId, ConceptId).
- **Data Points:**  
  Query for `DataPoint` by CompanyId, SubmissionId, TaxonomyConceptId, and filter by date if specified.
- **Command-line Example:**  dotnet run --print-statement --cik 1234 --concept "Assets" --date "2019-03-01" --max-depth 10- **Relevant Files:**  
  - `Program.cs` (main entry, add new switch)
  - `IDbmService.cs` (data access)
  - `ConceptDetailsDTO.cs`, `PresentationDetailsDTO.cs` (taxonomy)
  - `DataPoint.cs` (values)
  - `GetTaxonomyConceptsByTaxonomyTypeStmt.cs`, `GetAllSubmissionsStmt.cs` (DB queries)
  - Add new files/classes as needed for statement printing logic.

---

Update this table as requirements are implemented. Each requirement should be implemented in a way that does not introduce regressions to other ETL or data loading operations.
