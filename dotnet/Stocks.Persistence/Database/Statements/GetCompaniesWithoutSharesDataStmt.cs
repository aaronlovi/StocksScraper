using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetCompaniesWithoutSharesDataStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT DISTINCT c.company_id, c.cik, c.data_source
FROM companies c
JOIN submissions s ON c.company_id = s.company_id AND s.filing_type = 1
JOIN price_downloads pd ON c.cik = pd.cik
WHERE NOT EXISTS (
    SELECT 1 FROM data_points dp
    JOIN taxonomy_concepts tc ON dp.taxonomy_concept_id = tc.taxonomy_concept_id
    WHERE dp.company_id = c.company_id
      AND tc.name = ANY(@shares_concepts)
      AND dp.end_date >= @recent_cutoff
)
AND (SELECT COUNT(DISTINCT pd2.ticker) FROM price_downloads pd2 WHERE pd2.cik = c.cik) = 1;
";

    private readonly string[] _sharesConcepts;
    private readonly DateTime _recentCutoff;
    private readonly List<Company> _companies;

    private static int _companyIdIndex = -1;
    private static int _cikIndex = -1;
    private static int _dataSourceIndex = -1;

    public GetCompaniesWithoutSharesDataStmt(string[] sharesConcepts, DateTime recentCutoff)
        : base(sql, nameof(GetCompaniesWithoutSharesDataStmt)) {
        _sharesConcepts = sharesConcepts;
        _recentCutoff = recentCutoff;
        _companies = [];
    }

    public IReadOnlyCollection<Company> Companies => _companies;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);

        if (_companyIdIndex != -1)
            return;

        _companyIdIndex = reader.GetOrdinal("company_id");
        _cikIndex = reader.GetOrdinal("cik");
        _dataSourceIndex = reader.GetOrdinal("data_source");
    }

    protected override void ClearResults() => _companies.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [
        new NpgsqlParameter("shares_concepts", NpgsqlDbType.Array | NpgsqlDbType.Varchar) {
            Value = _sharesConcepts
        },
        new NpgsqlParameter<DateTime>("recent_cutoff", _recentCutoff) { NpgsqlDbType = NpgsqlDbType.Date }
    ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var company = new Company(
            (ulong)reader.GetInt64(_companyIdIndex),
            (ulong)reader.GetInt64(_cikIndex),
            reader.GetString(_dataSourceIndex));
        _companies.Add(company);
        return true;
    }
}
