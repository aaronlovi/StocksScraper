using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence;

internal sealed class InsertDataPointUnitStmt(DataPointUnit _unit) : NonQueryDbStmtBase(sql, nameof(InsertDataPointUnitStmt))
{
    private const string sql = "INSERT INTO units (unit_id, unit_name)"
        + " VALUES (@unitId, @unitName)";

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [
        new NpgsqlParameter<long>("unitId", (long)_unit.UnitId),
        new NpgsqlParameter<string>("unitName", _unit.UnitName)];
}
