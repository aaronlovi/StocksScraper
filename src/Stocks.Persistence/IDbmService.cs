using System.Threading;
using System.Threading.Tasks;

namespace Stocks.Persistence;

public interface IDbmService
{
    // id generator
    ValueTask<ulong> GetNextId64(CancellationToken ct);
    ValueTask<ulong> GetIdRange64(uint count, CancellationToken ct);
}
