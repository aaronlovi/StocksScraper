namespace Stocks.EDGARScraper.Models.Taxonomies;

public record UsGaap2025ConceptsFileProcessorOptions {
    public required string CsvPath { get; init; }
}
