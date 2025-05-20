using System.Collections.Generic;
using Npgsql;
using Stocks.Persistence.Database.DTO.Taxonomies;

namespace Stocks.Persistence.Database.Statements;

internal class GetTaxonomyConceptsByTaxonomyTypeStmt : QueryDbStmtBase {
    private const string Sql = """
select taxonomy_concept_id, taxonomy_type_id, taxonomy_period_type_id, taxonomy_balance_type_id, is_abstract, name, label, documentation
from taxonomy_concepts
where taxonomy_type_id = @taxonomy_type_id
""";

    // Inputs
    private readonly int _taxonomyTypeId;

    // Outputs
    private readonly List<ConceptDetailsDTO> _taxonomyConcepts;

    private static int _taxonomyConceptIdIndex = -1;
    private static int _taxonomyTypeIdIndex = -1;
    private static int _taxonomyPeriodTypeIdIndex = -1;
    private static int _taxonomyBalanceTypeIdIndex = -1;
    private static int _isAbstractIndex = -1;
    private static int _nameIndex = -1;
    private static int _labelIndex = -1;
    private static int _documentationIndex = -1;

    public GetTaxonomyConceptsByTaxonomyTypeStmt(int taxonomyTypeId)
        : base(Sql, nameof(GetTaxonomyConceptsByTaxonomyTypeStmt)) {
        _taxonomyTypeId = taxonomyTypeId;
        _taxonomyConcepts = [];
    }

    public IReadOnlyCollection<ConceptDetailsDTO> TaxonomyConcepts => _taxonomyConcepts;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);

        if (_taxonomyConceptIdIndex != -1)
            return;

        _taxonomyConceptIdIndex = reader.GetOrdinal("taxonomy_concept_id");
        _taxonomyTypeIdIndex = reader.GetOrdinal("taxonomy_type_id");
        _taxonomyPeriodTypeIdIndex = reader.GetOrdinal("taxonomy_period_type_id");
        _taxonomyBalanceTypeIdIndex = reader.GetOrdinal("taxonomy_balance_type_id");
        _isAbstractIndex = reader.GetOrdinal("is_abstract");
        _nameIndex = reader.GetOrdinal("name");
        _labelIndex = reader.GetOrdinal("label");
        _documentationIndex = reader.GetOrdinal("documentation");
    }
    protected override void ClearResults() => _taxonomyConcepts.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters()
        => [new NpgsqlParameter<int>("taxonomy_type_id", _taxonomyTypeId)];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var taxonomyConcept = new ConceptDetailsDTO(
            reader.GetInt32(_taxonomyConceptIdIndex),
            reader.GetInt32(_taxonomyTypeIdIndex),
            reader.GetInt32(_taxonomyPeriodTypeIdIndex),
            reader.GetInt32(_taxonomyBalanceTypeIdIndex),
            reader.GetBoolean(_isAbstractIndex),
            reader.GetString(_nameIndex),
            reader.GetString(_labelIndex),
            reader.GetString(_documentationIndex)
        );
        _taxonomyConcepts.Add(taxonomyConcept);

        return true;
    }
}
