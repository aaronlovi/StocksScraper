# Requirements: Prototype Financial Statement Viewer

| ID | Requirement | Description | Status | Acceptance Criteria | Notes |
|----|-------------|-------------|--------|---------------------|-------|
| 1  | List available statements | List all available balance sheets, income statements, cash flow statements, or any top-level/abstract taxonomy concept for a specific company. | Complete | Output must list all available top-level/abstract concepts for the specified company and match sample format. | Use command-line args to specify company (by CIK or name) and statement type or concept. |
| 2  | Display statement hierarchy | Display a specific statement or any abstract taxonomy concept for a company, starting from the selected concept. | In Progress | Output must show the full hierarchy for the selected concept, matching the requested format and sample output. | Recursively traverse taxonomy presentation tree, output as CSV, HTML, or JSON. |
| 3  | Output format | Output should be in CSV, HTML, or JSON format, suitable for piping to file, further processing, or human viewing. | Not Started | Output must match the selected format (CSV, HTML, or JSON) and include all required columns/fields. | Columns: Concept Name, Label, Value, Depth, Parent Concept, etc. |
| 4  | Recursion limit | Implement a reasonable recursion limit to avoid runaway output. | Not Started | Output must not include concepts deeper than the specified max depth. | Configurable, default to 10 levels. |
| 5  | Integration | Integrate as a new command in Stocks.EDGARScraper (console app). | Complete | New command-line switch (--print-statement) must be available and functional. | Add new command-line switch, e.g. --print-statement. |
| 6  | Data access | Use `IDbmService` (see `Stocks.Persistence.Database.IDbmService`) for all data access. | In Progress | All data must be loaded via `IDbmService` methods. | Use `GetTaxonomyConceptsByTaxonomyType`, `GetAllCompaniesByDataSource`, `GetSubmissions`, etc. |
| 7  | Robustness | Handle errors gracefully; log and continue where possible. | In Progress | Errors must be logged to stderr, and the tool must not crash on missing data. | Prototype quality, but avoid crashes. |
| 8  | Filter by date | Allow user to specify a date to select the set of data points (e.g., display balance sheet as of 2019-03-01). | Not Started | Output must reflect data as of the specified date. | Use command-line arg to filter by report date. |
| 9  | Output format switch | Allow user to select output format via CLI (csv, html, json). | Not Started | Output must match the format specified by the --format argument. | Use --format argument. |
| 10 | CLI argument validation | Validate all required CLI arguments; if missing or invalid, print usage instructions to stderr and exit with a non-zero code. | Complete | Usage instructions must be printed and exit code non-zero if required arguments are missing or invalid. | Usage should clearly describe all required and optional arguments. |

---

## High-Level Design

The prototype financial statement viewer will be a command-line tool integrated into the `Stocks.EDGARScraper` console application. It will allow users to display a company's financial statement (or any abstract taxonomy concept) as of a specific date, starting from any top-level/abstract taxonomy concept, and output the result as CSV, HTML, or JSON.

**Key Features:**
- List all available statements or abstract concepts for a company.
- Display a statement or concept hierarchy for a company and date, traversing the taxonomy presentation tree.
- Output results in CSV, HTML, or JSON format, suitable for further processing or analysis.
- Robust error handling and logging.
- **Argument validation:** If required arguments are missing or invalid, print usage instructions to stderr and exit with a non-zero code.

**User Flow:**
1. User invokes the tool with command-line arguments specifying CIK, concept, date, output format, and optional recursion depth.
2. The tool resolves the company, loads taxonomy concepts and presentation hierarchy, and finds the relevant submission for the specified date.
3. The tool recursively traverses the presentation tree from the selected concept, querying and outputting data points for each concept in the hierarchy.
4. Output is written to stdout in the selected format.

---

## Technical Design

### Main Components

- **Command-Line Interface (CLI):**
  - New switch: `--print-statement`
  - Arguments: `--cik`, `--concept`, `--date`, `--max-depth`, `--format`
  - Entry point: `Program.cs` in `Stocks.EDGARScraper`
  - Example:  dotnet run --print-statement --cik 1234 --concept "Assets" --date "2019-03-01" --max-depth 10 --format html
  - **Argument validation:** If any required argument is missing or invalid, print usage instructions to stderr and exit with a non-zero code.
  - **StatementPrinter Class:**
    - Encapsulates all logic for rendering a financial statement or taxonomy concept hierarchy for a company.
    - Responsibilities:
      - Accepts parameters for CIK, concept, date, recursion depth, and output format (CSV, HTML, JSON).
      - Loads taxonomy concepts and presentation hierarchy from the database using `IDbmService` (see `Stocks.Persistence.Database.IDbmService`).
      - Finds the relevant company and submission for the specified CIK and date.
      - Recursively traverses the taxonomy presentation tree starting from the selected concept, respecting the max depth.
      - Queries data points for each concept in the hierarchy.
      - Formats and outputs the results in the selected format (CSV, HTML, or JSON) to stdout.
      - Handles missing data, errors, and logging as specified in the requirements.
    - Main methods (suggested):
      - `PrintStatement()`: Main entry point for rendering the statement.
      - `TraverseConceptTree()`: Recursively walks the taxonomy tree.
      - `FormatOutput()`: Handles output formatting for each supported format.
      - `ValidateParameters()`: Ensures all required parameters are present and valid.
    
    - **Output Format Extensibility:**
      - The `StatementPrinter` class is designed for extensibility. To add a new output format, extend the `FormatOutput()` method (or use a strategy pattern or similar extension point) to support the new format. Document the new format and update CLI argument validation accordingly.
      - This allows future developers to add formats such as XML, Markdown, or others with minimal changes to the core logic.

- **Data Access:**
  - Uses `IDbmService` (see `Stocks.Persistence.Database.IDbmService`) for all database operations.
    - `GetAllCompaniesByDataSource` to resolve CIK to CompanyId.
    - `GetTaxonomyConceptsByTaxonomyType` to load all concepts.
    - (To be implemented) Query to load presentation hierarchy (children/parents).
    - `GetSubmissions` to find the correct submission for the specified date.
    - (To be implemented) Query to get data points for a given CompanyId, SubmissionId, and TaxonomyConceptId.

- **Taxonomy Navigation:**
  - Use `ConceptDetailsDTO` (see `Stocks.Persistence.Database.DTO.Taxonomies.ConceptDetailsDTO`) to identify abstract concepts and concept metadata.
  - Use `PresentationDetailsDTO` (see `Stocks.Persistence.Database.DTO.Taxonomies.PresentationDetailsDTO`) to navigate parent/child relationships in the taxonomy presentation tree.
  - Recursively traverse the tree, respecting the max depth.

- **Output Formats:**
  - **CSV:** Columns: `ConceptName,Label,Value,Depth,ParentConceptName`
  - **HTML:** Nested `<ul>`/`<li>` or table structure for hierarchy.
  - **JSON:** Tree structure with children as arrays.
  - **Extensibility:** New formats can be added by extending the `StatementPrinter` class as described above.

### Algorithm Design: Recursive Taxonomy Traversal

The core of statement hierarchy output is a recursive traversal of the taxonomy presentation tree, starting from a user-specified root concept. The traversal must:
- Output each concept's details (name, label, value, depth, parent) in the requested format (CSV, HTML, JSON)
- Query the value for each concept for the selected company and submission/date
- Respect a configurable maximum recursion depth (default: 10)
- Handle missing data points and missing children gracefully, logging warnings as needed

**Inputs:**
- Root concept (by name or ID)
- Parent concept (null for root)
- Current depth (starts at 0)
- Max depth (from CLI or default)
- In-memory parent/child map (built from PresentationDetailsDTO)
- Concept metadata (from ConceptDetailsDTO)
- Data points (from DataPoint, filtered by company, submission, concept)

**Outputs:**
- For each concept, output a row/object/element with: ConceptName, Label, Value, Depth, ParentConceptName (CSV/HTML/JSON as appropriate)
- For JSON/HTML, include children as nested arrays/elements

**Algorithm:**
1. If current depth > max depth, return (stop recursion)
2. Output the current concept's details (including value, if available)
3. For each child of the current concept (from parent/child map):
    - Recursively call the traversal function with child as the new current concept, incrementing depth
4. If a concept has no children, output as a leaf node
5. If a data point is missing for a concept, output an empty value and log a warning

**Data Structures:**
- Dictionary<long, List<PresentationDetailsDTO>>: maps ParentConceptId to child PresentationDetailsDTOs
- Dictionary<long, ConceptDetailsDTO>: maps ConceptId to concept metadata
- Dictionary<long, DataPoint>: maps ConceptId to data point for the selected company/submission

**Edge Cases:**
- Root concept not found: log error, exit non-zero
- No children for a concept: treat as leaf
- Cycles in the tree: detect and prevent infinite recursion (track visited ConceptIds)
- Max depth reached: stop recursion, do not output deeper nodes

This design supports extensibility for new output formats and additional metadata fields as needed.

### In-Memory Structure for Taxonomy Parent/Child Relationships

To efficiently traverse the taxonomy presentation tree, build an in-memory map from parent concept IDs to their child PresentationDetailsDTOs. This enables fast lookup of children for any concept during recursion.

**Recommended C# structure:**
// Maps ParentConceptId to a list of child PresentationDetailsDTOs
Dictionary<long, List<PresentationDetailsDTO>> parentToChildren = new();
- Populate this dictionary by iterating over all PresentationDetailsDTOs for the taxonomy.
- For each PresentationDetailsDTO, add it to the list for its ParentConceptId.
- During traversal, use this map to find all children of the current concept.
- This structure supports efficient, cycle-safe, and depth-limited recursion.

This structure should be created and populated as part of the data loading step in StatementPrinter before starting the recursive traversal.

### Data Flow

1. **Resolve Company:**  
   Use `IDbmService.GetAllCompaniesByDataSource` to find the company by CIK.
2. **Load Taxonomy Concepts:**  
   Use `IDbmService.GetTaxonomyConceptsByTaxonomyType` to load all concepts for US-GAAP 2025.
3. **List Abstract Concepts:**  
   Filter concepts where `IsAbstract == true` for user selection.
4. **Load Presentation Hierarchy:**  
   (If not already available) Implement a query to get all `PresentationDetailsDTO` (see `Stocks.Persistence.Database.DTO.Taxonomies.PresentationDetailsDTO`) for the taxonomy, and build a parent/child map in memory.
5. **Find Submission by Date:**  
   Use `IDbmService.GetSubmissions` to find the correct submission for the specified date.
6. **Traverse and Output:**  
   Starting from the selected concept, recursively traverse children, querying for data points at each node, and outputting results in the selected format.

---

## DTOs and Data Structures

### ConceptDetailsDTO
- **File:** `Stocks.Persistence/Database/DTO/Taxonomies/ConceptDetailsDTO.cs`
- **Namespace:** `Stocks.Persistence.Database.DTO.Taxonomies`
- **Definition:**
  public record ConceptDetailsDTO(
    long ConceptId, int TaxonomyTypeId, int PeriodTypeId, int BalanceTypeId, bool IsAbstract, string Name, string Label, string Documentation);
- **Properties:**
  - long ConceptId
  - int TaxonomyTypeId
  - int PeriodTypeId
  - int BalanceTypeId
  - bool IsAbstract
  - string Name
  - string Label
  - string Documentation

### PresentationDetailsDTO
- **File:** `Stocks.Persistence/Database/DTO/Taxonomies/PresentationDetailsDTO.cs`
- **Namespace:** `Stocks.Persistence.Database.DTO.Taxonomies`
- **Definition:**
  public record PresentationDetailsDTO(
    long PresentationId,
    long ConceptId,
    int Depth,
    int OrderInDepth,
    long ParentConceptId,
    long ParentPresentationId);
- **Properties:**
  - `long PresentationId`
  - `long ConceptId`
  - `int Depth`
  - `int OrderInDepth`
  - `long ParentConceptId`
  - `long ParentPresentationId`

### DataPoint
- **File:** `Stocks.DataModels/DataPoint.cs`
- **Namespace:** `Stocks.DataModels`
- **Definition:**
  public record DataPoint(
    ulong DataPointId,
    ulong CompanyId,
    string FactName,
    string FilingReference,
    DatePair DatePair,
    decimal Value,
    DataPointUnit Units,
    DateOnly FiledDate,
    ulong SubmissionId,
    long TaxonomyConceptId)
- **Properties:**
  - `ulong DataPointId`
  - `ulong CompanyId`
  - `string FactName`
  - `string FilingReference`
  - `DatePair DatePair`
  - `decimal Value`
  - `DataPointUnit Units`
  - `DateOnly FiledDate`
  - `ulong SubmissionId`
  - `long TaxonomyConceptId`

### IDbmService
- **File:** `Stocks.Persistence/Database/IDbmService.cs`
- **Namespace:** `Stocks.Persistence.Database`
- **Definition:**public interface IDbmService {
    Task<Result<IReadOnlyCollection<Company>>> GetAllCompaniesByDataSource(string dataSource, CancellationToken ct);
    Task<Result<IReadOnlyCollection<ConceptDetailsDTO>>> GetTaxonomyConceptsByTaxonomyType(int taxonomyTypeId, CancellationToken ct);
    Task<Result<IReadOnlyCollection<Submission>>> GetSubmissions(CancellationToken ct);
    // ...other methods
  }- **Key Methods Used:**
  - `GetAllCompaniesByDataSource`
  - `GetTaxonomyConceptsByTaxonomyType`
  - `GetSubmissions`

___

## Implementation Context and Codebase Helpers

### StatementPrinter Class

- The `StatementPrinter` class is already scaffolded and located at `Stocks.EDGARScraper/Services/Statements/StatementPrinter.cs`.
- **Fully qualified name:** `Stocks.EDGARScraper.Services.Statements.StatementPrinter`
- This class is responsible for rendering financial statements and should be referenced for all related implementation work.
- DTOs and data access interfaces are present in the `Stocks.Persistence` and `Stocks.DataModels` projects as described above.
- CLI argument parsing and entry point logic are in `Stocks.EDGARScraper/Program.cs`.

---

## Output Format Examples

### CSV
ConceptName,Label,Value,Depth,ParentConceptName
Assets,Assets,350000,0,
Current Assets,Current Assets,150000,1,Assets
Cash and Cash Equivalents,Cash,100000,2,Current Assets
Accounts Receivable,Receivables,50000,2,Current Assets
Noncurrent Assets,Noncurrent Assets,200000,1,Assets
Property, Plant, Equipment,PPE,200000,2,Noncurrent Assets
### HTML
<ul>
  <li>Assets: 350000
    <ul>
      <li>Current Assets: 150000
        <ul>
          <li>Cash and Cash Equivalents: 100000</li>
          <li>Accounts Receivable: 50000</li>
        </ul>
      </li>
      <li>Noncurrent Assets: 200000
        <ul>
          <li>Property, Plant, Equipment: 200000</li>
        </ul>
      </li>
    </ul>
  </li>
</ul>
### JSON
{
  "ConceptName": "Assets",
  "Label": "Assets",
  "Value": 350000,
  "Children": [
    {
      "ConceptName": "Current Assets",
      "Label": "Current Assets",
      "Value": 150000,
      "Children": [
        { "ConceptName": "Cash and Cash Equivalents", "Label": "Cash", "Value": 100000 },
        { "ConceptName": "Accounts Receivable", "Label": "Receivables", "Value": 50000 }
      ]
    },
    {
      "ConceptName": "Noncurrent Assets",
      "Label": "Noncurrent Assets",
      "Value": 200000,
      "Children": [
        { "ConceptName": "Property, Plant, Equipment", "Label": "PPE", "Value": 200000 }
      ]
    }
  ]
}
---

## Error Handling

- If a company, concept, or submission is not found, log a warning and exit gracefully with a non-zero exit code.
- If a data point is missing for a concept, output an empty value and log a warning.
- All errors should be logged to stderr.
- **If required CLI arguments are missing or invalid, print usage instructions to stderr and exit with a non-zero code.**

### Sample Error/Warning Messages

- Company not found:
  - `ERROR: Company with CIK '00001234' not found.`
- Concept not found:
  - `ERROR: Concept 'Assets' not found in taxonomy.`
- Submission not found for date:
  - `ERROR: No submission found for CIK '00001234' on or before 2019-03-01.`
- Data point missing for concept:
  - `WARNING: No data point found for concept 'Cash and Cash Equivalents' (ConceptId: 12345) in submission 67890.`
- Invalid or missing CLI argument:
  - `ERROR: Missing required argument --cik.`
  - `ERROR: Invalid value for --date: 'not-a-date'.`
  - `USAGE: dotnet run --print-statement --cik <CIK> --concept <ConceptName> --date <YYYY-MM-DD> --format <csv|html|json> [--max-depth <N>]`

---

## Testing

Below are concrete Gherkin scenarios for each major requirement. These scenarios should be automated using SpecFlow or a similar tool, or used as the basis for xUnit tests.

### Scenario: List available statements for a company
Given the EDGARScraper CLI is available
And the database contains a company with CIK "00001234" and US-GAAP taxonomy concepts
When I run "dotnet run --print-statement --cik 00001234 --list-statements"
Then the output should include a list of available top-level statements or abstract concepts
And the output should contain "Assets" and "Liabilities"

### Scenario: Display a statement in CSV format
Given the EDGARScraper CLI is available
And the database contains a company with CIK "00001234" and a submission dated "2019-03-01"
And the taxonomy contains the concept "Assets"
When I run "dotnet run --print-statement --cik 00001234 --concept Assets --date 2019-03-01 --format csv"
Then the output should be valid CSV
And the output should include a row with "Assets" and its value

### Scenario: Display a statement in HTML format
Given the EDGARScraper CLI is available
And the database contains a company with CIK "00001234" and a submission dated "2019-03-01"
And the taxonomy contains the concept "Assets"
When I run "dotnet run --print-statement --cik 00001234 --concept Assets --date 2019-03-01 --format html"
Then the output should be valid HTML
And the output should include an element with "Assets" and its value

### Scenario: Display a statement in JSON format
Given the EDGARScraper CLI is available
And the database contains a company with CIK "00001234" and a submission dated "2019-03-01"
And the taxonomy contains the concept "Assets"
When I run "dotnet run --print-statement --cik 00001234 --concept Assets --date 2019-03-01 --format json"
Then the output should be valid JSON
And the output should include a property "ConceptName" with value "Assets"

### Scenario: Handle missing company
Given the EDGARScraper CLI is available
And the database does not contain a company with CIK "99999999"
When I run "dotnet run --print-statement --cik 99999999 --concept Assets --date 2019-03-01 --format csv"
Then the error output should contain "ERROR: Company with CIK '99999999' not found."
And the exit code should be non-zero

### Scenario: Handle missing concept
Given the EDGARScraper CLI is available
And the database contains a company with CIK "00001234"
But the taxonomy does not contain the concept "NonexistentConcept"
When I run "dotnet run --print-statement --cik 00001234 --concept NonexistentConcept --date 2019-03-01 --format csv"
Then the error output should contain "ERROR: Concept 'NonexistentConcept' not found in taxonomy."
And the exit code should be non-zero

### Scenario: Handle missing submission for date
Given the EDGARScraper CLI is available
And the database contains a company with CIK "00001234"
But there is no submission for the date "1990-01-01"
When I run "dotnet run --print-statement --cik 00001234 --concept Assets --date 1990-01-01 --format csv"
Then the error output should contain "ERROR: No submission found for CIK '00001234' on or before 1990-01-01."
And the exit code should be non-zero

### Scenario: Recursion limit enforcement
Given the EDGARScraper CLI is available
And the taxonomy contains a deeply nested concept hierarchy under "Assets"
When I run "dotnet run --print-statement --cik 00001234 --concept Assets --date 2019-03-01 --format csv --max-depth 2"
Then the output should not include any concepts deeper than depth 2

### Scenario: Date filtering
Given the EDGARScraper CLI is available
And the database contains a company with CIK "00001234"
And there are submissions for "2018-12-31" and "2019-03-01"
When I run "dotnet run --print-statement --cik 00001234 --concept Assets --date 2019-03-01 --format csv"
Then the output should reflect data from the submission dated "2019-03-01"

---

## Kanban Task List

### Backlog
- Refactor StatementPrinter to query for a single company by CIK in the database, rather than loading all companies and iterating in memory, for better performance with large datasets.
- Add Microsoft.Extensions.Logging (Serilog abstraction) logging to StatementPrinter for typical info, warning, and error events.
- Add Gherkin/xUnit tests for all major scenarios.
- Document sample outputs and update this requirements file as needed.
- Document and maintain output format extensibility: When adding a new output format, extend the StatementPrinter class and update documentation and CLI validation accordingly.
- Write Gherkin/xUnit tests for CLI argument validation (requirement 10), including missing/invalid arguments and usage output.
- Write xUnit tests for CLI integration (requirement 5), ensuring the --print-statement switch is recognized and routed correctly.
- Write xUnit tests for data access methods used in statement listing (requirement 1/6), ensuring correct data is returned or errors are handled.
- Hierarchy Traversal & Output (Requirement 2)
  - Implement recursive traversal of the taxonomy tree, respecting the max depth.
  - Implement CSV output for the hierarchy, matching the required columns and format.
  - Implement HTML output for the hierarchy, using nested lists or tables.
  - Implement JSON output for the hierarchy, using a tree structure.
  - Implement recursion limit enforcement (Requirement 4).
  - Implement robust error handling for missing concept, missing children, and missing data points (Requirement 7).
  - Implement date filtering for data points (Requirement 8).
  - Add xUnit tests for hierarchy traversal, including edge cases (missing concept, recursion limit, etc.).
  - Add Gherkin scenarios for hierarchy output in all formats.

### Ready
- Implement GetTaxonomyPresentationsByTaxonomyType in DbmService: Implement the actual database query to retrieve all PresentationDetailsDTO for a given taxonomy type.
- Implement GetDataPointsForSubmission in DbmService: Implement the actual database query to retrieve all DataPoint records for a given company and submission.
- Implement main flow in StatementPrinter.PrintStatement() (load data, handle list/hierarchy, error handling).
- Hierarchy Traversal & Output (Requirement 2)
  - Implement recursive traversal of the taxonomy tree, respecting the max depth.

### In Progress
- Implement CLI argument parsing for all required switches.
- Implement StatementPrinter class with support for CSV, HTML, and JSON output (hierarchy mode).

### Done
- Inspect DTOs and data access methods; document their properties here.
- Implement StatementPrinter class with support for CSV output for --list-statements.
- Create GetTaxonomyPresentationsByTaxonomyTypeStmt for querying all PresentationDetailsDTO for a taxonomy type.
- Create GetDataPointsForSubmissionStmt for querying all DataPoint records for a company and submission.
- Write xUnit test for listing available statements (requirement 1)
- Write Gherkin/xUnit tests for listing available statements (requirement 1), including normal and edge cases (e.g., company not found, no abstract concepts).
- Add robust error handling and logging to StatementPrinter (listing mode).
- Hierarchy Traversal & Output (Requirement 2)
  - Design and document the recursive traversal algorithm for the taxonomy presentation tree.
  - Define the in-memory structure for parent/child relationships using PresentationDetailsDTO.
  - Implement loading and mapping of PresentationDetailsDTOs for the selected taxonomy.
  - Implement logic to find the root concept (by name or ID) and validate its existence.
