using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class UpsertPriceImportStmt : NonQueryDbStmtBase {
    private const string sql = @"
INSERT INTO price_imports (cik, ticker, exchange, last_imported_utc)
VALUES (@cik, @ticker, @exchange, @last_imported_utc)
ON CONFLICT (cik, ticker, exchange)
DO UPDATE SET last_imported_utc = EXCLUDED.last_imported_utc;
";

    private readonly PriceImportStatus _status;

    public UpsertPriceImportStmt(PriceImportStatus status)
        : base(sql, nameof(UpsertPriceImportStmt)) {
        _status = status;
    }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [
            new NpgsqlParameter<long>("cik", (long)_status.Cik),
            new NpgsqlParameter<string>("ticker", _status.Ticker),
            new NpgsqlParameter<string?>("exchange", _status.Exchange),
            new NpgsqlParameter("last_imported_utc", NpgsqlDbType.TimestampTz) { Value = _status.LastImportedUtc }
        ];
}
