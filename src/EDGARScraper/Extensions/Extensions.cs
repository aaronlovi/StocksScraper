using Stocks.EDGARScraper.Enums;

namespace Stocks.EDGARScraper.Extensions;

internal static class Extensions
{
    internal static bool IsWarningLevel(this XBRLFileParserFailureReason reason) =>
        reason switch
        {
            XBRLFileParserFailureReason.Invalid => false,
            XBRLFileParserFailureReason.FailedToFindSubmissions => false,

            XBRLFileParserFailureReason.GeneralFault => true,
            XBRLFileParserFailureReason.FailedToDeserializeXbrlJson => true,
            XBRLFileParserFailureReason.FailedToFindCompanyIdForCIK => true,
            _ => true,
        };
}
