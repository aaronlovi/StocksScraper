using System.Collections.Generic;
using Npgsql;

namespace Stocks.Persistence.Database.Statements;

internal sealed class TruncateCompanyScoresStmt : NonQueryDbStmtBase {
    private const string Sql = "TRUNCATE TABLE company_scores";

    public TruncateCompanyScoresStmt() : base(Sql, nameof(TruncateCompanyScoresStmt)) { }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];
}
