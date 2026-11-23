using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

/// <summary>Sets <c>sic_code</c> and <c>sic_description</c> on existing companies (null clears them).</summary>
internal sealed class UpdateCompanySicBatchStmt : NonQueryBatchedDbStmtBase {
    private const string sql = @"
UPDATE companies SET sic_code = @sic_code, sic_description = @sic_description
WHERE company_id = @company_id;
";

    public UpdateCompanySicBatchStmt(IReadOnlyCollection<CompanySic> sics)
        : base(nameof(UpdateCompanySicBatchStmt)) {
        foreach (CompanySic sic in sics) {
            AddCommandToBatch(sql, [
                new NpgsqlParameter("sic_code", NpgsqlDbType.Integer) { Value = sic.SicCode.HasValue ? sic.SicCode.Value : System.DBNull.Value },
                new NpgsqlParameter("sic_description", NpgsqlDbType.Varchar) { Value = sic.SicDescription is null ? System.DBNull.Value : sic.SicDescription },
                new NpgsqlParameter<long>("company_id", (long)sic.CompanyId)
            ]);
        }
    }
}
