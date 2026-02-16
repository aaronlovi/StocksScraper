using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetCompanyTickersByCompanyIdStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT company_id, ticker, exchange
FROM company_tickers
WHERE company_id = @company_id;
";

    private readonly ulong _companyId;
    private readonly List<CompanyTicker> _tickers;

    private static int _companyIdIndex = -1;
    private static int _tickerIndex = -1;
    private static int _exchangeIndex = -1;

    public GetCompanyTickersByCompanyIdStmt(ulong companyId)
        : base(sql, nameof(GetCompanyTickersByCompanyIdStmt)) {
        _companyId = companyId;
        _tickers = [];
    }

    public IReadOnlyCollection<CompanyTicker> Tickers => _tickers;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);

        if (_companyIdIndex != -1)
            return;

        _companyIdIndex = reader.GetOrdinal("company_id");
        _tickerIndex = reader.GetOrdinal("ticker");
        _exchangeIndex = reader.GetOrdinal("exchange");
    }

    protected override void ClearResults() => _tickers.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [new NpgsqlParameter<long>("company_id", (long)_companyId)];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        string? exchange = reader.IsDBNull(_exchangeIndex) ? null : reader.GetString(_exchangeIndex);
        var ticker = new CompanyTicker(
            (ulong)reader.GetInt64(_companyIdIndex),
            reader.GetString(_tickerIndex),
            exchange);
        _tickers.Add(ticker);
        return true;
    }
}
