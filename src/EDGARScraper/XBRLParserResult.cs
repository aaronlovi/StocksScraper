using Stocks.EDGARScraper.Enums;
using Stocks.Shared;

namespace EDGARScraper;

internal record XBRLParserResult : Results
{
    internal XBRLParserResult(
        bool success, string errorMessage = "", XBRLFileParserFailureReason reason = XBRLFileParserFailureReason.Invalid)
        : base(success, errorMessage)
    {
        Reason = reason;
    }

    internal XBRLFileParserFailureReason Reason { get; init; }

    internal bool IsWarningLevel => Reason.IsWarningLevel();

    internal static new XBRLParserResult SuccessResult() => new(true, "");

    internal static XBRLParserResult FailureResult(string errorMessage, XBRLFileParserFailureReason reason) =>
        new(false, errorMessage, reason);

    internal static XBRLParserResult FailedToDeserializeXbrlJson() =>
        FailureResult("Failed to deserialize XBRL JSON.", XBRLFileParserFailureReason.FailedToDeserializeXbrlJson);

    internal static XBRLParserResult FailedToFindCompanyIdForCIK(ulong cik) =>
        FailureResult($"Failed to find company ID for CIK {cik}, aborting", XBRLFileParserFailureReason.FailedToFindCompanyIdForCIK);

    internal static XBRLParserResult FailedToFindSubmissions(ulong companyId) =>
        FailureResult($"Failed to find submissions for company ID {companyId}, aborting", XBRLFileParserFailureReason.FailedToFindSubmissions);

    internal static XBRLParserResult GeneralFault(string errorMessage) =>
        FailureResult(errorMessage, XBRLFileParserFailureReason.GeneralFault);
}
