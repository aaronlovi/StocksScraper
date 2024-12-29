using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Stocks.DataModels;

namespace EDGARScraper;

internal class XBRLParser(MongoDbService _mongoDbService)
{
    private XBRLMetadata _xbrlMetadata = XBRLMetadata.Empty;

    public async Task ParseXBRL()
    {
        BsonDocument? xbrlDoc = await _mongoDbService.GetOneXbrlDocument();
        if (xbrlDoc is null)
        {
            Console.WriteLine("No XBRL documents found in the database.");
            return;
        }

        if (!xbrlDoc.TryGetValue("company", out BsonValue companyValue) ||
            !companyValue.AsBsonDocument.TryGetValue("cik", out BsonValue cikValue))
        {
            Console.WriteLine("Company CIK not found in the document.");
            return;
        }
        string cik = cikValue.AsString;

        if (!xbrlDoc.TryGetValue("filing_date", out BsonValue filingDateValue))
        {
            Console.WriteLine("Filing date not found in the document.");
            return;
        }
        string filingDate = filingDateValue.AsString;

        string content = xbrlDoc["content"].AsString;
        if (string.IsNullOrEmpty(content))
        {
            Console.WriteLine("XBRL document content is empty.");
            return;
        }

        XDocument xDocument = XDocument.Parse(content);
        XNamespace rootNamespace = GetRootNamespace(xDocument);
        XNamespace usGaapNamespace = GetUsGaapNamespace(xDocument);
        Dictionary<string, DatePair> contexts = GetContexts(xDocument, rootNamespace);
        _xbrlMetadata = new XBRLMetadata(rootNamespace, usGaapNamespace, xDocument, contexts);

        BsonArray metrics = ParseUsGaapMetrics();

        var financialData = new BsonDocument
        {
            { "company", xbrlDoc["company"].AsBsonDocument },
            { "filing_date", filingDate },
            { "metrics", metrics },
            { "parsed_at", DateTime.UtcNow }
        };

        await _mongoDbService.SaveFinancialData(cik, filingDate, financialData);
    }

    private BsonArray ParseUsGaapMetrics()
    {
        var metrics = new BsonArray();
        var processedMetrics = new HashSet<string>();

        IEnumerable<XElement> elements = _xbrlMetadata.XDocument.Descendants()
            .Where(e => e.Name.Namespace == _xbrlMetadata.UsGaapNamespace);

        foreach (XElement element in elements)
        {
            string metricName = element.Name.LocalName;
            string unitRef = element.Attribute("unitRef")?.Value ?? "unknown";
            string decimalsAttr = element.Attribute("decimals")?.Value ?? "0";
            string contextRef = element.Attribute("contextRef")?.Value ?? "";

            if (string.IsNullOrEmpty(contextRef)) continue;

            string metricKey = $"{metricName}_{contextRef}_{unitRef}_{decimalsAttr}";
            if (processedMetrics.Contains(metricKey)) continue;

            if (!decimal.TryParse(element.Value, out decimal rawValue))
            {
                string truncatedValue = element.Value.GetTruncatedStringForLogs();
                Console.WriteLine($"Failed to parse value for metric '{metricName}': {truncatedValue}");
                continue;
            }

            // Set the dates based on contextRef
            if (!_xbrlMetadata.Contexts.TryGetValue(contextRef, out DatePair? datePair))
            {
                Console.WriteLine($"Failed to find context for metric '{metricName}' with contextRef '{contextRef}'");
                continue;
            }

            metrics.Add(new BsonDocument
            {
                { "metric_name", metricName },
                { "value", rawValue },
                { "unit", unitRef },
                { "decimals", decimalsAttr },
                { "start_date", datePair.StartTimeUtc },
                { "end_date", datePair.EndTimeUtc }
            });

            processedMetrics.Add(metricKey);
        }

        return metrics;
    }

    private static XNamespace GetRootNamespace(XDocument xDocument)
    {
        XElement? xbrlElement = xDocument.Root;

        if (xbrlElement is null) return XNamespace.None;

        return xbrlElement.Name.Namespace;
    }

    private static XNamespace GetUsGaapNamespace(XDocument xDocument)
    {
        XElement? xbrlElement = xDocument.Root;

        if (xbrlElement is null) return XNamespace.None;

        string? usGaapNamespace = xbrlElement
            .Attributes()
            .FirstOrDefault(attr => attr.IsNamespaceDeclaration && attr.Name.LocalName == "us-gaap")?
            .Value;

        if (usGaapNamespace is null) return XNamespace.None;

        return XNamespace.Get(usGaapNamespace);
    }

    private static Dictionary<string, DatePair> GetContexts(XDocument xDoc, XNamespace rootNamespace)
    {
        var contextMap = new Dictionary<string, DatePair>();

        IEnumerable<XElement> elements = xDoc.Descendants(rootNamespace + "context");
        foreach (XElement element in elements)
        {
            string id = element.Attribute("id")?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(id)) continue;

            XElement? periodElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == "period");
            if (periodElement is null) continue;

            XElement? instantElement = periodElement.Elements().FirstOrDefault(e => e.Name.LocalName == "instant");
            if (instantElement?.Value is string val && DateOnly.TryParseExact(val, "yyyy-MM-dd", out DateOnly instant))
            {
                contextMap.Add(id, new DatePair(instant, instant));
                continue;
            }

            XElement? startDateElement = periodElement.Elements().FirstOrDefault(e => e.Name.LocalName == "startDate");
            XElement? endDateElement = periodElement.Elements().FirstOrDefault(e => e.Name.LocalName == "endDate");
            if (startDateElement?.Value is string startDate &&
                endDateElement?.Value is string endDate &&
                DateOnly.TryParseExact(startDate, "yyyy-MM-dd", out DateOnly start) &&
                DateOnly.TryParseExact(endDate, "yyyy-MM-dd", out DateOnly end))
            {
                contextMap.Add(id, new DatePair(start, end));
            }
        }

        return contextMap;
    }
}
