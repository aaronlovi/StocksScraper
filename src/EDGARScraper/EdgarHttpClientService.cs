using MongoDB.Bson;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace EDGARScraper;

internal class EdgarHttpClientService
{
    private readonly HttpClient _httpClient;

    internal EdgarHttpClientService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EDGARScraper (inno.and.logic@gmail.com)");
    }

    internal async Task<string?> FetchContentAsync(string url)
    {
        try
        {
            return await _httpClient.GetStringAsync(url);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Failed to fetch content from {url}. Error: {ex.Message}");
            return null;
        }
    }
}
