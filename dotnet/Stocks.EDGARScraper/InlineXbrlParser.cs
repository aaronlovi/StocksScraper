using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;

namespace EDGARScraper;

internal record InlineXbrlSharesFact(string ContextRef, decimal Value);
internal record XbrlContextInfo(string Id, DateOnly InstantDate);
internal record AggregatedSharesFact(DateOnly Date, decimal TotalShares);

internal sealed class InlineXbrlParser {
    private const string EntityCommonStockSharesOutstanding = ":EntityCommonStockSharesOutstanding";

    internal async Task<IReadOnlyCollection<AggregatedSharesFact>> ParseSharesFromHtmlAsync(string html) {
        IBrowsingContext browsingContext = BrowsingContext.New(Configuration.Default);
        IDocument document = await browsingContext.OpenAsync(req => req.Content(html));

        // Step 1: Find all ix:nonFraction elements for EntityCommonStockSharesOutstanding
        List<InlineXbrlSharesFact> sharesFacts = ExtractSharesFacts(document);
        if (sharesFacts.Count == 0)
            return [];

        // Step 2: Parse all xbrli:context elements to get instant dates
        Dictionary<string, XbrlContextInfo> contexts = ExtractContexts(document);

        // Step 3: Group by date and take the largest share class per date
        return LargestByDate(sharesFacts, contexts);
    }

    private static List<InlineXbrlSharesFact> ExtractSharesFacts(IDocument document) {
        var facts = new List<InlineXbrlSharesFact>();

        // AngleSharp lowercases tag names, and ix:nonfraction is the tag in HTML
        IHtmlCollection<IElement> elements = document.QuerySelectorAll("ix\\:nonfraction");

        foreach (IElement element in elements) {
            string? name = element.GetAttribute("name");
            if (name is null || !name.EndsWith(EntityCommonStockSharesOutstanding, StringComparison.OrdinalIgnoreCase))
                continue;

            string? contextRef = element.GetAttribute("contextref");
            if (contextRef is null)
                continue;

            string valueText = element.TextContent.Trim().Replace(",", "");
            if (!decimal.TryParse(valueText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                continue;

            // Handle scale attribute (e.g., scale="6" means multiply by 10^6)
            string? scale = element.GetAttribute("scale");
            if (scale is not null && int.TryParse(scale, out int scaleValue) && scaleValue != 0) {
                for (int i = 0; i < Math.Abs(scaleValue); i++) {
                    if (scaleValue > 0)
                        value *= 10;
                    else
                        value /= 10;
                }
            }

            // Handle sign attribute
            string? sign = element.GetAttribute("sign");
            if (sign == "-")
                value = -value;

            facts.Add(new InlineXbrlSharesFact(contextRef, value));
        }

        return facts;
    }

    private static Dictionary<string, XbrlContextInfo> ExtractContexts(IDocument document) {
        var contexts = new Dictionary<string, XbrlContextInfo>(StringComparer.OrdinalIgnoreCase);

        IHtmlCollection<IElement> contextElements = document.QuerySelectorAll("xbrli\\:context");

        foreach (IElement contextElement in contextElements) {
            string? id = contextElement.GetAttribute("id");
            if (id is null)
                continue;

            // Look for xbrli:instant inside xbrli:period
            IElement? instantElement = contextElement.QuerySelector("xbrli\\:instant");
            if (instantElement is null)
                continue;

            string dateText = instantElement.TextContent.Trim();
            if (!DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly instantDate))
                continue;

            contexts[id] = new XbrlContextInfo(id, instantDate);
        }

        return contexts;
    }

    private static IReadOnlyCollection<AggregatedSharesFact> LargestByDate(
        List<InlineXbrlSharesFact> sharesFacts,
        Dictionary<string, XbrlContextInfo> contexts) {

        var maxByDate = new Dictionary<DateOnly, decimal>();

        foreach (InlineXbrlSharesFact fact in sharesFacts) {
            if (!contexts.TryGetValue(fact.ContextRef, out XbrlContextInfo? context))
                continue;

            if (maxByDate.TryGetValue(context.InstantDate, out decimal existing))
                maxByDate[context.InstantDate] = Math.Max(existing, fact.Value);
            else
                maxByDate[context.InstantDate] = fact.Value;
        }

        var results = new List<AggregatedSharesFact>(maxByDate.Count);
        foreach (KeyValuePair<DateOnly, decimal> entry in maxByDate)
            results.Add(new AggregatedSharesFact(entry.Key, entry.Value));

        return results;
    }
}
