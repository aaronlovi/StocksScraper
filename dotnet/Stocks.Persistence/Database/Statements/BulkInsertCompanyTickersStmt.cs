using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class BulkInsertCompanyTickersStmt : NonQueryBatchedDbStmtBase {
    private const string sql = @"
INSERT INTO company_tickers (company_id, ticker, exchange)
VALUES (@company_id, @ticker, @exchange)
ON CONFLICT (company_id, ticker) DO UPDATE SET exchange = EXCLUDED.exchange;
";

    public BulkInsertCompanyTickersStmt(IReadOnlyCollection<CompanyTicker> tickers)
        : base(nameof(BulkInsertCompanyTickersStmt)) {
        foreach (CompanyTicker ticker in tickers) {
            var exchangeParam = new NpgsqlParameter("exchange", NpgsqlDbType.Varchar) {
                Value = ticker.Exchange is not null ? ticker.Exchange : System.DBNull.Value
            };
            AddCommandToBatch(sql, [
                new NpgsqlParameter<long>("company_id", (long)ticker.CompanyId),
                new NpgsqlParameter<string>("ticker", ticker.Ticker),
                exchangeParam
            ]);
        }
    }
}
