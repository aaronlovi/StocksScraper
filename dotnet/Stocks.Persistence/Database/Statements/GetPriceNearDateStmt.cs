using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetPriceNearDateStmt : QueryDbStmtBase {
    private const string Sql = @"
SELECT price_id, cik, ticker, exchange, stooq_symbol, price_date, open, high, low, close, volume
FROM prices
WHERE ticker = @ticker AND price_date <= @target_date
ORDER BY price_date DESC
LIMIT 1;
";

    private readonly string _ticker;
    private readonly DateOnly _targetDate;
    private PriceRow? _price;

    private static int _priceIdIndex = -1;
    private static int _cikIndex = -1;
    private static int _tickerIndex = -1;
    private static int _exchangeIndex = -1;
    private static int _stooqSymbolIndex = -1;
    private static int _priceDateIndex = -1;
    private static int _openIndex = -1;
    private static int _highIndex = -1;
    private static int _lowIndex = -1;
    private static int _closeIndex = -1;
    private static int _volumeIndex = -1;

    public GetPriceNearDateStmt(string ticker, DateOnly targetDate)
        : base(Sql, nameof(GetPriceNearDateStmt)) {
        _ticker = ticker;
        _targetDate = targetDate;
    }

    public PriceRow? Price => _price;

    protected override void ClearResults() => _price = null;

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [
            new NpgsqlParameter<string>("ticker", _ticker),
            new NpgsqlParameter<DateTime>("target_date", _targetDate.ToDateTime(TimeOnly.MinValue)) { NpgsqlDbType = NpgsqlDbType.Date },
        ];

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        if (_priceIdIndex != -1)
            return;
        _priceIdIndex = reader.GetOrdinal("price_id");
        _cikIndex = reader.GetOrdinal("cik");
        _tickerIndex = reader.GetOrdinal("ticker");
        _exchangeIndex = reader.GetOrdinal("exchange");
        _stooqSymbolIndex = reader.GetOrdinal("stooq_symbol");
        _priceDateIndex = reader.GetOrdinal("price_date");
        _openIndex = reader.GetOrdinal("open");
        _highIndex = reader.GetOrdinal("high");
        _lowIndex = reader.GetOrdinal("low");
        _closeIndex = reader.GetOrdinal("close");
        _volumeIndex = reader.GetOrdinal("volume");
    }

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        ulong priceId = (ulong)reader.GetInt64(_priceIdIndex);
        ulong cik = (ulong)reader.GetInt64(_cikIndex);
        string ticker = reader.GetString(_tickerIndex);
        string? exchange = reader.GetNullableRefType<string>(_exchangeIndex);
        string stooqSymbol = reader.GetString(_stooqSymbolIndex);
        DateTime priceDate = reader.GetDateTime(_priceDateIndex);
        decimal open = reader.GetDecimal(_openIndex);
        decimal high = reader.GetDecimal(_highIndex);
        decimal low = reader.GetDecimal(_lowIndex);
        decimal close = reader.GetDecimal(_closeIndex);
        long volume = reader.GetInt64(_volumeIndex);

        _price = new PriceRow(
            priceId,
            cik,
            ticker,
            exchange,
            stooqSymbol,
            DateOnly.FromDateTime(priceDate),
            open,
            high,
            low,
            close,
            volume);
        return false; // Only need one row (LIMIT 1)
    }
}
