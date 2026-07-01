using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels.Scoring;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetGrahamSnapshotConstituentsStmt : QueryDbStmtBase {
    private const string Sql = @"
SELECT as_of_date, company_id, cik, company_name, ticker, exchange,
    overall_score, computable_checks, price_per_share, price_date
FROM graham_score_snapshots
WHERE overall_score >= @min_score
ORDER BY as_of_date ASC, company_id ASC";

    private readonly int _minScore;
    private readonly List<GrahamSnapshotConstituent> _results = [];

    private int _asOfDateIndex = -1;
    private int _companyIdIndex = -1;
    private int _cikIndex = -1;
    private int _companyNameIndex = -1;
    private int _tickerIndex = -1;
    private int _exchangeIndex = -1;
    private int _overallScoreIndex = -1;
    private int _computableChecksIndex = -1;
    private int _pricePerShareIndex = -1;
    private int _priceDateIndex = -1;

    public GetGrahamSnapshotConstituentsStmt(int minScore)
        : base(Sql, nameof(GetGrahamSnapshotConstituentsStmt)) {
        _minScore = minScore;
    }

    public IReadOnlyCollection<GrahamSnapshotConstituent> Results => _results;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        _asOfDateIndex = reader.GetOrdinal("as_of_date");
        _companyIdIndex = reader.GetOrdinal("company_id");
        _cikIndex = reader.GetOrdinal("cik");
        _companyNameIndex = reader.GetOrdinal("company_name");
        _tickerIndex = reader.GetOrdinal("ticker");
        _exchangeIndex = reader.GetOrdinal("exchange");
        _overallScoreIndex = reader.GetOrdinal("overall_score");
        _computableChecksIndex = reader.GetOrdinal("computable_checks");
        _pricePerShareIndex = reader.GetOrdinal("price_per_share");
        _priceDateIndex = reader.GetOrdinal("price_date");
    }

    protected override void ClearResults() => _results.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [
        new NpgsqlParameter<int>("min_score", _minScore) { NpgsqlDbType = NpgsqlDbType.Integer },
    ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var value = new GrahamSnapshotConstituent(
            DateOnly.FromDateTime(reader.GetDateTime(_asOfDateIndex)),
            (ulong)reader.GetInt64(_companyIdIndex),
            reader.GetInt64(_cikIndex).ToString(),
            reader.IsDBNull(_companyNameIndex) ? null : reader.GetString(_companyNameIndex),
            reader.IsDBNull(_tickerIndex) ? null : reader.GetString(_tickerIndex),
            reader.IsDBNull(_exchangeIndex) ? null : reader.GetString(_exchangeIndex),
            reader.GetInt32(_overallScoreIndex),
            reader.GetInt32(_computableChecksIndex),
            reader.IsDBNull(_pricePerShareIndex) ? null : reader.GetDecimal(_pricePerShareIndex),
            reader.IsDBNull(_priceDateIndex) ? null : DateOnly.FromDateTime(reader.GetDateTime(_priceDateIndex)));
        _results.Add(value);
        return true;
    }
}
