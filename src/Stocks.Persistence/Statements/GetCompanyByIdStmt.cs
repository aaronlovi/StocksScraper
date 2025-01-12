using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Statements;

internal sealed class GetCompanyByIdStmt : QueryDbStmtBase
{
    private const string sql = @"
SELECT company_id, cik, data_source
FROM companies
WHERE company_id = @company_id;
";

    // Inputs
    private readonly ulong _companyId;

    // Outputs
    private Company _company;

    private static int _companyIdIndex = -1;
    private static int _cikIndex = -1;
    private static int _dataSourceIndex = -1;

    public GetCompanyByIdStmt(ulong companyId) : base(sql, nameof(GetCompanyByIdStmt))
    {
        _companyId = companyId;
        _company = Company.Empty;
    }

    public Company Company => _company;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader)
    {
        base.BeforeRowProcessing(reader);

        if (_companyIdIndex != -1) return;

        _companyIdIndex = reader.GetOrdinal("company_id");
        _cikIndex = reader.GetOrdinal("cik");
        _dataSourceIndex = reader.GetOrdinal("data_source");
    }

    protected override void ClearResults() => _company = Company.Empty;

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [ new NpgsqlParameter<ulong>("company_id", _companyId) ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader)
    {
        _company = new Company(
            (ulong)reader.GetInt64(_companyIdIndex),
            (ulong)reader.GetInt64(_cikIndex),
            reader.GetString(_dataSourceIndex));
        return false;
    }
}
