using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;

namespace Stocks.Persistence.Database;

public interface IDbmService {
    // Utilities

    Task<Result> DropAllTables(CancellationToken ct);

    // Id generator

    ValueTask<ulong> GetNextId64(CancellationToken ct);
    ValueTask<ulong> GetIdRange64(uint count, CancellationToken ct);

    // Companies

    Task<Result<Company>> GetCompanyById(ulong companyId, CancellationToken ct);
    Task<Result<IReadOnlyCollection<Company>>> GetAllCompaniesByDataSource(
        string dataSource, CancellationToken ct);
    Task<Result<PagedCompanies>> GetPagedCompaniesByDataSource(
        string dataSource, PaginationRequest pagination, CancellationToken ct);
    Task<Result> EmptyCompaniesTables(CancellationToken ct);
    Task<Result> BulkInsertCompanies(List<Company> companies, CancellationToken ct);
    Task<Result> BulkInsertCompanyNames(List<CompanyName> companyNames, CancellationToken ct);
    Task<Result<Company>> GetCompanyByCik(string cik, CancellationToken ct);
    Task<Result<PagedResults<CompanySearchResult>>> SearchCompanies(string query, PaginationRequest pagination, CancellationToken ct);

    // Company names

    Task<Result<IReadOnlyCollection<CompanyName>>> GetAllCompanyNames(CancellationToken ct);
    Task<Result<IReadOnlyCollection<CompanyName>>> GetCompanyNamesByCompanyId(ulong companyId, CancellationToken ct);

    // Company tickers

    Task<Result> BulkInsertCompanyTickers(List<CompanyTicker> tickers, CancellationToken ct);
    Task<Result<IReadOnlyCollection<CompanyTicker>>> GetCompanyTickersByCompanyId(ulong companyId, CancellationToken ct);
    Task<Result<IReadOnlyCollection<CompanyTicker>>> GetAllCompanyTickers(CancellationToken ct);

    // Data points and data point units

    Task<Result<IReadOnlyCollection<DataPointUnit>>> GetDataPointUnits(CancellationToken ct);
    Task<Result> InsertDataPointUnit(DataPointUnit dataPointUnit, CancellationToken ct);
    Task<Result> BulkInsertDataPoints(List<DataPoint> dataPoints, CancellationToken ct);
    Task<Result> BulkInsertTaxonomyConcepts(List<ConceptDetailsDTO> taxonomyConcepts, CancellationToken ct);
    Task<Result<IReadOnlyCollection<ConceptDetailsDTO>>> GetTaxonomyConceptsByTaxonomyType(
        int taxonomyTypeId, CancellationToken ct);
    Task<Result> BulkInsertTaxonomyPresentations(List<PresentationDetailsDTO> taxonomyPresentations, CancellationToken ct);

    // Taxonomy presentation hierarchy
    Task<Result<IReadOnlyCollection<PresentationDetailsDTO>>> GetTaxonomyPresentationsByTaxonomyType(int taxonomyTypeId, CancellationToken ct);

    // Data points for a company and submission
    Task<Result<IReadOnlyCollection<DataPoint>>> GetDataPointsForSubmission(ulong companyId, ulong submissionId, CancellationToken ct);

    // Dashboard
    Task<Result<DashboardStats>> GetDashboardStats(CancellationToken ct);

    // Company submissions

    Task<Result<IReadOnlyCollection<Submission>>> GetSubmissions(CancellationToken ct);
    Task<Result<IReadOnlyCollection<Submission>>> GetSubmissionsByCompanyId(ulong companyId, CancellationToken ct);
    Task<Result> BulkInsertSubmissions(List<Submission> batch, CancellationToken none);

    // Prices

    Task<Result<IReadOnlyCollection<PriceImportStatus>>> GetPriceImportStatuses(CancellationToken ct);
    Task<Result<IReadOnlyCollection<PriceRow>>> GetPricesByTicker(string ticker, CancellationToken ct);
    Task<Result> UpsertPriceImport(PriceImportStatus status, CancellationToken ct);
    Task<Result> DeletePricesForTicker(string ticker, CancellationToken ct);
    Task<Result> BulkInsertPrices(List<PriceRow> prices, CancellationToken ct);

    // Price downloads

    Task<Result<IReadOnlyCollection<PriceDownloadStatus>>> GetPriceDownloadStatuses(CancellationToken ct);
    Task<Result> UpsertPriceDownload(PriceDownloadStatus status, CancellationToken ct);

    // Taxonomy types

    Task<Result<TaxonomyTypeInfo>> GetTaxonomyTypeByNameVersion(string name, int version, CancellationToken ct);
    Task<Result<TaxonomyTypeInfo>> EnsureTaxonomyType(string name, int version, CancellationToken ct);
    Task<Result<int>> GetTaxonomyConceptCountByType(int taxonomyTypeId, CancellationToken ct);
    Task<Result<int>> GetTaxonomyPresentationCountByType(int taxonomyTypeId, CancellationToken ct);
}
