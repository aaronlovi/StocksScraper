using System.Collections.Generic;
using Npgsql;

namespace Stocks.Persistence.Database.Statements;

internal sealed class TruncateCompanyMoatScoresStmt : NonQueryDbStmtBase {
    private const string Sql = "TRUNCATE TABLE company_moat_scores";

    public TruncateCompanyMoatScoresStmt() : base(Sql, nameof(TruncateCompanyMoatScoresStmt)) { }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];
}
