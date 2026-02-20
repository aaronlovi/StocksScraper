using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetCompaniesWithoutSharesDataStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT c.company_id, c.cik, c.data_source,
       (SELECT COUNT(DISTINCT pd2.ticker) FROM price_downloads pd2 WHERE pd2.cik = c.cik) AS ticker_count
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
GROUP BY c.company_id, c.cik, c.data_source;
";

    private readonly string[] _sharesConcepts;
    private readonly DateTime _recentCutoff;
    private readonly List<Company> _companies;
    private readonly HashSet<ulong> _multiTickerCompanyIds;

    private static int _companyIdIndex = -1;
    private static int _cikIndex = -1;
    private static int _dataSourceIndex = -1;
    private static int _tickerCountIndex = -1;

    public GetCompaniesWithoutSharesDataStmt(string[] sharesConcepts, DateTime recentCutoff)
        : base(sql, nameof(GetCompaniesWithoutSharesDataStmt)) {
        _sharesConcepts = sharesConcepts;
        _recentCutoff = recentCutoff;
        _companies = [];
        _multiTickerCompanyIds = [];
    }

    public IReadOnlyCollection<Company> Companies => _companies;
    public IReadOnlyCollection<ulong> MultiTickerCompanyIds => _multiTickerCompanyIds;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);

        if (_companyIdIndex != -1)
            return;

        _companyIdIndex = reader.GetOrdinal("company_id");
        _cikIndex = reader.GetOrdinal("cik");
        _dataSourceIndex = reader.GetOrdinal("data_source");
        _tickerCountIndex = reader.GetOrdinal("ticker_count");
    }

    protected override void ClearResults() {
        _companies.Clear();
        _multiTickerCompanyIds.Clear();
    }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [
        new NpgsqlParameter("shares_concepts", NpgsqlDbType.Array | NpgsqlDbType.Varchar) {
            Value = _sharesConcepts
        },
        new NpgsqlParameter<DateTime>("recent_cutoff", _recentCutoff) { NpgsqlDbType = NpgsqlDbType.Date }
    ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        ulong companyId = (ulong)reader.GetInt64(_companyIdIndex);
        var company = new Company(
            companyId,
            (ulong)reader.GetInt64(_cikIndex),
            reader.GetString(_dataSourceIndex));
        _companies.Add(company);

        long tickerCount = reader.GetInt64(_tickerCountIndex);
        if (tickerCount > 1)
            _multiTickerCompanyIds.Add(companyId);

        return true;
    }
}
