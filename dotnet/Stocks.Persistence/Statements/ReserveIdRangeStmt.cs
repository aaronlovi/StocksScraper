using System.Collections.Generic;
using Npgsql;

namespace Stocks.Persistence.Statements;

internal sealed class ReserveIdRangeStmt(long _numIds) : QueryDbStmtBase(sql, nameof(ReserveIdRangeStmt)) {
    private const string sql = "UPDATE generator SET last_reserved = last_reserved + @numToGet RETURNING last_reserved";

    public long LastReserved { get; set; }

    protected override void ClearResults() { }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [new NpgsqlParameter<long>("numToGet", _numIds)];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        LastReserved = reader.GetInt64(0);
        return false;
    }
}
