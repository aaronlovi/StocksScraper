using System.Collections.Generic;
using System.Xml.Linq;

namespace EDGARScraper;

internal record XBRLMetadata(
    XNamespace RootNamespace,
    XNamespace UsGaapNamespace,
    XDocument XDocument,
    Dictionary<string, DatePair> Contexts)
{
    public static readonly XBRLMetadata Empty = new(XNamespace.None, XNamespace.None, new XDocument(), new Dictionary<string, DatePair>());
}
