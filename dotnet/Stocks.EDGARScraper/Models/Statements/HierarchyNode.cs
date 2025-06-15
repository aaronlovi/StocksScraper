namespace Stocks.EDGARScraper.Models.Statements;

public record HierarchyNode
{
    public long ConceptId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Depth { get; init; }
    public long? ParentConceptId { get; init; }
}
