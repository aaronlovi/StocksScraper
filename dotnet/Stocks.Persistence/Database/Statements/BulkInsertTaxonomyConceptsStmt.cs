
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Stocks.Persistence.Database.DTO;

namespace Stocks.Persistence.Database.Statements;

internal sealed class BulkInsertTaxonomyConceptsStmt : BulkInsertDbStmtBase<TaxonomyConceptDTO> {
    public BulkInsertTaxonomyConceptsStmt(IReadOnlyCollection<TaxonomyConceptDTO> taxonomyConcepts)
        : base(nameof(BulkInsertTaxonomyConceptsStmt), taxonomyConcepts) { }
    protected override string GetCopyCommand() => "COPY taxonomy_concepts"
        + " (taxonomy_concept_id, taxonomy_type_id, taxonomy_period_type_id, taxonomy_balance_type_id, is_abstract, name, label, documentation)"
        + " FROM STDIN (FORMAT BINARY)";
    protected override async Task WriteItemAsync(NpgsqlBinaryImporter writer, TaxonomyConceptDTO concept) {
        await writer.WriteAsync(concept.TaxonomyConceptId, NpgsqlDbType.Bigint);
        await writer.WriteAsync(concept.TaxonomyTypeId, NpgsqlDbType.Integer);
        await writer.WriteAsync(concept.TaxonomyPeriodTypeId, NpgsqlDbType.Integer);
        await writer.WriteAsync(concept.TaxonomyBalanceTypeId, NpgsqlDbType.Integer);
        await writer.WriteAsync(concept.IsAbstract, NpgsqlDbType.Boolean);
        await writer.WriteAsync(concept.Name, NpgsqlDbType.Varchar);
        await writer.WriteAsync(concept.Label, NpgsqlDbType.Varchar);
        await writer.WriteAsync(concept.Documentation, NpgsqlDbType.Varchar);
    }
}