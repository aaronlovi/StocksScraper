using System;
using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal class GetDataPointsForSubmissionStmt : QueryDbStmtBase {
    private const string Sql = @"""
select data_point_id, company_id, fact_name, filing_reference, start_date, end_date, value, unit_id, unit_name, filed_date, submission_id, taxonomy_concept_id
from data_points
where company_id = @company_id and submission_id = @submission_id
""";

    private readonly ulong _companyId;
    private readonly ulong _submissionId;
    private readonly List<DataPoint> _dataPoints = [];

    private static int _dataPointIdIndex = -1;
    private static int _companyIdIndex = -1;
    private static int _factNameIndex = -1;
    private static int _filingReferenceIndex = -1;
    private static int _startDateIndex = -1;
    private static int _endDateIndex = -1;
    private static int _valueIndex = -1;
    private static int _unitIdIndex = -1;
    private static int _unitNameIndex = -1;
    private static int _filedDateIndex = -1;
    private static int _submissionIdIndex = -1;
    private static int _taxonomyConceptIdIndex = -1;

    public GetDataPointsForSubmissionStmt(ulong companyId, ulong submissionId)
        : base(Sql, nameof(GetDataPointsForSubmissionStmt)) {
        _companyId = companyId;
        _submissionId = submissionId;
    }

    public IReadOnlyCollection<DataPoint> DataPoints => _dataPoints;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        if (_dataPointIdIndex != -1)
            return;
        _dataPointIdIndex = reader.GetOrdinal("data_point_id");
        _companyIdIndex = reader.GetOrdinal("company_id");
        _factNameIndex = reader.GetOrdinal("fact_name");
        _filingReferenceIndex = reader.GetOrdinal("filing_reference");
        _startDateIndex = reader.GetOrdinal("start_date");
        _endDateIndex = reader.GetOrdinal("end_date");
        _valueIndex = reader.GetOrdinal("value");
        _unitIdIndex = reader.GetOrdinal("unit_id");
        _unitNameIndex = reader.GetOrdinal("unit_name");
        _filedDateIndex = reader.GetOrdinal("filed_date");
        _submissionIdIndex = reader.GetOrdinal("submission_id");
        _taxonomyConceptIdIndex = reader.GetOrdinal("taxonomy_concept_id");
    }
    protected override void ClearResults() => _dataPoints.Clear();
    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [new NpgsqlParameter<ulong>("company_id", _companyId), new NpgsqlParameter<ulong>("submission_id", _submissionId)];
    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var datePair = new DatePair(
            DateOnly.FromDateTime(reader.GetDateTime(_startDateIndex)),
            DateOnly.FromDateTime(reader.GetDateTime(_endDateIndex))
        );
        var unit = new DataPointUnit(
            reader.GetFieldValue<ulong>(_unitIdIndex),
            reader.GetString(_unitNameIndex)
        );
        var dp = new DataPoint(
            reader.GetFieldValue<ulong>(_dataPointIdIndex),
            reader.GetFieldValue<ulong>(_companyIdIndex),
            reader.GetString(_factNameIndex),
            reader.GetString(_filingReferenceIndex),
            datePair,
            reader.GetDecimal(_valueIndex),
            unit,
            DateOnly.FromDateTime(reader.GetDateTime(_filedDateIndex)),
            reader.GetFieldValue<ulong>(_submissionIdIndex),
            reader.GetInt64(_taxonomyConceptIdIndex)
        );
        _dataPoints.Add(dp);
        return true;
    }
}
