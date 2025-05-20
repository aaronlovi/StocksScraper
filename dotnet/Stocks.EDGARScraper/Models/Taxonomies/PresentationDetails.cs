using System.Collections.Generic;
using Stocks.DataModels.Enums;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.EDGARScraper.Models.Taxonomies;

/// <summary>
/// Each presentation detail represents how to display a concept in a filing.
/// </summary>
/// <param name="IsUsGaapPresentationDetail">
/// Indicates both that the concept is US-GAAP, and it's root node is US-GAAP.
/// This indicates whether the record will be persisted to the database
/// </param>
/// <param name="Prefix">Prefix. We are only interested in "us-gaap", but need other prefixes to construct the entire tree</param>
/// <param name="TaxonomyType">Corresponds to a taxonomy <see cref="TaxonomyTypes"/>, e.g. US-GAAP 2025.</param>
/// <param name="ConceptName">The name of the concept that this presentation detail represents.</param>
/// <param name="Depth">The depth at which to display this concept.</param>
/// <param name="OrderInDepth">The relative order among other concepts at the current depth at which to display this concept.</param>
/// <param name="ParentPresentationDetails">The parent presentation details, or string.Empty for root-level.</param>
internal record PresentationDetails(
    bool IsUsGaapPresentationDetail,
    TaxonomyTypes TaxonomyType,
    string Prefix,
    string ConceptName,
    string Depth,
    string OrderInDepth,
    PresentationDetails? ParentPresentationDetails) {

    internal string ParentConceptName => ParentPresentationDetails?.ConceptName ?? string.Empty;

    internal Result<PresentationDetailsDTO> ToPresentationDetailsDTO(
        long id,
        long parentPresentationId,
        IReadOnlyDictionary<string, long> conceptIdsByName) {
        if (!conceptIdsByName.TryGetValue(ConceptName, out long conceptId)) {
            return Result<PresentationDetailsDTO>.Failure(
                ErrorCodes.ValidationError,
                $"Concept name '{ConceptName}' not found in concept IDs dictionary.",
                ConceptName);
        }
        if (!conceptIdsByName.TryGetValue(ParentConceptName, out long parentConceptId)) {
            return Result<PresentationDetailsDTO>.Failure(
                ErrorCodes.ValidationError,
                $"Parent concept name '{ParentConceptName}' not found in concept IDs dictionary.",
                ParentConceptName);
        }
        if (!int.TryParse(Depth, out int depth)) {
            return Result<PresentationDetailsDTO>.Failure(
                ErrorCodes.ValidationError,
                $"Invalid depth '{Depth}' for concept '{ConceptName}'.",
                ConceptName);
        }
        if (!decimal.TryParse(OrderInDepth, out decimal orderInDepth)) {
            return Result<PresentationDetailsDTO>.Failure(
                ErrorCodes.ValidationError,
                $"Invalid order in depth '{OrderInDepth}' for concept '{ConceptName}'.",
                ConceptName);
        }
        decimal orderInDepthScaled100 = orderInDepth * 100m;
        if (!orderInDepthScaled100.IsInteger()) {
            return Result<PresentationDetailsDTO>.Failure(
                ErrorCodes.ValidationError,
                $"Invalid order in depth '{OrderInDepth}' for concept '{ConceptName}'.",
                ConceptName);
        }
        var dto = new PresentationDetailsDTO(
            id,
            conceptId,
            depth,
            (int)orderInDepthScaled100,
            parentConceptId,
            parentPresentationId);
        return Result<PresentationDetailsDTO>.Success(dto);
    }

    #region PRIVATE HELPER METHODS

    #endregion
}
