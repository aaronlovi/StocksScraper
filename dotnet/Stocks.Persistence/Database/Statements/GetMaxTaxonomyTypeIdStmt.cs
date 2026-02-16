using System.Collections.Generic;
using Npgsql;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetMaxTaxonomyTypeIdStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT COALESCE(MAX(taxonomy_type_id), 0) AS max_id
FROM taxonomy_types;
";

    private static int _maxIdIndex = -1;
    private int _maxId;

    public GetMaxTaxonomyTypeIdStmt()
        : base(sql, nameof(GetMaxTaxonomyTypeIdStmt)) { }

    public int MaxId => _maxId;

    protected override void ClearResults() => _maxId = 0;

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        if (_maxIdIndex != -1)
            return;
        _maxIdIndex = reader.GetOrdinal("max_id");
    }

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        _maxId = reader.GetInt32(_maxIdIndex);
        return false;
    }
}
