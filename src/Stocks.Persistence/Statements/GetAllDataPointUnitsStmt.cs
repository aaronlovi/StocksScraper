using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence;

internal sealed class GetAllDataPointUnitsStmt : QueryDbStmtBase
{
    private const string sql = "SELECT unit_id, unit_name FROM units";

    private readonly List<DataPointUnit> _units;

    private static int _unitIdIndex = -1;
    private static int _unitNameIndex = -1;

    public GetAllDataPointUnitsStmt()
        : base(sql, nameof(GetAllDataPointUnitsStmt))
        => _units = [];

    public IReadOnlyCollection<DataPointUnit> Units => _units;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader)
    {
        base.BeforeRowProcessing(reader);

        if (_unitIdIndex != -1) return;

        _unitIdIndex = reader.GetOrdinal("unit_id");
        _unitNameIndex = reader.GetOrdinal("unit_name");
    }

    protected override void ClearResults() => _units.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader)
    {
        _units.Add(new DataPointUnit((ulong)reader.GetInt64(_unitIdIndex), reader.GetString(_unitNameIndex)));
        return true;
    }
}
