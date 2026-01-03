using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class UpsertPriceDownloadStmt : NonQueryDbStmtBase {
    private const string sql = @"
INSERT INTO price_downloads (cik, ticker, exchange, last_downloaded_utc)
VALUES (@cik, @ticker, @exchange, @last_downloaded_utc)
ON CONFLICT (cik, ticker, exchange)
DO UPDATE SET last_downloaded_utc = EXCLUDED.last_downloaded_utc;
";

    private readonly PriceDownloadStatus _status;

    public UpsertPriceDownloadStmt(PriceDownloadStatus status)
        : base(sql, nameof(UpsertPriceDownloadStmt)) {
        _status = status;
    }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [
            new NpgsqlParameter<long>("cik", (long)_status.Cik),
            new NpgsqlParameter<string>("ticker", _status.Ticker),
            new NpgsqlParameter<string?>("exchange", _status.Exchange),
            new NpgsqlParameter("last_downloaded_utc", NpgsqlDbType.TimestampTz) { Value = _status.LastDownloadedUtc }
        ];
}
