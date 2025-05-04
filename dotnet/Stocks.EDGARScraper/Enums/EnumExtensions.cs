using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stocks.EDGARScraper.Enums;

internal static class EnumExtensions
{
    internal static bool IsWarningLevel(this XBRLFileParserFailureReason reason) =>
        reason switch
        {
            XBRLFileParserFailureReason.Invalid => false,
            XBRLFileParserFailureReason.FailedToFindSubmissions => false,
            XBRLFileParserFailureReason.CikIsZero => false,

            XBRLFileParserFailureReason.GeneralFault => true,
            XBRLFileParserFailureReason.FailedToDeserializeXbrlJson => true,
            XBRLFileParserFailureReason.FailedToFindCompanyIdForCIK => true,
            _ => true,
        };
}
