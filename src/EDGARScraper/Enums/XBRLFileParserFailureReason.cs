namespace Stocks.EDGARScraper.Enums;

internal enum XBRLFileParserFailureReason
{
    Invalid = 0,
    GeneralFault,
    FailedToDeserializeXbrlJson,
    FailedToFindCompanyIdForCIK,
    FailedToFindSubmissions,
}
