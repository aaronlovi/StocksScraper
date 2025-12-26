using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Stocks.Persistence.Database.DTO.Taxonomies;

namespace Stocks.Persistence.Database.Statements;

internal sealed class BulkInsertTaxonomyPresentationStmt : BulkInsertDbStmtBase<PresentationDetailsDTO> {
    public BulkInsertTaxonomyPresentationStmt(IReadOnlyCollection<PresentationDetailsDTO> taxonomyPresentations)
        : base(nameof(BulkInsertTaxonomyPresentationStmt), taxonomyPresentations) { }
    protected override string GetCopyCommand() => "COPY taxonomy_presentation"
        + " (taxonomy_presentation_id, taxonomy_concept_id, depth, order_in_depth, parent_concept_id, parent_presentation_id, role_name)"
        + " FROM STDIN (FORMAT BINARY)";
    protected override async Task WriteItemAsync(NpgsqlBinaryImporter writer, PresentationDetailsDTO presentationDetails) {
        await writer.WriteAsync(presentationDetails.PresentationId, NpgsqlDbType.Bigint);
        await writer.WriteAsync(presentationDetails.ConceptId, NpgsqlDbType.Bigint);
        await writer.WriteAsync(presentationDetails.Depth, NpgsqlDbType.Integer);
        await writer.WriteAsync(presentationDetails.OrderInDepth, NpgsqlDbType.Integer);
        await writer.WriteAsync(presentationDetails.ParentConceptId, NpgsqlDbType.Bigint);
        await writer.WriteAsync(presentationDetails.ParentPresentationId, NpgsqlDbType.Bigint);
        await writer.WriteAsync(presentationDetails.RoleName ?? string.Empty, NpgsqlDbType.Varchar);
    }
}
