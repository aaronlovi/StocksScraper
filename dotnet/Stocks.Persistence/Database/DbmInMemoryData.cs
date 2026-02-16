using System;
using System.Collections.Generic;
using Stocks.DataModels;

namespace Stocks.Persistence.Database;

public sealed class DbmInMemoryData {
    private readonly object _mutex = new();
    private readonly Dictionary<string, PriceImportStatus> _priceImports = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PriceDownloadStatus> _priceDownloads = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PriceRow> _prices = [];
    private readonly Dictionary<string, TaxonomyTypeInfo> _taxonomyTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Company> _companies = [];
    private readonly List<CompanyName> _companyNames = [];
    private readonly List<CompanyTicker> _companyTickers = [];
    private readonly List<Submission> _submissions = [];

    // Companies

    public void AddCompanies(IReadOnlyCollection<Company> companies) {
        lock (_mutex)
            _companies.AddRange(companies);
    }

    public Company? GetCompanyById(ulong companyId) {
        lock (_mutex) {
            foreach (Company company in _companies) {
                if (company.CompanyId == companyId)
                    return company;
            }
        }
        return null;
    }

    public Company? GetCompanyByCik(ulong cik) {
        lock (_mutex) {
            foreach (Company company in _companies) {
                if (company.Cik == cik)
                    return company;
            }
        }
        return null;
    }

    // Company names

    public void AddCompanyNames(IReadOnlyCollection<CompanyName> names) {
        lock (_mutex)
            _companyNames.AddRange(names);
    }

    public IReadOnlyCollection<CompanyName> GetCompanyNames() {
        lock (_mutex)
            return [.. _companyNames];
    }

    // Company tickers

    public void AddOrUpdateCompanyTickers(IReadOnlyCollection<CompanyTicker> tickers) {
        lock (_mutex) {
            foreach (CompanyTicker ticker in tickers) {
                bool found = false;
                for (int i = 0; i < _companyTickers.Count; i++) {
                    if (_companyTickers[i].CompanyId == ticker.CompanyId
                        && string.Equals(_companyTickers[i].Ticker, ticker.Ticker, StringComparison.OrdinalIgnoreCase)) {
                        _companyTickers[i] = ticker;
                        found = true;
                        break;
                    }
                }
                if (!found)
                    _companyTickers.Add(ticker);
            }
        }
    }

    public IReadOnlyCollection<CompanyTicker> GetCompanyTickersByCompanyId(ulong companyId) {
        var results = new List<CompanyTicker>();
        lock (_mutex) {
            foreach (CompanyTicker ticker in _companyTickers) {
                if (ticker.CompanyId == companyId)
                    results.Add(ticker);
            }
        }
        return results;
    }

    // Submissions

    public void AddSubmissions(IReadOnlyCollection<Submission> submissions) {
        lock (_mutex)
            _submissions.AddRange(submissions);
    }

    public IReadOnlyCollection<Submission> GetSubmissionsByCompanyId(ulong companyId) {
        var results = new List<Submission>();
        lock (_mutex) {
            foreach (Submission submission in _submissions) {
                if (submission.CompanyId == companyId)
                    results.Add(submission);
            }
        }
        results.Sort((a, b) => b.ReportDate.CompareTo(a.ReportDate));
        return results;
    }

    // Prices

    public IReadOnlyCollection<PriceImportStatus> GetPriceImports() {
        lock (_mutex)
            return [.. _priceImports.Values];
    }

    public void UpsertPriceImport(PriceImportStatus status) {
        string key = BuildImportKey(status.Cik, status.Ticker, status.Exchange);
        lock (_mutex)
            _priceImports[key] = status;
    }

    public IReadOnlyCollection<PriceDownloadStatus> GetPriceDownloads() {
        lock (_mutex)
            return [.. _priceDownloads.Values];
    }

    public void UpsertPriceDownload(PriceDownloadStatus status) {
        string key = BuildImportKey(status.Cik, status.Ticker, status.Exchange);
        lock (_mutex)
            _priceDownloads[key] = status;
    }

    public void DeletePricesForTicker(string ticker) {
        if (string.IsNullOrWhiteSpace(ticker))
            return;
        string normalized = ticker.Trim().ToUpperInvariant();
        lock (_mutex) {
            for (int i = _prices.Count - 1; i >= 0; i--) {
                if (string.Equals(_prices[i].Ticker, normalized, StringComparison.OrdinalIgnoreCase))
                    _prices.RemoveAt(i);
            }
        }
    }

    public void AddPrices(IReadOnlyCollection<PriceRow> prices) {
        lock (_mutex)
            _prices.AddRange(prices);
    }

    public IReadOnlyCollection<PriceRow> GetPrices() {
        lock (_mutex)
            return [.. _prices];
    }

    public IReadOnlyCollection<PriceRow> GetPricesByTicker(string ticker) {
        var results = new List<PriceRow>();
        if (string.IsNullOrWhiteSpace(ticker))
            return results;
        string normalized = ticker.Trim().ToUpperInvariant();
        lock (_mutex) {
            foreach (PriceRow price in _prices) {
                if (string.Equals(price.Ticker, normalized, StringComparison.OrdinalIgnoreCase))
                    results.Add(price);
            }
        }
        return results;
    }

    // Taxonomy types

    public TaxonomyTypeInfo? GetTaxonomyType(string name, int version) {
        string key = BuildTaxonomyKey(name, version);
        lock (_mutex)
            return _taxonomyTypes.TryGetValue(key, out TaxonomyTypeInfo? value) ? value : null;
    }

    public TaxonomyTypeInfo AddTaxonomyType(string name, int version) {
        string key = BuildTaxonomyKey(name, version);
        lock (_mutex) {
            if (_taxonomyTypes.TryGetValue(key, out TaxonomyTypeInfo? existing))
                return existing;
            int nextId = _taxonomyTypes.Count + 1;
            var created = new TaxonomyTypeInfo(nextId, name, version);
            _taxonomyTypes[key] = created;
            return created;
        }
    }

    public int GetTaxonomyConceptCount(int taxonomyTypeId) => 0;

    public int GetTaxonomyPresentationCount(int taxonomyTypeId) => 0;

    // Helpers

    private static string BuildImportKey(ulong cik, string ticker, string? exchange) {
        string normalizedTicker = string.IsNullOrWhiteSpace(ticker) ? string.Empty : ticker.Trim().ToUpperInvariant();
        string normalizedExchange = string.IsNullOrWhiteSpace(exchange) ? string.Empty : exchange.Trim().ToUpperInvariant();
        return $"{cik}|{normalizedTicker}|{normalizedExchange}";
    }

    private static string BuildTaxonomyKey(string name, int version) {
        string normalizedName = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim().ToLowerInvariant();
        return $"{normalizedName}|{version}";
    }
}
