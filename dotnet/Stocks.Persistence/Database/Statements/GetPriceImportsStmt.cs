using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetPriceImportsStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT cik, ticker, exchange, last_imported_utc
FROM price_imports;
";

    private readonly List<PriceImportStatus> _imports;

    private static int _cikIndex = -1;
    private static int _tickerIndex = -1;
    private static int _exchangeIndex = -1;
    private static int _lastImportedIndex = -1;

    public GetPriceImportsStmt()
        : base(sql, nameof(GetPriceImportsStmt)) {
        _imports = [];
    }

    public IReadOnlyCollection<PriceImportStatus> Imports => _imports;

    protected override void ClearResults() => _imports.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        if (_tickerIndex != -1)
            return;
        _cikIndex = reader.GetOrdinal("cik");
        _tickerIndex = reader.GetOrdinal("ticker");
        _exchangeIndex = reader.GetOrdinal("exchange");
        _lastImportedIndex = reader.GetOrdinal("last_imported_utc");
    }

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        ulong cik = (ulong)reader.GetInt64(_cikIndex);
        string ticker = reader.GetString(_tickerIndex);
        string? exchange = reader.GetNullableRefType<string>(_exchangeIndex);
        var lastImportedUtc = reader.GetDateTime(_lastImportedIndex);
        _imports.Add(new PriceImportStatus(cik, ticker, exchange, lastImportedUtc));
        return true;
    }
}
