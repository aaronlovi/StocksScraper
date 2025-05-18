namespace Stocks.Persistence.Database.DTO;

public record TaxonomyConceptDTO(
    long TaxonomyConceptId, int TaxonomyTypeId, int TaxonomyPeriodTypeId, int TaxonomyBalanceTypeId, bool IsAbstract, string Name, string Label, string Documentation);
