using System;
using System.Collections.Generic;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.Persistence.Database.DTO.Taxonomies;

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
    private readonly List<DataPoint> _dataPoints = [];

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

    public IReadOnlyCollection<Company> GetAllCompaniesByDataSource(string dataSource) {
        var results = new List<Company>();
        lock (_mutex) {
            foreach (Company company in _companies) {
                if (string.Equals(company.DataSource, dataSource, StringComparison.OrdinalIgnoreCase))
                    results.Add(company);
            }
        }
        return results;
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

    public IReadOnlyCollection<CompanyName> GetCompanyNamesByCompanyId(ulong companyId) {
        var results = new List<CompanyName>();
        lock (_mutex) {
            foreach (CompanyName name in _companyNames) {
                if (name.CompanyId == companyId)
                    results.Add(name);
            }
        }
        return results;
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

    public IReadOnlyCollection<CompanyTicker> GetAllCompanyTickers() {
        lock (_mutex)
            return [.. _companyTickers];
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

    // Search

    public PagedResults<CompanySearchResult> SearchCompanies(string query, PaginationRequest pagination) {
        var matches = new List<CompanySearchResult>();
        string queryLower = query.ToLowerInvariant();

        lock (_mutex) {
            var seen = new HashSet<ulong>();

            foreach (Company company in _companies) {
                if (seen.Contains(company.CompanyId))
                    continue;

                bool matched = false;
                string? matchedName = null;

                // Check CIK exact match
                if (company.Cik.ToString() == query) {
                    matched = true;
                }

                // Check company names (case-insensitive substring)
                if (!matched) {
                    foreach (CompanyName cn in _companyNames) {
                        if (cn.CompanyId == company.CompanyId
                            && cn.Name.ToLowerInvariant().Contains(queryLower)) {
                            matched = true;
                            matchedName = cn.Name;
                            break;
                        }
                    }
                }

                // Check tickers (case-insensitive substring)
                string? matchedTicker = null;
                string? matchedExchange = null;
                foreach (CompanyTicker ct in _companyTickers) {
                    if (ct.CompanyId == company.CompanyId) {
                        if (!matched && ct.Ticker.ToLowerInvariant().Contains(queryLower))
                            matched = true;
                        if (matchedTicker is null) {
                            matchedTicker = ct.Ticker;
                            matchedExchange = ct.Exchange;
                        }
                    }
                }

                // Get first name if not matched by name
                if (matched && matchedName is null) {
                    foreach (CompanyName cn in _companyNames) {
                        if (cn.CompanyId == company.CompanyId) {
                            matchedName = cn.Name;
                            break;
                        }
                    }
                }

                if (matched) {
                    _ = seen.Add(company.CompanyId);

                    decimal? latestPrice = null;
                    DateOnly? latestPriceDate = null;
                    if (matchedTicker is not null) {
                        DateOnly maxDate = DateOnly.MinValue;
                        foreach (PriceRow price in _prices) {
                            if (string.Equals(price.Ticker, matchedTicker, StringComparison.OrdinalIgnoreCase)
                                && price.PriceDate > maxDate) {
                                maxDate = price.PriceDate;
                                latestPrice = price.Close;
                                latestPriceDate = price.PriceDate;
                            }
                        }
                    }

                    matches.Add(new CompanySearchResult(
                        company.CompanyId,
                        company.Cik.ToString(),
                        matchedName ?? string.Empty,
                        matchedTicker,
                        matchedExchange,
                        latestPrice,
                        latestPriceDate));
                }
            }
        }

        uint totalItems = (uint)matches.Count;
        int offset = (int)((pagination.PageNumber - 1) * pagination.PageSize);
        int limit = (int)pagination.PageSize;

        var page = new List<CompanySearchResult>();
        for (int i = offset; i < matches.Count && page.Count < limit; i++)
            page.Add(matches[i]);

        uint totalPages = totalItems == 0 ? 0 : (uint)System.Math.Ceiling(totalItems / (double)pagination.PageSize);
        var paginationResponse = new PaginationResponse(pagination.PageNumber, totalItems, totalPages);
        return new PagedResults<CompanySearchResult>(page, paginationResponse);
    }

    // Data points

    public void AddDataPoints(IReadOnlyCollection<DataPoint> dataPoints) {
        lock (_mutex)
            _dataPoints.AddRange(dataPoints);
    }

    // Dashboard

    public DashboardStats GetDashboardStats() {
        lock (_mutex) {
            long totalCompanies = _companies.Count;
            long totalSubmissions = _submissions.Count;
            long totalDataPoints = _dataPoints.Count;

            DateOnly? earliest = null;
            DateOnly? latest = null;
            foreach (Submission s in _submissions) {
                if (earliest is null || s.ReportDate < earliest)
                    earliest = s.ReportDate;
                if (latest is null || s.ReportDate > latest)
                    latest = s.ReportDate;
            }

            var distinctTickers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PriceImportStatus import in _priceImports.Values)
                _ = distinctTickers.Add(import.Ticker);
            long companiesWithPriceData = distinctTickers.Count;

            var byType = new Dictionary<string, long>();
            foreach (Submission s in _submissions) {
                string typeName = FilingTypeToName(s.FilingType);
                if (byType.ContainsKey(typeName))
                    byType[typeName]++;
                else
                    byType[typeName] = 1;
            }

            return new DashboardStats(
                totalCompanies,
                totalSubmissions,
                totalDataPoints,
                earliest,
                latest,
                companiesWithPriceData,
                byType);
        }
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

    // Taxonomy concepts

    private readonly List<ConceptDetailsDTO> _taxonomyConcepts = [];

    public void AddTaxonomyConcepts(IReadOnlyCollection<ConceptDetailsDTO> concepts) {
        lock (_mutex)
            _taxonomyConcepts.AddRange(concepts);
    }

    public IReadOnlyCollection<ConceptDetailsDTO> GetTaxonomyConceptsByTaxonomyType(int taxonomyTypeId) {
        var results = new List<ConceptDetailsDTO>();
        lock (_mutex) {
            foreach (ConceptDetailsDTO c in _taxonomyConcepts) {
                if (c.TaxonomyTypeId == taxonomyTypeId)
                    results.Add(c);
            }
        }
        return results;
    }

    // Taxonomy presentations

    private readonly List<PresentationDetailsDTO> _taxonomyPresentations = [];

    public void AddTaxonomyPresentations(IReadOnlyCollection<PresentationDetailsDTO> presentations) {
        lock (_mutex)
            _taxonomyPresentations.AddRange(presentations);
    }

    public IReadOnlyCollection<PresentationDetailsDTO> GetTaxonomyPresentationsByTaxonomyType(int taxonomyTypeId) {
        var results = new List<PresentationDetailsDTO>();
        lock (_mutex) {
            foreach (PresentationDetailsDTO p in _taxonomyPresentations) {
                // PresentationDetailsDTO doesn't have TaxonomyTypeId, so return all
                // In production, the SQL filters by taxonomy_type_id via JOIN
                results.Add(p);
            }
        }
        return results;
    }

    // Data points by submission

    public IReadOnlyCollection<DataPoint> GetDataPointsForSubmission(ulong companyId, ulong submissionId) {
        var results = new List<DataPoint>();
        lock (_mutex) {
            foreach (DataPoint dp in _dataPoints) {
                if (dp.CompanyId == companyId && dp.SubmissionId == submissionId)
                    results.Add(dp);
            }
        }
        return results;
    }

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

    private static readonly Dictionary<FilingType, string> FilingTypeNames = new() {
        { FilingType.TenK, "10-K" },
        { FilingType.TenQ, "10-Q" },
        { FilingType.EightK, "8-K" },
        { FilingType.EightK_A, "8-K/A" },
        { FilingType.TenK_A, "10-K/A" },
        { FilingType.TenQ_A, "10-Q/A" },
        { FilingType.TenKT_A, "10-KT/A" },
        { FilingType.TenQT_A, "10-QT/A" },
        { FilingType.TenKT, "10-KT" },
        { FilingType.TenQT, "10-QT" },
        { FilingType.FortyF, "40-F" },
        { FilingType.FortyF_A, "40-F/A" },
        { FilingType.TwentyF, "20-F" },
        { FilingType.TwentyF_A, "20-F/A" },
        { FilingType.SixK, "6-K" },
        { FilingType.SixK_A, "6-K/A" },
    };

    private static string FilingTypeToName(FilingType filingType) {
        if (FilingTypeNames.TryGetValue(filingType, out string? name))
            return name;
        return filingType.ToString();
    }
}
