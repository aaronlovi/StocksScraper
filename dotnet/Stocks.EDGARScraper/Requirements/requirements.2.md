# Requirements: Prototype Financial Statement Viewer

| ID | Requirement | Description | Status | Notes |
|----|-------------|-------------|--------|-------|
| 1  | List available statements | List all available balance sheets, income statements, cash flow statements, or any top-level/abstract taxonomy concept for a specific company. | Not Started | Use command-line args to specify company (by CIK or name) and statement type or concept. |
| 2  | Display statement hierarchy | Display a specific statement or any abstract taxonomy concept for a company, starting from the selected concept. | Not Started | Recursively traverse taxonomy presentation tree, output as CSV, HTML, or JSON. |
| 3  | Output format | Output should be in CSV, HTML, or JSON format, suitable for piping to file, further processing, or human viewing. | Not Started | Columns: Concept Name, Label, Value, Depth, Parent Concept, etc. |
| 4  | Recursion limit | Implement a reasonable recursion limit to avoid runaway output. | Not Started | Configurable, default to 10 levels. |
| 5  | Integration | Integrate as a new command in Stocks.EDGARScraper (console app). | Not Started | Add new command-line switch, e.g. --print-statement. |
| 6  | Data access | Use IDbmService for all data access. | Not Started | Use GetTaxonomyConceptsByTaxonomyType, GetAllCompaniesByDataSource, GetSubmissions, etc. |
| 7  | Robustness | Handle errors gracefully; log and continue where possible. | Not Started | Prototype quality, but avoid crashes. |
| 8  | Filter by date | Allow user to specify a date to select the set of data points (e.g., display balance sheet as of 2019-03-01). | Not Started | Use command-line arg to filter by report date. |
| 9  | Output format switch | Allow user to select output format via CLI (csv, html, json). | Not Started | Use --format argument. |
| 10 | CLI argument validation | Validate all required CLI arguments; if missing or invalid, print usage instructions to stderr and exit with a non-zero code. | Not Started | Usage should clearly describe all required and optional arguments. |

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
      - Loads taxonomy concepts and presentation hierarchy from the database using IDbmService.
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

- **Data Access:**
  - Uses `IDbmService` for all database operations.
    - `GetAllCompaniesByDataSource` to resolve CIK to CompanyId.
    - `GetTaxonomyConceptsByTaxonomyType` to load all concepts.
    - (To be implemented) Query to load presentation hierarchy (children/parents).
    - `GetSubmissions` to find the correct submission for the specified date.
    - (To be implemented) Query to get data points for a given CompanyId, SubmissionId, and TaxonomyConceptId.

- **Taxonomy Navigation:**
  - Use `ConceptDetailsDTO` to identify abstract concepts and concept metadata.
  - Use `PresentationDetailsDTO` to navigate parent/child relationships in the taxonomy presentation tree.
  - Recursively traverse the tree, respecting the max depth.

- **Output Formats:**
  - **CSV:** Columns: `ConceptName,Label,Value,Depth,ParentConceptName`
  - **HTML:** Nested `<ul>`/`<li>` or table structure for hierarchy.
  - **JSON:** Tree structure with children as arrays.

### Data Flow

1. **Resolve Company:**  
   Use `IDbmService.GetAllCompaniesByDataSource` to find the company by CIK.
2. **Load Taxonomy Concepts:**  
   Use `IDbmService.GetTaxonomyConceptsByTaxonomyType` to load all concepts for US-GAAP 2025.
3. **List Abstract Concepts:**  
   Filter concepts where `IsAbstract == true` for user selection.
4. **Load Presentation Hierarchy:**  
   (If not already available) Implement a query to get all `PresentationDetailsDTO` for the taxonomy, and build a parent/child map in memory.
5. **Find Submission by Date:**  
   Use `IDbmService.GetSubmissions` and filter by `CompanyId` and `ReportDate` to find the correct submission.
6. **Traverse and Output:**  
   Starting from the selected concept, recursively traverse children, querying for data points at each node, and outputting results in the selected format.

---

## DTOs and Data Structures

### ConceptDetailsDTO (namespace: Stocks.Persistence.Database.DTO.Taxonomies)
- `long ConceptId`
- `int TaxonomyTypeId`
- `int PeriodTypeId`
- `int BalanceTypeId`
- `bool IsAbstract`
- `string Name`
- `string Label`
- `string Documentation`

### PresentationDetailsDTO (namespace: Stocks.Persistence.Database.DTO.Taxonomies)
- `long PresentationId`
- `long ConceptId`
- `int Depth`
- `int OrderInDepth`
- `long ParentConceptId`
- `long ParentPresentationId`

### DataPoint (namespace: Stocks.DataModels)
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

---

## Output Format Examples

### CSV
#### ConceptName,Label,Value,Depth,ParentConceptName
Assets,Assets,350000,0,
Current Assets,Current Assets,150000,1,Assets
Cash and Cash Equivalents,Cash,100000,2,Current Assets
Accounts Receivable,Receivables,50000,2,Current Assets
Noncurrent Assets,Noncurrent Assets,200000,1,Assets
Property, Plant, Equipment,PPE,200000,2,Noncurrent Assets
### HTML<ul>
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
```json
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
```
---

## Error Handling

- If a company, concept, or submission is not found, log a warning and exit gracefully with a non-zero exit code.
- If a data point is missing for a concept, output an empty value and log a warning.
- All errors should be logged to stderr.
- **If required CLI arguments are missing or invalid, print usage instructions to stderr and exit with a non-zero code.**

---

## Testing

- Add Gherkin-style scenarios or xUnit tests for:
  - Listing available statements for a company.
  - Displaying a statement in each output format.
  - Handling missing company, concept, or data.
  - Recursion limit enforcement.
  - Date filtering.

---

## Kanban Task List

- [ ] Inspect DTOs and data access methods; document their properties here.
- [ ] Implement CLI argument parsing for all required switches.
- [ ] Implement StatementPrinter class with support for CSV, HTML, and JSON output.
- [ ] Implement/extend data access methods in IDbmService.
- [ ] Implement error handling and logging.
- [ ] Add Gherkin/xUnit tests for all major scenarios.
- [ ] Document sample outputs and update this requirements file as needed.

---

## Implementation Hints

- Use `ConceptDetailsDTO.IsAbstract == true` to find top-level statement concepts.
- Use `PresentationDetailsDTO` for parent/child navigation.
- Query for `DataPoint` by CompanyId, Submission
