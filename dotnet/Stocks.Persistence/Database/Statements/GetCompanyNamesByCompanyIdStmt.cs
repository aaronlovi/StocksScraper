using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetCompanyNamesByCompanyIdStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT name_id, company_id, name
FROM company_names
WHERE company_id = @company_id;
";

    private readonly ulong _companyId;
    private readonly List<CompanyName> _names;

    private static int _nameIdIndex = -1;
    private static int _companyIdIndex = -1;
    private static int _nameIndex = -1;

    public GetCompanyNamesByCompanyIdStmt(ulong companyId)
        : base(sql, nameof(GetCompanyNamesByCompanyIdStmt)) {
        _companyId = companyId;
        _names = [];
    }

    public IReadOnlyCollection<CompanyName> Names => _names;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);

        if (_nameIdIndex != -1)
            return;

        _nameIdIndex = reader.GetOrdinal("name_id");
        _companyIdIndex = reader.GetOrdinal("company_id");
        _nameIndex = reader.GetOrdinal("name");
    }

    protected override void ClearResults() => _names.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [new NpgsqlParameter<long>("company_id", (long)_companyId)];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var name = new CompanyName(
            (ulong)reader.GetInt64(_nameIdIndex),
            (ulong)reader.GetInt64(_companyIdIndex),
            reader.GetString(_nameIndex));
        _names.Add(name);
        return true;
    }
}
