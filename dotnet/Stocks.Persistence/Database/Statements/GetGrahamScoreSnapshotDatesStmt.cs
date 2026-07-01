using System;
using System.Collections.Generic;
using Npgsql;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetGrahamScoreSnapshotDatesStmt : QueryDbStmtBase {
    private const string Sql = "SELECT DISTINCT as_of_date FROM graham_score_snapshots ORDER BY as_of_date";

    private readonly List<DateOnly> _results = [];

    private int _asOfDateIndex = -1;

    public GetGrahamScoreSnapshotDatesStmt() : base(Sql, nameof(GetGrahamScoreSnapshotDatesStmt)) { }

    public IReadOnlyCollection<DateOnly> Results => _results;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        _asOfDateIndex = reader.GetOrdinal("as_of_date");
    }

    protected override void ClearResults() => _results.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        _results.Add(DateOnly.FromDateTime(reader.GetDateTime(_asOfDateIndex)));
        return true;
    }
}
