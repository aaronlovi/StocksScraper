using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Stocks.Persistence.Database.DTO.Taxonomies;

namespace Stocks.Persistence.Database.Statements;

internal sealed class BulkInsertTaxonomyConceptsStmt : BulkInsertDbStmtBase<ConceptDetailsDTO> {
    public BulkInsertTaxonomyConceptsStmt(IReadOnlyCollection<ConceptDetailsDTO> taxonomyConcepts)
        : base(nameof(BulkInsertTaxonomyConceptsStmt), taxonomyConcepts) { }
    protected override string GetCopyCommand() => "COPY taxonomy_concepts"
        + " (taxonomy_concept_id, taxonomy_type_id, taxonomy_period_type_id, taxonomy_balance_type_id, is_abstract, name, label, documentation)"
        + " FROM STDIN (FORMAT BINARY)";
    protected override async Task WriteItemAsync(NpgsqlBinaryImporter writer, ConceptDetailsDTO concept) {
        await writer.WriteAsync(concept.ConceptId, NpgsqlDbType.Bigint);
        await writer.WriteAsync(concept.TaxonomyTypeId, NpgsqlDbType.Integer);
        await writer.WriteAsync(concept.PeriodTypeId, NpgsqlDbType.Integer);
        await writer.WriteAsync(concept.BalanceTypeId, NpgsqlDbType.Integer);
        await writer.WriteAsync(concept.IsAbstract, NpgsqlDbType.Boolean);
        await writer.WriteAsync(concept.Name, NpgsqlDbType.Varchar);
        await writer.WriteAsync(concept.Label, NpgsqlDbType.Varchar);
        await writer.WriteAsync(concept.Documentation, NpgsqlDbType.Varchar);
    }
}
