using System;
using System.Collections.Generic;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.DataModels.Scoring;
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

    public PriceRow? GetPriceNearDate(string ticker, DateOnly targetDate) {
        if (string.IsNullOrWhiteSpace(ticker))
            return null;
        string normalized = ticker.Trim().ToUpperInvariant();
        PriceRow? best = null;
        lock (_mutex) {
            foreach (PriceRow price in _prices) {
                if (!string.Equals(price.Ticker, normalized, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (price.PriceDate > targetDate)
                    continue;
                if (best is null || price.PriceDate > best.PriceDate)
                    best = price;
            }
        }
        return best;
    }

    public PriceRow? GetLatestPriceByTicker(string ticker) {
        if (string.IsNullOrWhiteSpace(ticker))
            return null;
        string normalized = ticker.Trim().ToUpperInvariant();
        PriceRow? best = null;
        lock (_mutex) {
            foreach (PriceRow price in _prices) {
                if (!string.Equals(price.Ticker, normalized, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (best is null || price.PriceDate > best.PriceDate)
                    best = price;
            }
        }
        return best;
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

    // Scoring data points

    public IReadOnlyCollection<ScoringConceptValue> GetScoringDataPoints(ulong companyId, string[] conceptNames, int yearLimit = 5) {
        var conceptSet = new HashSet<string>(conceptNames, StringComparer.Ordinal);
        var results = new List<ScoringConceptValue>();

        lock (_mutex) {
            // Build a lookup of submission_id → Submission for 10-K and 10-Q filings of this company
            var eligibleSubmissions = new Dictionary<ulong, Submission>();
            foreach (Submission s in _submissions) {
                if (s.CompanyId == companyId && (s.FilingType == FilingType.TenK || s.FilingType == FilingType.TenQ))
                    eligibleSubmissions[s.SubmissionId] = s;
            }

            // Build taxonomy concept id → (name, balanceTypeId) lookup
            var conceptIdToInfo = new Dictionary<long, (string Name, int BalanceTypeId)>();
            foreach (ConceptDetailsDTO c in _taxonomyConcepts) {
                if (conceptSet.Contains(c.Name))
                    conceptIdToInfo[c.ConceptId] = (c.Name, c.BalanceTypeId);
            }

            // Find the 5 most recent distinct 10-K report dates that have matching data points
            var tenKDatesWithData = new HashSet<DateOnly>();
            // Find the most recent report date across all eligible filing types
            DateOnly latestAnyDate = DateOnly.MinValue;
            foreach (DataPoint dp in _dataPoints) {
                if (dp.CompanyId != companyId)
                    continue;
                if (!eligibleSubmissions.TryGetValue(dp.SubmissionId, out Submission? sub))
                    continue;
                if (!conceptIdToInfo.ContainsKey(dp.TaxonomyConceptId))
                    continue;
                if (sub.FilingType == FilingType.TenK)
                    tenKDatesWithData.Add(sub.ReportDate);
                if (sub.ReportDate > latestAnyDate)
                    latestAnyDate = sub.ReportDate;
            }

            var sortedDates = new SortedSet<DateOnly>(tenKDatesWithData);
            var topDates = new HashSet<DateOnly>();
            int count = 0;
            foreach (DateOnly date in sortedDates.Reverse()) {
                topDates.Add(date);
                count++;
                if (count >= yearLimit)
                    break;
            }

            // Add the latest date across all types (may already be in topDates)
            if (latestAnyDate != DateOnly.MinValue)
                topDates.Add(latestAnyDate);

            // Collect candidate data points, keyed by (submissionId, conceptName)
            // Keep only the one with the max end_date per key (DISTINCT ON equivalent)
            var bestByKey = new Dictionary<(ulong submissionId, string conceptName), (DateOnly endDate, decimal value, DateOnly reportDate, int balanceTypeId, int filingTypeId)>();

            foreach (DataPoint dp in _dataPoints) {
                if (dp.CompanyId != companyId)
                    continue;

                if (!eligibleSubmissions.TryGetValue(dp.SubmissionId, out Submission? submission))
                    continue;

                if (!topDates.Contains(submission.ReportDate))
                    continue;

                if (!conceptIdToInfo.TryGetValue(dp.TaxonomyConceptId, out var conceptInfo))
                    continue;

                var key = (dp.SubmissionId, conceptInfo.Name);
                if (!bestByKey.TryGetValue(key, out var existing) || dp.DatePair.EndDate > existing.endDate)
                    bestByKey[key] = (dp.DatePair.EndDate, dp.Value, submission.ReportDate, conceptInfo.BalanceTypeId, (int)submission.FilingType);
            }

            foreach (var entry in bestByKey)
                results.Add(new ScoringConceptValue(entry.Key.conceptName, entry.Value.value, entry.Value.reportDate, entry.Value.balanceTypeId, entry.Value.filingTypeId));
        }

        // Sort by report_date DESC, then concept name
        results.Sort((a, b) => {
            int dateCompare = b.ReportDate.CompareTo(a.ReportDate);
            if (dateCompare != 0)
                return dateCompare;
            return string.Compare(a.ConceptName, b.ConceptName, StringComparison.Ordinal);
        });

        return results;
    }

    // Batch scoring data points (all companies)

    public IReadOnlyCollection<BatchScoringConceptValue> GetAllScoringDataPoints(string[] conceptNames, int yearLimit = 5) {
        var conceptSet = new HashSet<string>(conceptNames, StringComparer.Ordinal);
        var results = new List<BatchScoringConceptValue>();

        lock (_mutex) {
            // Build a lookup of submission_id → Submission for 10-K and 10-Q filings
            var eligibleSubmissions = new Dictionary<ulong, Submission>();
            foreach (Submission s in _submissions) {
                if (s.FilingType == FilingType.TenK || s.FilingType == FilingType.TenQ)
                    eligibleSubmissions[s.SubmissionId] = s;
            }

            // Build taxonomy concept id → (name, balanceTypeId) lookup
            var conceptIdToInfo = new Dictionary<long, (string Name, int BalanceTypeId)>();
            foreach (ConceptDetailsDTO c in _taxonomyConcepts) {
                if (conceptSet.Contains(c.Name))
                    conceptIdToInfo[c.ConceptId] = (c.Name, c.BalanceTypeId);
            }

            // Find the 5 most recent distinct 10-K report dates per company that have matching data points
            var tenKDatesByCompany = new Dictionary<ulong, HashSet<DateOnly>>();
            // Track the latest report date across all eligible types per company
            var latestAnyDateByCompany = new Dictionary<ulong, DateOnly>();
            foreach (DataPoint dp in _dataPoints) {
                if (!eligibleSubmissions.TryGetValue(dp.SubmissionId, out Submission? sub))
                    continue;
                if (sub.CompanyId != dp.CompanyId)
                    continue;
                if (!conceptIdToInfo.ContainsKey(dp.TaxonomyConceptId))
                    continue;
                if (sub.FilingType == FilingType.TenK) {
                    if (!tenKDatesByCompany.TryGetValue(dp.CompanyId, out HashSet<DateOnly>? dates)) {
                        dates = new HashSet<DateOnly>();
                        tenKDatesByCompany[dp.CompanyId] = dates;
                    }
                    dates.Add(sub.ReportDate);
                }
                if (!latestAnyDateByCompany.TryGetValue(dp.CompanyId, out DateOnly existingLatest)
                    || sub.ReportDate > existingLatest)
                    latestAnyDateByCompany[dp.CompanyId] = sub.ReportDate;
            }

            // Build eligible dates per company: N most recent 10-K dates UNION latest any date
            var topDatesByCompany = new Dictionary<ulong, HashSet<DateOnly>>();
            foreach (var entry in tenKDatesByCompany) {
                ulong companyId = entry.Key;
                HashSet<DateOnly> allDates = entry.Value;
                var sorted = new SortedSet<DateOnly>(allDates);
                var top = new HashSet<DateOnly>();
                int count = 0;
                foreach (DateOnly date in sorted.Reverse()) {
                    top.Add(date);
                    count++;
                    if (count >= yearLimit)
                        break;
                }
                topDatesByCompany[companyId] = top;
            }
            // Add latest any-type date per company
            foreach (var entry in latestAnyDateByCompany) {
                if (!topDatesByCompany.TryGetValue(entry.Key, out HashSet<DateOnly>? top)) {
                    top = new HashSet<DateOnly>();
                    topDatesByCompany[entry.Key] = top;
                }
                top.Add(entry.Value);
            }

            // Collect candidate data points, keyed by (companyId, submissionId, conceptName)
            // Keep only the one with the max end_date per key (DISTINCT ON equivalent)
            var bestByKey = new Dictionary<(ulong companyId, ulong submissionId, string conceptName),
                (DateOnly endDate, decimal value, DateOnly reportDate, int balanceTypeId, int filingTypeId)>();

            foreach (DataPoint dp in _dataPoints) {
                if (!eligibleSubmissions.TryGetValue(dp.SubmissionId, out Submission? submission))
                    continue;
                if (submission.CompanyId != dp.CompanyId)
                    continue;
                if (!topDatesByCompany.TryGetValue(dp.CompanyId, out HashSet<DateOnly>? topDates))
                    continue;
                if (!topDates.Contains(submission.ReportDate))
                    continue;
                if (!conceptIdToInfo.TryGetValue(dp.TaxonomyConceptId, out var conceptInfo))
                    continue;

                var key = (dp.CompanyId, dp.SubmissionId, conceptInfo.Name);
                if (!bestByKey.TryGetValue(key, out var existing) || dp.DatePair.EndDate > existing.endDate)
                    bestByKey[key] = (dp.DatePair.EndDate, dp.Value, submission.ReportDate, conceptInfo.BalanceTypeId, (int)submission.FilingType);
            }

            foreach (var entry in bestByKey) {
                results.Add(new BatchScoringConceptValue(
                    entry.Key.companyId,
                    entry.Key.conceptName,
                    entry.Value.value,
                    entry.Value.reportDate,
                    entry.Value.balanceTypeId,
                    entry.Value.filingTypeId));
            }
        }

        return results;
    }

    // Latest prices (one per ticker)

    public IReadOnlyCollection<LatestPrice> GetAllLatestPrices() {
        var results = new List<LatestPrice>();
        lock (_mutex) {
            var latestByTicker = new Dictionary<string, (decimal close, DateOnly date)>(StringComparer.OrdinalIgnoreCase);
            foreach (PriceRow price in _prices) {
                if (!latestByTicker.TryGetValue(price.Ticker, out var existing) || price.PriceDate > existing.date)
                    latestByTicker[price.Ticker] = (price.Close, price.PriceDate);
            }
            foreach (var entry in latestByTicker)
                results.Add(new LatestPrice(entry.Key, entry.Value.close, entry.Value.date));
        }
        return results;
    }

    public IReadOnlyCollection<LatestPrice> GetAllPricesNearDate(DateOnly targetDate) {
        var results = new List<LatestPrice>();
        lock (_mutex) {
            var nearestByTicker = new Dictionary<string, LatestPrice>(StringComparer.OrdinalIgnoreCase);
            foreach (PriceRow price in _prices) {
                if (price.PriceDate > targetDate)
                    continue;
                if (!nearestByTicker.TryGetValue(price.Ticker, out LatestPrice? existing) || price.PriceDate > existing.PriceDate)
                    nearestByTicker[price.Ticker] = new LatestPrice(price.Ticker, price.Close, price.PriceDate);
            }
            foreach (var entry in nearestByTicker)
                results.Add(entry.Value);
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

    // Company scores

    private readonly List<CompanyScoreSummary> _companyScores = [];

    public void TruncateCompanyScores() {
        lock (_mutex)
            _companyScores.Clear();
    }

    public void AddCompanyScores(IReadOnlyCollection<CompanyScoreSummary> scores) {
        lock (_mutex)
            _companyScores.AddRange(scores);
    }

    public IReadOnlyCollection<CompanyScoreSummary> GetCompanyScores() {
        lock (_mutex)
            return [.. _companyScores];
    }

    public PagedResults<CompanyScoreSummary> GetCompanyScoresPaged(
        PaginationRequest pagination, ScoresSortBy sortBy, SortDirection sortDir,
        ScoresFilter? filter) {
        lock (_mutex) {
            // Filter
            var filtered = new List<CompanyScoreSummary>();
            foreach (CompanyScoreSummary s in _companyScores) {
                if (filter is not null) {
                    if (filter.MinScore.HasValue && s.OverallScore < filter.MinScore.Value)
                        continue;
                    if (filter.MaxScore.HasValue && s.OverallScore > filter.MaxScore.Value)
                        continue;
                    if (!string.IsNullOrWhiteSpace(filter.Exchange)
                        && !string.Equals(s.Exchange, filter.Exchange, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                filtered.Add(s);
            }

            // Sort
            filtered.Sort((a, b) => {
                int cmp = CompareByField(a, b, sortBy);
                if (sortDir == SortDirection.Descending)
                    cmp = -cmp;
                if (cmp != 0)
                    return cmp;
                return a.CompanyId.CompareTo(b.CompanyId);
            });

            // Paginate
            uint totalItems = (uint)filtered.Count;
            int offset = (int)((pagination.PageNumber - 1) * pagination.PageSize);
            int limit = (int)pagination.PageSize;

            var page = new List<CompanyScoreSummary>();
            for (int i = offset; i < filtered.Count && page.Count < limit; i++)
                page.Add(filtered[i]);

            uint totalPages = totalItems == 0 ? 0 : (uint)Math.Ceiling(totalItems / (double)pagination.PageSize);
            var paginationResponse = new PaginationResponse(pagination.PageNumber, totalItems, totalPages);
            return new PagedResults<CompanyScoreSummary>(page, paginationResponse);
        }
    }

    // Company moat scores

    private readonly List<CompanyMoatScoreSummary> _companyMoatScores = [];

    public void TruncateCompanyMoatScores() {
        lock (_mutex)
            _companyMoatScores.Clear();
    }

    public void AddCompanyMoatScores(IReadOnlyCollection<CompanyMoatScoreSummary> scores) {
        lock (_mutex)
            _companyMoatScores.AddRange(scores);
    }

    public IReadOnlyCollection<CompanyMoatScoreSummary> GetCompanyMoatScores() {
        lock (_mutex)
            return [.. _companyMoatScores];
    }

    public PagedResults<CompanyMoatScoreSummary> GetCompanyMoatScoresPaged(
        PaginationRequest pagination, MoatScoresSortBy sortBy, SortDirection sortDir,
        ScoresFilter? filter) {
        lock (_mutex) {
            var filtered = new List<CompanyMoatScoreSummary>();
            foreach (CompanyMoatScoreSummary s in _companyMoatScores) {
                if (filter is not null) {
                    if (filter.MinScore.HasValue && s.OverallScore < filter.MinScore.Value)
                        continue;
                    if (filter.MaxScore.HasValue && s.OverallScore > filter.MaxScore.Value)
                        continue;
                    if (!string.IsNullOrWhiteSpace(filter.Exchange)
                        && !string.Equals(s.Exchange, filter.Exchange, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                filtered.Add(s);
            }

            filtered.Sort((a, b) => {
                int cmp = CompareByMoatField(a, b, sortBy);
                if (sortDir == SortDirection.Descending)
                    cmp = -cmp;
                if (cmp != 0)
                    return cmp;
                return a.CompanyId.CompareTo(b.CompanyId);
            });

            uint totalItems = (uint)filtered.Count;
            int offset = (int)((pagination.PageNumber - 1) * pagination.PageSize);
            int limit = (int)pagination.PageSize;

            var page = new List<CompanyMoatScoreSummary>();
            for (int i = offset; i < filtered.Count && page.Count < limit; i++)
                page.Add(filtered[i]);

            uint totalPages = totalItems == 0 ? 0 : (uint)Math.Ceiling(totalItems / (double)pagination.PageSize);
            var paginationResponse = new PaginationResponse(pagination.PageNumber, totalItems, totalPages);
            return new PagedResults<CompanyMoatScoreSummary>(page, paginationResponse);
        }
    }

    private static int CompareByMoatField(CompanyMoatScoreSummary a, CompanyMoatScoreSummary b, MoatScoresSortBy sortBy) {
        return sortBy switch {
            MoatScoresSortBy.AverageGrossMargin => CompareNullable(a.AverageGrossMargin, b.AverageGrossMargin),
            MoatScoresSortBy.AverageOperatingMargin => CompareNullable(a.AverageOperatingMargin, b.AverageOperatingMargin),
            MoatScoresSortBy.AverageRoeCF => CompareNullable(a.AverageRoeCF, b.AverageRoeCF),
            MoatScoresSortBy.AverageRoeOE => CompareNullable(a.AverageRoeOE, b.AverageRoeOE),
            MoatScoresSortBy.EstimatedReturnOE => CompareNullable(a.EstimatedReturnOE, b.EstimatedReturnOE),
            MoatScoresSortBy.RevenueCagr => CompareNullable(a.RevenueCagr, b.RevenueCagr),
            MoatScoresSortBy.CapexRatio => CompareNullable(a.CapexRatio, b.CapexRatio),
            MoatScoresSortBy.InterestCoverage => CompareNullable(a.InterestCoverage, b.InterestCoverage),
            MoatScoresSortBy.DebtToEquityRatio => CompareNullable(a.DebtToEquityRatio, b.DebtToEquityRatio),
            MoatScoresSortBy.Return1y => CompareNullable(a.Return1y, b.Return1y),
            _ => a.OverallScore.CompareTo(b.OverallScore),
        };
    }

    private static int CompareByField(CompanyScoreSummary a, CompanyScoreSummary b, ScoresSortBy sortBy) {
        return sortBy switch {
            ScoresSortBy.BookValue => CompareNullable(a.BookValue, b.BookValue),
            ScoresSortBy.MarketCap => CompareNullable(a.MarketCap, b.MarketCap),
            ScoresSortBy.EstimatedReturnCF => CompareNullable(a.EstimatedReturnCF, b.EstimatedReturnCF),
            ScoresSortBy.EstimatedReturnOE => CompareNullable(a.EstimatedReturnOE, b.EstimatedReturnOE),
            ScoresSortBy.DebtToEquityRatio => CompareNullable(a.DebtToEquityRatio, b.DebtToEquityRatio),
            ScoresSortBy.PriceToBookRatio => CompareNullable(a.PriceToBookRatio, b.PriceToBookRatio),
            ScoresSortBy.Return1y => CompareNullable(a.Return1y, b.Return1y),
            _ => a.OverallScore.CompareTo(b.OverallScore),
        };
    }

    private static int CompareNullable(decimal? a, decimal? b) {
        if (!a.HasValue && !b.HasValue) return 0;
        if (!a.HasValue) return -1;
        if (!b.HasValue) return 1;
        return a.Value.CompareTo(b.Value);
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

    private static string FilingTypeToName(FilingType filingType) => filingType.ToDisplayName();
}
