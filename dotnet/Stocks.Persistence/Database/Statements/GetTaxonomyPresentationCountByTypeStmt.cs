using System.Collections.Generic;
using Npgsql;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetTaxonomyPresentationCountByTypeStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT COUNT(*) AS presentation_count
FROM taxonomy_presentation p
JOIN taxonomy_concepts c ON c.taxonomy_concept_id = p.taxonomy_concept_id
WHERE c.taxonomy_type_id = @taxonomy_type_id;
";

    private static int _countIndex = -1;
    private int _count;
    private readonly int _taxonomyTypeId;

    public GetTaxonomyPresentationCountByTypeStmt(int taxonomyTypeId)
        : base(sql, nameof(GetTaxonomyPresentationCountByTypeStmt)) {
        _taxonomyTypeId = taxonomyTypeId;
    }

    public int Count => _count;

    protected override void ClearResults() => _count = 0;

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [new NpgsqlParameter<int>("taxonomy_type_id", _taxonomyTypeId)];

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        if (_countIndex != -1)
            return;
        _countIndex = reader.GetOrdinal("presentation_count");
    }

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        _count = (int)reader.GetInt64(_countIndex);
        return false;
    }
}
