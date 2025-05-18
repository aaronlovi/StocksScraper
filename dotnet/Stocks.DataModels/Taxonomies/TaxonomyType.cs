using System.Collections.Generic;
using Stocks.DataModels.Enums;

namespace Stocks.DataModels.Taxonomies;

public record TaxonomyType(TaxonomyTypes TypeId, string Name, int Version) {
    public static readonly List<TaxonomyType> List
        = [new(TaxonomyTypes.US_GAAP_2025, "us-gaap", 2025)];
}
