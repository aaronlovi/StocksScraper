using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.Persistence.Database;

public sealed class DbmInMemoryService : IDbmService {
    private readonly DbmInMemoryData _data;
    private ulong _nextId;

    public DbmInMemoryService() {
        _data = new DbmInMemoryData();
        _nextId = 1;
    }

    public Task<Result> DropAllTables(CancellationToken ct) => Task.FromResult(Result.Success);

    public ValueTask<ulong> GetNextId64(CancellationToken ct) {
        _nextId++;
        return ValueTask.FromResult(_nextId);
    }

    public ValueTask<ulong> GetIdRange64(uint count, CancellationToken ct) {
        if (count == 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be 0");
        ulong start = _nextId + 1;
        _nextId += count;
        return ValueTask.FromResult(start);
    }

    // Companies

    public Task<Result<Company>> GetCompanyById(ulong companyId, CancellationToken ct) {
        Company? company = _data.GetCompanyById(companyId);
        if (company is null)
            return Task.FromResult(Result<Company>.Failure(ErrorCodes.NotFound, "Company not found"));
        return Task.FromResult(Result<Company>.Success(company));
    }

    public Task<Result> BulkInsertCompanies(List<Company> companies, CancellationToken ct) {
        _data.AddCompanies(companies);
        return Task.FromResult(Result.Success);
    }

    public Task<Result> BulkInsertCompanyNames(List<CompanyName> companyNames, CancellationToken ct) {
        _data.AddCompanyNames(companyNames);
        return Task.FromResult(Result.Success);
    }

    public Task<Result<PagedResults<CompanySearchResult>>> SearchCompanies(string query, PaginationRequest pagination, CancellationToken ct) {
        PagedResults<CompanySearchResult> results = _data.SearchCompanies(query, pagination);
        return Task.FromResult(Result<PagedResults<CompanySearchResult>>.Success(results));
    }

    public Task<Result<Company>> GetCompanyByCik(string cik, CancellationToken ct) {
        if (!ulong.TryParse(cik, out ulong cikValue))
            return Task.FromResult(Result<Company>.Failure(ErrorCodes.NotFound, $"Invalid CIK: {cik}"));
        Company? company = _data.GetCompanyByCik(cikValue);
        if (company is null)
            return Task.FromResult(Result<Company>.Failure(ErrorCodes.NotFound, $"Company not found for CIK: {cik}"));
        return Task.FromResult(Result<Company>.Success(company));
    }

    // Company names

    public Task<Result<IReadOnlyCollection<CompanyName>>> GetAllCompanyNames(CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<CompanyName>>.Success(_data.GetCompanyNames()));

    public Task<Result<IReadOnlyCollection<CompanyName>>> GetCompanyNamesByCompanyId(ulong companyId, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<CompanyName>>.Success(_data.GetCompanyNamesByCompanyId(companyId)));

    // Company tickers

    public Task<Result> BulkInsertCompanyTickers(List<CompanyTicker> tickers, CancellationToken ct) {
        _data.AddOrUpdateCompanyTickers(tickers);
        return Task.FromResult(Result.Success);
    }

    public Task<Result<IReadOnlyCollection<CompanyTicker>>> GetCompanyTickersByCompanyId(ulong companyId, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<CompanyTicker>>.Success(_data.GetCompanyTickersByCompanyId(companyId)));

    public Task<Result<IReadOnlyCollection<CompanyTicker>>> GetAllCompanyTickers(CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<CompanyTicker>>.Success(_data.GetAllCompanyTickers()));

    // Dashboard

    public Task<Result<DashboardStats>> GetDashboardStats(CancellationToken ct) =>
        Task.FromResult(Result<DashboardStats>.Success(_data.GetDashboardStats()));

    // Submissions

    public Task<Result<IReadOnlyCollection<Submission>>> GetSubmissions(CancellationToken ct) => throw new NotSupportedException();

    public Task<Result<IReadOnlyCollection<Submission>>> GetSubmissionsByCompanyId(ulong companyId, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<Submission>>.Success(_data.GetSubmissionsByCompanyId(companyId)));

    public Task<Result> BulkInsertSubmissions(List<Submission> batch, CancellationToken none) {
        _data.AddSubmissions(batch);
        return Task.FromResult(Result.Success);
    }

    // Prices

    public Task<Result<IReadOnlyCollection<PriceImportStatus>>> GetPriceImportStatuses(CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<PriceImportStatus>>.Success(_data.GetPriceImports()));

    public Task<Result<IReadOnlyCollection<PriceRow>>> GetPricesByTicker(string ticker, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<PriceRow>>.Success(_data.GetPricesByTicker(ticker)));

    public Task<Result<IReadOnlyCollection<PriceDownloadStatus>>> GetPriceDownloadStatuses(CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<PriceDownloadStatus>>.Success(_data.GetPriceDownloads()));

    public Task<Result> UpsertPriceImport(PriceImportStatus status, CancellationToken ct) {
        _data.UpsertPriceImport(status);
        return Task.FromResult(Result.Success);
    }

    public Task<Result> DeletePricesForTicker(string ticker, CancellationToken ct) {
        _data.DeletePricesForTicker(ticker);
        return Task.FromResult(Result.Success);
    }

    public Task<Result> BulkInsertPrices(List<PriceRow> prices, CancellationToken ct) {
        _data.AddPrices(prices);
        return Task.FromResult(Result.Success);
    }

    public Task<Result> UpsertPriceDownload(PriceDownloadStatus status, CancellationToken ct) {
        _data.UpsertPriceDownload(status);
        return Task.FromResult(Result.Success);
    }

    // Taxonomy types

    public Task<Result<TaxonomyTypeInfo>> GetTaxonomyTypeByNameVersion(string name, int version, CancellationToken ct) {
        TaxonomyTypeInfo? existing = _data.GetTaxonomyType(name, version);
        if (existing is null)
            return Task.FromResult(Result<TaxonomyTypeInfo>.Failure(ErrorCodes.NotFound, "Taxonomy type not found"));
        return Task.FromResult(Result<TaxonomyTypeInfo>.Success(existing));
    }

    public Task<Result<TaxonomyTypeInfo>> EnsureTaxonomyType(string name, int version, CancellationToken ct) {
        TaxonomyTypeInfo created = _data.AddTaxonomyType(name, version);
        return Task.FromResult(Result<TaxonomyTypeInfo>.Success(created));
    }

    public Task<Result<int>> GetTaxonomyConceptCountByType(int taxonomyTypeId, CancellationToken ct) =>
        Task.FromResult(Result<int>.Success(_data.GetTaxonomyConceptCount(taxonomyTypeId)));

    public Task<Result<int>> GetTaxonomyPresentationCountByType(int taxonomyTypeId, CancellationToken ct) =>
        Task.FromResult(Result<int>.Success(_data.GetTaxonomyPresentationCount(taxonomyTypeId)));

    // Not yet implemented

    public Task<Result<IReadOnlyCollection<Company>>> GetAllCompaniesByDataSource(string dataSource, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<Company>>.Success(_data.GetAllCompaniesByDataSource(dataSource)));
    public Task<Result<PagedCompanies>> GetPagedCompaniesByDataSource(string dataSource, PaginationRequest pagination, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> EmptyCompaniesTables(CancellationToken ct) => throw new NotSupportedException();
    public Task<Result<IReadOnlyCollection<DataPointUnit>>> GetDataPointUnits(CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertDataPointUnit(DataPointUnit dataPointUnit, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> BulkInsertDataPoints(List<DataPoint> dataPoints, CancellationToken ct) {
        _data.AddDataPoints(dataPoints);
        return Task.FromResult(Result.Success);
    }
    public Task<Result> BulkInsertTaxonomyConcepts(List<ConceptDetailsDTO> taxonomyConcepts, CancellationToken ct) {
        _data.AddTaxonomyConcepts(taxonomyConcepts);
        return Task.FromResult(Result.Success);
    }

    public Task<Result<IReadOnlyCollection<ConceptDetailsDTO>>> GetTaxonomyConceptsByTaxonomyType(int taxonomyTypeId, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(_data.GetTaxonomyConceptsByTaxonomyType(taxonomyTypeId)));

    public Task<Result> BulkInsertTaxonomyPresentations(List<PresentationDetailsDTO> taxonomyPresentations, CancellationToken ct) {
        _data.AddTaxonomyPresentations(taxonomyPresentations);
        return Task.FromResult(Result.Success);
    }

    public Task<Result<IReadOnlyCollection<PresentationDetailsDTO>>> GetTaxonomyPresentationsByTaxonomyType(int taxonomyTypeId, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(_data.GetTaxonomyPresentationsByTaxonomyType(taxonomyTypeId)));

    public Task<Result<IReadOnlyCollection<DataPoint>>> GetDataPointsForSubmission(ulong companyId, ulong submissionId, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<DataPoint>>.Success(_data.GetDataPointsForSubmission(companyId, submissionId)));

    public Task<Result<IReadOnlyCollection<ScoringConceptValue>>> GetScoringDataPoints(
        ulong companyId, string[] conceptNames, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyCollection<ScoringConceptValue>>.Success(
            _data.GetScoringDataPoints(companyId, conceptNames)));
}
