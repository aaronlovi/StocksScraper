using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels.Scoring;

namespace Stocks.Persistence.Database.Statements;

internal class GetAllPricesNearDateStmt : QueryDbStmtBase {
    private const string Sql = @"
SELECT DISTINCT ON (ticker) ticker, close, price_date
FROM prices
WHERE price_date <= @target_date
ORDER BY ticker, price_date DESC";

    private readonly DateOnly _targetDate;
    private readonly List<LatestPrice> _results = [];

    private int _tickerIndex = -1;
    private int _closeIndex = -1;
    private int _priceDateIndex = -1;

    public GetAllPricesNearDateStmt(DateOnly targetDate)
        : base(Sql, nameof(GetAllPricesNearDateStmt)) {
        _targetDate = targetDate;
    }

    public IReadOnlyCollection<LatestPrice> Results => _results;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        _tickerIndex = reader.GetOrdinal("ticker");
        _closeIndex = reader.GetOrdinal("close");
        _priceDateIndex = reader.GetOrdinal("price_date");
    }

    protected override void ClearResults() => _results.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [
        new NpgsqlParameter<DateTime>("target_date", _targetDate.ToDateTime(TimeOnly.MinValue)) { NpgsqlDbType = NpgsqlDbType.Date },
    ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var value = new LatestPrice(
            reader.GetString(_tickerIndex),
            reader.GetDecimal(_closeIndex),
            DateOnly.FromDateTime(reader.GetDateTime(_priceDateIndex))
        );
        _results.Add(value);
        return true;
    }
}
