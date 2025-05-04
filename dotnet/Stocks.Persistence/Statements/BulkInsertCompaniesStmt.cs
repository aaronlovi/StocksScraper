
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence.Statements;

internal sealed class BulkInsertCompaniesStmt : BulkInsertDbStmtBase<Company> {
    public BulkInsertCompaniesStmt(IReadOnlyCollection<Company> companies)
        : base(nameof(BulkInsertCompaniesStmt), companies) { }

    protected override string GetCopyCommand() =>
        "COPY companies (company_id, cik, data_source) FROM STDIN (FORMAT BINARY)";

    protected override async Task WriteItemAsync(NpgsqlBinaryImporter writer, Company company) {
        await writer.WriteAsync((long)company.CompanyId, NpgsqlDbType.Bigint);
        await writer.WriteAsync((long)company.Cik, NpgsqlDbType.Bigint);
        await writer.WriteAsync(company.DataSource, NpgsqlDbType.Varchar);
    }
}
