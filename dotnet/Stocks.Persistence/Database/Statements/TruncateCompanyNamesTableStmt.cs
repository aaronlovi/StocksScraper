using System.Collections.Generic;
using Npgsql;

namespace Stocks.Persistence.Database.Statements;

internal sealed class TruncateCompanyNamesTableStmt : NonQueryDbStmtBase {
    private const string sql = "TRUNCATE TABLE company_names";

    public TruncateCompanyNamesTableStmt() : base(sql, nameof(TruncateCompanyNamesTableStmt)) { }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];
}
