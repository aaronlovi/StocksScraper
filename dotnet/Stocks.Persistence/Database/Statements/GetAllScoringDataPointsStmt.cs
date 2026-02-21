using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels.Scoring;

namespace Stocks.Persistence.Database.Statements;

internal class GetAllScoringDataPointsStmt : QueryDbStmtBase {
    private const string Sql = @"
WITH annual_ranked_dates AS (
    SELECT s.company_id, s.report_date,
           ROW_NUMBER() OVER (PARTITION BY s.company_id ORDER BY s.report_date DESC) AS rn
    FROM submissions s
    WHERE s.filing_type = 1
      AND EXISTS (
          SELECT 1 FROM data_points dp2
          JOIN taxonomy_concepts tc2 ON dp2.taxonomy_concept_id = tc2.taxonomy_concept_id
          WHERE dp2.submission_id = s.submission_id AND dp2.company_id = s.company_id
            AND tc2.name = ANY(@concept_names)
      )
    GROUP BY s.company_id, s.report_date
),
latest_any_date AS (
    SELECT DISTINCT ON (s.company_id)
        s.company_id, s.report_date
    FROM submissions s
    WHERE s.filing_type IN (1, 2)
      AND EXISTS (
          SELECT 1 FROM data_points dp2
          JOIN taxonomy_concepts tc2 ON dp2.taxonomy_concept_id = tc2.taxonomy_concept_id
          WHERE dp2.submission_id = s.submission_id AND dp2.company_id = s.company_id
            AND tc2.name = ANY(@concept_names)
      )
    ORDER BY s.company_id, s.report_date DESC
),
eligible_dates AS (
    SELECT company_id, report_date FROM annual_ranked_dates WHERE rn <= 5
    UNION
    SELECT company_id, report_date FROM latest_any_date
)
SELECT DISTINCT ON (dp.company_id, s.submission_id, tc.name)
    dp.company_id, tc.name AS concept_name, dp.value, s.report_date,
    tc.taxonomy_balance_type_id AS balance_type_id,
    s.filing_type
FROM data_points dp
JOIN taxonomy_concepts tc ON dp.taxonomy_concept_id = tc.taxonomy_concept_id
JOIN submissions s ON dp.submission_id = s.submission_id AND dp.company_id = s.company_id
JOIN eligible_dates ed ON ed.company_id = dp.company_id AND ed.report_date = s.report_date
WHERE s.filing_type IN (1, 2)
  AND tc.name = ANY(@concept_names)
ORDER BY dp.company_id, s.submission_id, tc.name, dp.end_date DESC, dp.start_date ASC";

    private readonly string[] _conceptNames;
    private readonly List<BatchScoringConceptValue> _results = [];

    private int _companyIdIndex = -1;
    private int _conceptNameIndex = -1;
    private int _valueIndex = -1;
    private int _reportDateIndex = -1;
    private int _balanceTypeIdIndex = -1;
    private int _filingTypeIndex = -1;

    public GetAllScoringDataPointsStmt(string[] conceptNames)
        : base(Sql, nameof(GetAllScoringDataPointsStmt)) {
        _conceptNames = conceptNames;
    }

    public IReadOnlyCollection<BatchScoringConceptValue> Results => _results;

    protected override int CommandTimeoutSeconds => 600;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        _companyIdIndex = reader.GetOrdinal("company_id");
        _conceptNameIndex = reader.GetOrdinal("concept_name");
        _valueIndex = reader.GetOrdinal("value");
        _reportDateIndex = reader.GetOrdinal("report_date");
        _balanceTypeIdIndex = reader.GetOrdinal("balance_type_id");
        _filingTypeIndex = reader.GetOrdinal("filing_type");
    }

    protected override void ClearResults() => _results.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [
            new NpgsqlParameter("concept_names", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = _conceptNames }
        ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var value = new BatchScoringConceptValue(
            unchecked((ulong)reader.GetInt64(_companyIdIndex)),
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
