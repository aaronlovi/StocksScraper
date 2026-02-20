using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class UpsertDataPointsBatchStmt : NonQueryBatchedDbStmtBase {
    private const string sql = @"
INSERT INTO data_points (data_point_id, company_id, unit_id, fact_name,
    start_date, end_date, value, filed_date, submission_id, taxonomy_concept_id)
VALUES (@data_point_id, @company_id, @unit_id, @fact_name,
    @start_date, @end_date, @value, @filed_date, @submission_id, @taxonomy_concept_id)
ON CONFLICT (company_id, fact_name, unit_id, start_date, end_date, submission_id)
DO UPDATE SET value = EXCLUDED.value, filed_date = EXCLUDED.filed_date,
    taxonomy_concept_id = EXCLUDED.taxonomy_concept_id;
";

    public UpsertDataPointsBatchStmt(IReadOnlyCollection<DataPoint> dataPoints)
        : base(nameof(UpsertDataPointsBatchStmt)) {
        foreach (DataPoint dp in dataPoints) {
            AddCommandToBatch(sql, [
                new NpgsqlParameter<long>("data_point_id", (long)dp.DataPointId),
                new NpgsqlParameter<long>("company_id", (long)dp.CompanyId),
                new NpgsqlParameter<long>("unit_id", (long)dp.Units.UnitId),
                new NpgsqlParameter<string>("fact_name", dp.FactName),
                new NpgsqlParameter<DateTime>("start_date", dp.DatePair.StartTimeUtc) { NpgsqlDbType = NpgsqlDbType.Date },
                new NpgsqlParameter<DateTime>("end_date", dp.DatePair.EndTimeUtc) { NpgsqlDbType = NpgsqlDbType.Date },
                new NpgsqlParameter<decimal>("value", dp.Value),
                new NpgsqlParameter<DateTime>("filed_date", dp.FiledTimeUtc) { NpgsqlDbType = NpgsqlDbType.Date },
                new NpgsqlParameter<long>("submission_id", (long)dp.SubmissionId),
                new NpgsqlParameter<long>("taxonomy_concept_id", dp.TaxonomyConceptId)
            ]);
        }
    }
}
