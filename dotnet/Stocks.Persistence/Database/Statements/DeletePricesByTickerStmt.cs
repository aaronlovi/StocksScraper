using System.Collections.Generic;
using Npgsql;

namespace Stocks.Persistence.Database.Statements;

internal sealed class DeletePricesByTickerStmt : NonQueryDbStmtBase {
    private const string sql = @"
DELETE FROM prices
WHERE ticker = @ticker;
";

    private readonly string _ticker;

    public DeletePricesByTickerStmt(string ticker)
        : base(sql, nameof(DeletePricesByTickerStmt)) {
        _ticker = ticker;
    }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [new NpgsqlParameter<string>("ticker", _ticker)];
}
