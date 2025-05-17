using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Stocks.Persistence.Database;

public interface IPostgresStatement {
    Task<DbStmtResult> Execute(NpgsqlConnection conn, CancellationToken ct);
}
