using System.Collections.Generic;
using System.Xml.Linq;
using Stocks.DataModels;

namespace EDGARScraper;

internal record XBRLMetadata(
    XNamespace RootNamespace,
    XNamespace UsGaapNamespace,
    XDocument XDocument,
    Dictionary<string, DatePair> Contexts)
{
    public static readonly XBRLMetadata Empty = new(XNamespace.None, XNamespace.None, new XDocument(), []);
}
