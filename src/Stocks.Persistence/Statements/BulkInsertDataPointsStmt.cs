#pragma warning disable IDE0290 // Use primary constructor

using System.Collections.Generic;
using System.Threading.Tasks;
using Elastic.Channels.Buffers;
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
        + " (data_point_id, company_id, unit_id, fact_name, start_date, end_date, value, filed_date, submission_id)"
        + " FROM STDIN (FORMAT BINARY)";

    protected override async Task WriteItemAsync(NpgsqlBinaryImporter writer, DataPoint dataPoint)
    {
        await writer.WriteAsync((long)dataPoint.DataPointId, NpgsqlDbType.Bigint);
        await writer.WriteAsync((long)dataPoint.CompanyId, NpgsqlDbType.Bigint);
        await writer.WriteAsync((long)dataPoint.Units.UnitId, NpgsqlDbType.Bigint);
        await writer.WriteAsync(dataPoint.FactName, NpgsqlDbType.Varchar);
        await writer.WriteAsync(dataPoint.DatePair.StartTimeUtc, NpgsqlDbType.Date);
        await writer.WriteAsync(dataPoint.DatePair.EndTimeUtc, NpgsqlDbType.Date);
        await writer.WriteAsync(dataPoint.Value, NpgsqlDbType.Numeric);
        await writer.WriteAsync(dataPoint.FiledTimeUtc, NpgsqlDbType.Date);
        await writer.WriteAsync((long)dataPoint.SubmissionId, NpgsqlDbType.Bigint);
    }
}
