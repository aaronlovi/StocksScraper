using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;

namespace Stocks.Persistence.Database.Statements;

internal sealed class DeleteGrahamScoreSnapshotsByDateStmt : NonQueryDbStmtBase {
    private const string Sql = "DELETE FROM graham_score_snapshots WHERE as_of_date = @as_of_date";

    private readonly DateOnly _asOfDate;

    public DeleteGrahamScoreSnapshotsByDateStmt(DateOnly asOfDate)
        : base(Sql, nameof(DeleteGrahamScoreSnapshotsByDateStmt)) {
        _asOfDate = asOfDate;
    }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [
        new NpgsqlParameter<DateTime>("as_of_date", _asOfDate.ToDateTime(TimeOnly.MinValue)) { NpgsqlDbType = NpgsqlDbType.Date },
    ];
}
