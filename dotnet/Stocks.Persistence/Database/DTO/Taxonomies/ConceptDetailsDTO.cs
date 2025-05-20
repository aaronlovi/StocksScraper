namespace Stocks.Persistence.Database.DTO.Taxonomies;

public record ConceptDetailsDTO(
    long ConceptId, int TaxonomyTypeId, int PeriodTypeId, int BalanceTypeId, bool IsAbstract, string Name, string Label, string Documentation);
