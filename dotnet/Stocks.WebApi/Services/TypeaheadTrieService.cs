using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Shared;

namespace Stocks.WebApi.Services;

public record TypeaheadResult(string Text, string Type, string Cik);

public class TypeaheadTrieService : IHostedService {
    private readonly IDbmService _dbm;
    private TrieNode _root = new();

    public TypeaheadTrieService(IDbmService dbm) {
        _dbm = dbm;
    }

    public async Task StartAsync(CancellationToken ct) {
        const string DataSource = "EDGAR";
        _root = new TrieNode();

        Result<IReadOnlyCollection<Company>> companiesResult =
            await _dbm.GetAllCompaniesByDataSource(DataSource, ct);
        if (companiesResult.IsFailure || companiesResult.Value is null)
            return;

        var companyMap = new Dictionary<ulong, Company>();
        foreach (Company c in companiesResult.Value) {
            companyMap[c.CompanyId] = c;
            Insert(c.Cik.ToString(), "company", c.Cik.ToString());
        }

        Result<IReadOnlyCollection<CompanyName>> namesResult =
            await _dbm.GetAllCompanyNames(ct);
        if (namesResult.IsSuccess && namesResult.Value is not null) {
            foreach (CompanyName cn in namesResult.Value) {
                if (companyMap.TryGetValue(cn.CompanyId, out Company? company))
                    Insert(cn.Name, "company", company.Cik.ToString());
            }
        }

        Result<IReadOnlyCollection<CompanyTicker>> tickersResult =
            await _dbm.GetAllCompanyTickers(ct);

        if (tickersResult.IsSuccess && tickersResult.Value is not null) {
            foreach (CompanyTicker ticker in tickersResult.Value) {
                if (companyMap.TryGetValue(ticker.CompanyId, out Company? company))
                    Insert(ticker.Ticker, "ticker", company.Cik.ToString());
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public List<TypeaheadResult> Search(string prefix, int maxResults = 10) {
        var results = new List<TypeaheadResult>();
        if (string.IsNullOrWhiteSpace(prefix))
            return results;

        string lowerPrefix = prefix.ToLowerInvariant();
        TrieNode? node = _root;
        foreach (char ch in lowerPrefix) {
            if (!node.Children.TryGetValue(ch, out TrieNode? child))
                return results;
            node = child;
        }

        CollectResults(node, results, maxResults);
        return results;
    }

    private void Insert(string text, string type, string cik) {
        string lower = text.ToLowerInvariant();
        TrieNode node = _root;
        foreach (char ch in lower) {
            if (!node.Children.TryGetValue(ch, out TrieNode? child)) {
                child = new TrieNode();
                node.Children[ch] = child;
            }
            node = child;
        }
        node.Entries.Add(new TypeaheadResult(text, type, cik));
    }

    private static void CollectResults(TrieNode node, List<TypeaheadResult> results, int maxResults) {
        foreach (TypeaheadResult entry in node.Entries) {
            if (results.Count >= maxResults)
                return;
            results.Add(entry);
        }
        foreach ((char _, TrieNode child) in node.Children) {
            if (results.Count >= maxResults)
                return;
            CollectResults(child, results, maxResults);
        }
    }

    private class TrieNode {
        public Dictionary<char, TrieNode> Children { get; } = new();
        public List<TypeaheadResult> Entries { get; } = new();
    }
}
