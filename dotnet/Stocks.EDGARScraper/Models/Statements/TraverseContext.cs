using System.Collections.Generic;
using Stocks.Persistence.Database.DTO.Taxonomies;

namespace Stocks.EDGARScraper.Models.Statements;

public record TraverseContext(
    long ConceptId,
    Dictionary<long, List<PresentationDetailsDTO>> ParentToChildren,
    Dictionary<long, ConceptDetailsDTO> ConceptMap,
    int Depth,
    int MaxDepth,
    long? ParentConceptId,
    HashSet<long> Visited
);
