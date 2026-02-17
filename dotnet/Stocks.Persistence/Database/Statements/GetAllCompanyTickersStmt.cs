using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetAllCompanyTickersStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT company_id, ticker, exchange
FROM company_tickers;
";

    private readonly List<CompanyTicker> _tickers;

    private static int _companyIdIndex = -1;
    private static int _tickerIndex = -1;
    private static int _exchangeIndex = -1;

    public GetAllCompanyTickersStmt() : base(sql, nameof(GetAllCompanyTickersStmt)) {
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

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var ticker = new CompanyTicker(
            (ulong)reader.GetInt64(_companyIdIndex),
            reader.GetString(_tickerIndex),
            reader.IsDBNull(_exchangeIndex) ? null : reader.GetString(_exchangeIndex));
        _tickers.Add(ticker);
        return true;
    }
}
