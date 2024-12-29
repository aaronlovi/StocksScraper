#pragma warning disable IDE0290 // Use primary constructor

using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence;

internal sealed class BulkInsertDataPointsStmt : BulkInsertDbStmtBase<DataPoint>
{
    public BulkInsertDataPointsStmt(IReadOnlyCollection<DataPoint> dataPoints)
        : base(nameof(BulkInsertDataPointsStmt), dataPoints)
    { }

    protected override string GetCopyCommand() => "COPY data_points"
        + " (data_point_id, company_id, unit_id, fact_name, start_date, end_date, value, filed_date)"
        + " FROM STDIN (FORMAT BINARY)";

    protected override void WriteItem(NpgsqlBinaryImporter writer, DataPoint dataPoint)
    {
        writer.Write((long)dataPoint.DataPointId, NpgsqlDbType.Bigint);
        writer.Write((long)dataPoint.CompanyId, NpgsqlDbType.Bigint);
        writer.Write((long)dataPoint.Units.UnitId, NpgsqlDbType.Bigint);
        writer.Write(dataPoint.FactName, NpgsqlDbType.Varchar);
        writer.Write(dataPoint.DatePair.StartTimeUtc, NpgsqlDbType.Date);
        writer.Write(dataPoint.DatePair.EndTimeUtc, NpgsqlDbType.Date);
        writer.Write(dataPoint.Value, NpgsqlDbType.Numeric);
        writer.Write(dataPoint.FiledTimeUtc, NpgsqlDbType.Date);
    }
}
