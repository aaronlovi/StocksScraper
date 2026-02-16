using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetCompanyByCikStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT company_id, cik, data_source
FROM companies
WHERE cik = @cik
LIMIT 1;
";

    private readonly ulong _cik;
    private static int _companyIdIndex = -1;
    private static int _cikIndex = -1;
    private static int _dataSourceIndex = -1;

    public GetCompanyByCikStmt(ulong cik) : base(sql, nameof(GetCompanyByCikStmt)) {
        _cik = cik;
        Company = Company.Empty;
    }

    public Company Company { get; private set; }

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);

        if (_companyIdIndex != -1)
            return;

        _companyIdIndex = reader.GetOrdinal("company_id");
        _cikIndex = reader.GetOrdinal("cik");
        _dataSourceIndex = reader.GetOrdinal("data_source");
    }

    protected override void ClearResults() => Company = Company.Empty;

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [new NpgsqlParameter<long>("cik", (long)_cik)];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        Company = new Company(
            (ulong)reader.GetInt64(_companyIdIndex),
            (ulong)reader.GetInt64(_cikIndex),
            reader.GetString(_dataSourceIndex));
        return false;
    }
}
