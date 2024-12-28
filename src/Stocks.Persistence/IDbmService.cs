using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataModels;
using Utilities;

namespace Stocks.Persistence;

public interface IDbmService
{
    // Id generator

    ValueTask<ulong> GetNextId64(CancellationToken ct);
    ValueTask<ulong> GetIdRange64(uint count, CancellationToken ct);

    // Companies

    Task<Results> EmptyCompaniesTables(CancellationToken ct);
    Task<Results> SaveCompaniesBatch(List<Company> companies, CancellationToken ct);
    Task<Results> SaveCompanyNamesBatch(List<CompanyName> companyNames, CancellationToken ct);
}
