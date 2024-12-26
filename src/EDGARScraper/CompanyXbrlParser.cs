using MongoDB.Bson;
using System;
using System.Threading.Tasks;

namespace EDGARScraper;

internal static class CompanyXbrlParser
{
    internal static async Task<BsonDocument?> ParseXbrlUrlToDocument(CompanyXbrlLink companyXbrlLink, EdgarHttpClientService httpClientService)
    {
        string? xbrlContent = await httpClientService.FetchContentAsync(companyXbrlLink.XbrlUrl);
        if (xbrlContent is null)
        {
            Console.WriteLine($"Failed to fetch XBRL content from {companyXbrlLink.XbrlUrl}");
            return null;
        }

        return new BsonDocument
        {
            { "company", companyXbrlLink.CompanyDoc },
            { "filing_date", companyXbrlLink.FilingDate },
            { "xbrl_url", companyXbrlLink.XbrlUrl },
            { "content", xbrlContent },
            { "downloaded_at", DateTime.UtcNow }
        };
    }
}