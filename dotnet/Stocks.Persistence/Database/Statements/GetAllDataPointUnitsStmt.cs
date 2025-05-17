using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetAllDataPointUnitsStmt : QueryDbStmtBase {
    private const string sql = "SELECT data_point_unit_id, data_point_unit_name FROM data_point_units";

    private readonly List<DataPointUnit> _dataPointUnits;

    private static int _dataPointUnitIdIndex = -1;
    private static int _dataPointUnitNameIndex = -1;

    public GetAllDataPointUnitsStmt()
        : base(sql, nameof(GetAllDataPointUnitsStmt)) {
        _dataPointUnits = [];
    }

    public IReadOnlyCollection<DataPointUnit> Units => _dataPointUnits;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);

        if (_dataPointUnitIdIndex != -1)
            return;

        _dataPointUnitIdIndex = reader.GetOrdinal("data_point_unit_id");
        _dataPointUnitNameIndex = reader.GetOrdinal("data_point_unit_name");
    }

    protected override void ClearResults() => _dataPointUnits.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var dataPointUnit = new DataPointUnit(
            (ulong)reader.GetInt64(_dataPointUnitIdIndex),
            reader.GetString(_dataPointUnitNameIndex));
        _dataPointUnits.Add(dataPointUnit);
        return true;
    }
}
