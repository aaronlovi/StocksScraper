namespace Stocks.Persistence.Database.DTO.Taxonomies;

public record PresentationDetailsDTO(
    long PresentationId,
    long ConceptId,
    int Depth,
    int OrderInDepth,
    long ParentConceptId,
    long ParentPresentationId,
    string RoleName);
