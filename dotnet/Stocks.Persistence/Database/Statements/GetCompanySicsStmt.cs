using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

/// <summary>Every company with a SIC code.</summary>
internal sealed class GetCompanySicsStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT company_id, sic_code, sic_description
FROM companies
WHERE sic_code IS NOT NULL
ORDER BY company_id;
";

    private readonly List<CompanySic> _results = [];

    private int _companyIdIndex = -1;
    private int _sicCodeIndex = -1;
    private int _sicDescriptionIndex = -1;

    public GetCompanySicsStmt() : base(sql, nameof(GetCompanySicsStmt)) { }

    public IReadOnlyCollection<CompanySic> Results => _results;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        _companyIdIndex = reader.GetOrdinal("company_id");
        _sicCodeIndex = reader.GetOrdinal("sic_code");
        _sicDescriptionIndex = reader.GetOrdinal("sic_description");
    }

    protected override void ClearResults() => _results.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        _results.Add(new CompanySic(
            (ulong)reader.GetInt64(_companyIdIndex),
            reader.GetInt32(_sicCodeIndex),
            reader.IsDBNull(_sicDescriptionIndex) ? null : reader.GetString(_sicDescriptionIndex)));
        return true;
    }
}
