using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Stocks.Persistence.DistributedCaching;

public interface IDistributedCacheStmt {
    Task<CacheStmtResult> Execute(IDistributedCache cache, CancellationToken ct);
}

public interface IWritingDistributedCacheStmt : IDistributedCacheStmt { }
