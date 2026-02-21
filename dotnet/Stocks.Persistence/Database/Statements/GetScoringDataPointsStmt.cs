using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels.Scoring;

namespace Stocks.Persistence.Database.Statements;

internal class GetScoringDataPointsStmt : QueryDbStmtBase {
    private const string Sql = @"
SELECT concept_name, value, report_date, balance_type_id, filing_type
FROM (
    SELECT DISTINCT ON (s.submission_id, tc.name)
        tc.name AS concept_name, dp.value, s.report_date,
        tc.taxonomy_balance_type_id AS balance_type_id,
        s.filing_type
    FROM data_points dp
    JOIN taxonomy_concepts tc ON dp.taxonomy_concept_id = tc.taxonomy_concept_id
    JOIN submissions s ON dp.submission_id = s.submission_id AND dp.company_id = s.company_id
    WHERE dp.company_id = @company_id
      AND s.filing_type IN (1, 2)
      AND tc.name = ANY(@concept_names)
      AND s.report_date IN (
        SELECT report_date FROM (
            SELECT DISTINCT s2.report_date
            FROM submissions s2
            WHERE s2.company_id = @company_id AND s2.filing_type = 1
              AND EXISTS (
                SELECT 1 FROM data_points dp2
                JOIN taxonomy_concepts tc2 ON dp2.taxonomy_concept_id = tc2.taxonomy_concept_id
                WHERE dp2.submission_id = s2.submission_id AND dp2.company_id = s2.company_id
                  AND tc2.name = ANY(@concept_names)
              )
            ORDER BY s2.report_date DESC
            LIMIT @year_limit
        ) annual_dates
        UNION
        SELECT report_date FROM (
            SELECT s3.report_date
            FROM submissions s3
            WHERE s3.company_id = @company_id AND s3.filing_type IN (1, 2)
              AND EXISTS (
                SELECT 1 FROM data_points dp3
                JOIN taxonomy_concepts tc3 ON dp3.taxonomy_concept_id = tc3.taxonomy_concept_id
                WHERE dp3.submission_id = s3.submission_id AND dp3.company_id = s3.company_id
                  AND tc3.name = ANY(@concept_names)
              )
            ORDER BY s3.report_date DESC
            LIMIT 1
        ) latest_date
      )
    ORDER BY s.submission_id, tc.name, dp.end_date DESC, dp.start_date ASC
) sub
ORDER BY report_date DESC, concept_name";

    private readonly ulong _companyId;
    private readonly string[] _conceptNames;
    private readonly int _yearLimit;
    private readonly List<ScoringConceptValue> _results = [];

    private static int _conceptNameIndex = -1;
    private static int _valueIndex = -1;
    private static int _reportDateIndex = -1;
    private static int _balanceTypeIdIndex = -1;
    private static int _filingTypeIndex = -1;

    public GetScoringDataPointsStmt(ulong companyId, string[] conceptNames)
        : this(companyId, conceptNames, 5) { }

    public GetScoringDataPointsStmt(ulong companyId, string[] conceptNames, int yearLimit)
        : base(Sql, nameof(GetScoringDataPointsStmt)) {
        _companyId = companyId;
        _conceptNames = conceptNames;
        _yearLimit = yearLimit;
    }

    public IReadOnlyCollection<ScoringConceptValue> Results => _results;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        if (_conceptNameIndex != -1)
            return;
        _conceptNameIndex = reader.GetOrdinal("concept_name");
        _valueIndex = reader.GetOrdinal("value");
        _reportDateIndex = reader.GetOrdinal("report_date");
        _balanceTypeIdIndex = reader.GetOrdinal("balance_type_id");
        _filingTypeIndex = reader.GetOrdinal("filing_type");
    }

    protected override void ClearResults() => _results.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [
            new NpgsqlParameter<long>("company_id", unchecked((long)_companyId)),
            new NpgsqlParameter("concept_names", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = _conceptNames },
            new NpgsqlParameter<int>("year_limit", _yearLimit)
        ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var value = new ScoringConceptValue(
            reader.GetString(_conceptNameIndex),
            reader.GetDecimal(_valueIndex),
            DateOnly.FromDateTime(reader.GetDateTime(_reportDateIndex)),
            reader.GetInt32(_balanceTypeIdIndex),
            reader.GetInt32(_filingTypeIndex)
        );
        _results.Add(value);
        return true;
    }
}
