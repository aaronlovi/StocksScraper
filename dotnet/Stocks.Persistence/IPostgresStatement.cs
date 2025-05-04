using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Stocks.Persistence;

public interface IPostgresStatement
{
    Task<DbStmtResult> Execute(NpgsqlConnection conn, CancellationToken ct);
}
