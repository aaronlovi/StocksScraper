using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels.Scoring;

namespace Stocks.Persistence.Database.Statements;

internal class GetScoringDataPointsStmt : QueryDbStmtBase {
    private const string Sql = @"
SELECT concept_name, value, report_date
FROM (
    SELECT DISTINCT ON (s.submission_id, tc.name)
        tc.name AS concept_name, dp.value, s.report_date
    FROM data_points dp
    JOIN taxonomy_concepts tc ON dp.taxonomy_concept_id = tc.taxonomy_concept_id
    JOIN submissions s ON dp.submission_id = s.submission_id AND dp.company_id = s.company_id
    WHERE dp.company_id = @company_id
      AND s.filing_type = 1
      AND tc.name = ANY(@concept_names)
      AND s.report_date IN (
        SELECT DISTINCT s2.report_date
        FROM submissions s2
        WHERE s2.company_id = @company_id AND s2.filing_type = 1
        ORDER BY s2.report_date DESC
        LIMIT 5
      )
    ORDER BY s.submission_id, tc.name, dp.end_date DESC
) sub
ORDER BY report_date DESC, concept_name";

    private readonly ulong _companyId;
    private readonly string[] _conceptNames;
    private readonly List<ScoringConceptValue> _results = [];

    private static int _conceptNameIndex = -1;
    private static int _valueIndex = -1;
    private static int _reportDateIndex = -1;

    public GetScoringDataPointsStmt(ulong companyId, string[] conceptNames)
        : base(Sql, nameof(GetScoringDataPointsStmt)) {
        _companyId = companyId;
        _conceptNames = conceptNames;
    }

    public IReadOnlyCollection<ScoringConceptValue> Results => _results;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        if (_conceptNameIndex != -1)
            return;
        _conceptNameIndex = reader.GetOrdinal("concept_name");
        _valueIndex = reader.GetOrdinal("value");
        _reportDateIndex = reader.GetOrdinal("report_date");
    }

    protected override void ClearResults() => _results.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [
            new NpgsqlParameter<long>("company_id", unchecked((long)_companyId)),
            new NpgsqlParameter("concept_names", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = _conceptNames }
        ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var value = new ScoringConceptValue(
            reader.GetString(_conceptNameIndex),
            reader.GetDecimal(_valueIndex),
            DateOnly.FromDateTime(reader.GetDateTime(_reportDateIndex))
        );
        _results.Add(value);
        return true;
    }
}
