using System.Collections.Generic;
using Npgsql;
using Stocks.Persistence.Database.DTO.Taxonomies;

namespace Stocks.Persistence.Database.Statements;

internal class GetTaxonomyPresentationsByTaxonomyTypeStmt : QueryDbStmtBase {
    private const string Sql = @"
select p.taxonomy_presentation_id, p.taxonomy_concept_id, p.depth, p.order_in_depth, p.parent_concept_id, p.parent_presentation_id
from taxonomy_presentation p
join taxonomy_concepts c on c.taxonomy_concept_id = p.taxonomy_concept_id
where c.taxonomy_type_id = @taxonomy_type_id";

    private readonly int _taxonomyTypeId;
    private readonly List<PresentationDetailsDTO> _presentations = [];

    private static int _presentationIdIndex = -1;
    private static int _conceptIdIndex = -1;
    private static int _depthIndex = -1;
    private static int _orderInDepthIndex = -1;
    private static int _parentConceptIdIndex = -1;
    private static int _parentPresentationIdIndex = -1;

    public GetTaxonomyPresentationsByTaxonomyTypeStmt(int taxonomyTypeId)
        : base(Sql, nameof(GetTaxonomyPresentationsByTaxonomyTypeStmt)) {
        _taxonomyTypeId = taxonomyTypeId;
    }

    public IReadOnlyCollection<PresentationDetailsDTO> Presentations => _presentations;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        if (_presentationIdIndex != -1)
            return;
        _presentationIdIndex = reader.GetOrdinal("taxonomy_presentation_id");
        _conceptIdIndex = reader.GetOrdinal("taxonomy_concept_id");
        _depthIndex = reader.GetOrdinal("depth");
        _orderInDepthIndex = reader.GetOrdinal("order_in_depth");
        _parentConceptIdIndex = reader.GetOrdinal("parent_concept_id");
        _parentPresentationIdIndex = reader.GetOrdinal("parent_presentation_id");
    }
    protected override void ClearResults() => _presentations.Clear();
    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters()
        => [new NpgsqlParameter<int>("taxonomy_type_id", _taxonomyTypeId)];
    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var dto = new PresentationDetailsDTO(
            reader.GetInt64(_presentationIdIndex),
            reader.GetInt64(_conceptIdIndex),
            reader.GetInt32(_depthIndex),
            reader.GetInt32(_orderInDepthIndex),
            reader.GetInt64(_parentConceptIdIndex),
            reader.GetInt64(_parentPresentationIdIndex)
        );
        _presentations.Add(dto);
        return true;
    }
}
