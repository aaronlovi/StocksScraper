using Stocks.DataModels.Enums;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.EDGARScraper.Models.Taxonomies;

/// <summary>
/// Each concept represents a data point that can be reported in a filing.
/// </summary>
/// <param name="TaxonomyType">Corresponds to a taxonomy <see cref="TaxonomyTypes"/>, e.g. US-GAAP 2025.</param>
/// <param name="PeriodType">Whether this is a duration or instant in time. Corresponds to a <see cref="TaxonomyPeriodTypes"/>.</param>
/// <param name="Balance">Whether this is a credit, debit, or neither. Corresponds to a <see cref="TaxonomyBalanceTypes"/>.</param>
/// <param name="Abstract">Whether or not this is an "abstract" concept. "Abstract" concepts are typically roll-up concepts, containing other concepts</param>
/// <param name="Name">One-word name for the concept.</param>
/// <param name="Label">Brief description of the concept.</param>
/// <param name="Documentation">Long description of the concept.</param>
internal record ConceptDetails(
    TaxonomyTypes TaxonomyType,
    string PeriodType,
    string Balance,
    string Abstract,
    string Name,
    string Label,
    string Documentation) {

    internal Result<ConceptDetailsDTO> ToConceptDetailsDTO(long id) {
        Result<TaxonomyPeriodTypes> parsePeriodTypeResult = ParsePeriodType();
        if (parsePeriodTypeResult.IsFailure)
            return Result<ConceptDetailsDTO>.Failure(parsePeriodTypeResult);

        Result<TaxonomyBalanceTypes> parseBalanceTypeResult = ParseBalanceType();
        if (parseBalanceTypeResult.IsFailure)
            return Result<ConceptDetailsDTO>.Failure(parseBalanceTypeResult);

        Result<bool> parseIsAbstractResult = ParseIsAbstract();
        if (parseIsAbstractResult.IsFailure)
            return Result<ConceptDetailsDTO>.Failure(parseIsAbstractResult);

        var dto = new ConceptDetailsDTO(
            id,
            (int)TaxonomyType,
            (int)parsePeriodTypeResult.Value,
            (int)parseBalanceTypeResult.Value,
            parseIsAbstractResult.Value,
            Name.Trim(),
            Label.Trim(),
            Documentation.Trim());
        return Result<ConceptDetailsDTO>.Success(dto);
    }

    #region PRIVATE HELPER METHODS

    private Result<TaxonomyPeriodTypes> ParsePeriodType() {
        return PeriodType.Trim() switch {
            "duration" => Result<TaxonomyPeriodTypes>.Success(TaxonomyPeriodTypes.Duration),
            "instant" => Result<TaxonomyPeriodTypes>.Success(TaxonomyPeriodTypes.Instant),
            _ => Result<TaxonomyPeriodTypes>.Failure(ErrorCodes.ValidationError, $"Invalid period type: {PeriodType}", PeriodType)
        };
    }

    private Result<TaxonomyBalanceTypes> ParseBalanceType() {
        return Balance.Trim() switch {
            "credit" => Result<TaxonomyBalanceTypes>.Success(TaxonomyBalanceTypes.Credit),
            "debit" => Result<TaxonomyBalanceTypes>.Success(TaxonomyBalanceTypes.Debit),
            "" => Result<TaxonomyBalanceTypes>.Success(TaxonomyBalanceTypes.NotApplicable),
            _ => Result<TaxonomyBalanceTypes>.Failure(ErrorCodes.ValidationError, $"Invalid balance type: {Balance}", Balance)
        };
    }

    private Result<bool> ParseIsAbstract() {
        if (string.IsNullOrWhiteSpace(Abstract))
            return Result<bool>.Success(false); // Valid -- not an abstract concept

        if (Abstract.EqualsInvariant("true"))
            return Result<bool>.Success(true); // Valid -- is an abstract concept
        if (Abstract.EqualsInvariant("false"))
            return Result<bool>.Success(false); // Valid -- not an abstract concept

        return Result<bool>.Failure(ErrorCodes.ValidationError, $"Invalid abstract value: {Abstract}", Abstract);
    }

    #endregion
}
