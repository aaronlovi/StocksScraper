using System;
using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetPriceDownloadsStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT cik, ticker, exchange, last_downloaded_utc
FROM price_downloads;
";

    private readonly List<PriceDownloadStatus> _downloads;

    private static int _cikIndex = -1;
    private static int _tickerIndex = -1;
    private static int _exchangeIndex = -1;
    private static int _lastDownloadedIndex = -1;

    public GetPriceDownloadsStmt()
        : base(sql, nameof(GetPriceDownloadsStmt)) {
        _downloads = [];
    }

    public IReadOnlyCollection<PriceDownloadStatus> Downloads => _downloads;

    protected override void ClearResults() => _downloads.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        if (_tickerIndex != -1)
            return;
        _cikIndex = reader.GetOrdinal("cik");
        _tickerIndex = reader.GetOrdinal("ticker");
        _exchangeIndex = reader.GetOrdinal("exchange");
        _lastDownloadedIndex = reader.GetOrdinal("last_downloaded_utc");
    }

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        ulong cik = (ulong)reader.GetInt64(_cikIndex);
        string ticker = reader.GetString(_tickerIndex);
        string? exchange = reader.GetNullableRefType<string>(_exchangeIndex);
        DateTime lastDownloadedUtc = reader.GetDateTime(_lastDownloadedIndex);
        _downloads.Add(new PriceDownloadStatus(cik, ticker, exchange, lastDownloadedUtc));
        return true;
    }
}
