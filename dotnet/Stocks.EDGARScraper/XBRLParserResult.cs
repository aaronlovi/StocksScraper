using Stocks.EDGARScraper.Enums;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace EDGARScraper;

internal record XBRLParserResult : Result {
    internal static new readonly XBRLParserResult Success = new(ErrorCodes.None);

    internal XBRLParserResult(
        ErrorCodes errorCode, string errorMessage = "", XBRLFileParserFailureReason reason = XBRLFileParserFailureReason.Invalid)
        : base(errorCode, errorMessage) {
        Reason = reason;
    }

    internal XBRLFileParserFailureReason Reason { get; init; }

    internal bool IsWarningLevel => Reason.IsWarningLevel();


    internal static XBRLParserResult Failure(string errorMessage, XBRLFileParserFailureReason reason) =>
        new(ErrorCodes.GenericError, errorMessage, reason);

    internal static XBRLParserResult FailedToDeserializeXbrlJson() =>
        Failure("Failed to deserialize XBRL JSON.", XBRLFileParserFailureReason.FailedToDeserializeXbrlJson);

    internal static XBRLParserResult FailedToFindCompanyIdForCIK(ulong cik) =>
        Failure($"Failed to find company ID for CIK {cik}, aborting", XBRLFileParserFailureReason.FailedToFindCompanyIdForCIK);

    internal static XBRLParserResult FailedToFindSubmissions(ulong companyId) =>
        Failure($"Failed to find submissions for company ID {companyId}, aborting", XBRLFileParserFailureReason.FailedToFindSubmissions);

    internal static XBRLParserResult CikIsZero() =>
        Failure("CIK is 0", XBRLFileParserFailureReason.CikIsZero);

    internal static XBRLParserResult GeneralFault(string errorMessage) =>
        Failure(errorMessage, XBRLFileParserFailureReason.GeneralFault);
}
