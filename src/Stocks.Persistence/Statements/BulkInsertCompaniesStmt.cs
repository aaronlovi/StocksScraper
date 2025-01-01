#pragma warning disable IDE0290 // Use primary constructor

using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence;

internal sealed class BulkInsertCompaniesStmt : BulkInsertDbStmtBase<Company>
{
    public BulkInsertCompaniesStmt(IReadOnlyCollection<Company> companies)
        : base(nameof(BulkInsertCompaniesStmt), companies)
    { }

    protected override string GetCopyCommand() =>
        "COPY companies (company_id, cik, data_source) FROM STDIN (FORMAT BINARY)";

    protected override async Task WriteItemAsync(NpgsqlBinaryImporter writer, Company company)
    {
        await writer.WriteAsync((long)company.CompanyId, NpgsqlTypes.NpgsqlDbType.Bigint);
        await writer.WriteAsync((long)company.Cik, NpgsqlTypes.NpgsqlDbType.Bigint);
        await writer.WriteAsync(company.DataSource, NpgsqlTypes.NpgsqlDbType.Varchar);
    }
}
