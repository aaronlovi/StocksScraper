using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.Shared;

namespace Stocks.Persistence;

public interface IDbmService
{
    // Utilities

    Task<Results> DropAllTables(CancellationToken ct);

    // Id generator

    ValueTask<ulong> GetNextId64(CancellationToken ct);
    ValueTask<ulong> GetIdRange64(uint count, CancellationToken ct);

    // Companies

    Task<GenericResults<IReadOnlyCollection<Company>>> GetCompaniesByDataSource(string dataSource, CancellationToken ct);
    Task<Results> EmptyCompaniesTables(CancellationToken ct);
    Task<Results> BulkInsertCompanies(List<Company> companies, CancellationToken ct);
    Task<Results> BulkInsertCompanyNames(List<CompanyName> companyNames, CancellationToken ct);

    // Data points and data point units

    Task<GenericResults<IReadOnlyCollection<DataPointUnit>>> GetDataPointUnits(CancellationToken ct);
    Task<Results> InsertDataPointUnit(DataPointUnit dataPointUnit, CancellationToken ct);
    Task<Results> BulkInsertDataPoints(List<DataPoint> dataPoints, CancellationToken ct);

    // Company submissions

    Task<GenericResults<IReadOnlyCollection<Submission>>> GetSubmissions(CancellationToken ct);
    Task<Results> BulkInsertSubmissions(List<Submission> batch, CancellationToken none);
}
