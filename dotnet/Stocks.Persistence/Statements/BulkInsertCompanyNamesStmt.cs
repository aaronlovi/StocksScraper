#pragma warning disable IDE0290 // Use primary constructor

using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using Stocks.DataModels;
using Stocks.Persistence.Statements;

namespace Stocks.Persistence;

internal sealed class BulkInsertCompanyNamesStmt : BulkInsertDbStmtBase<CompanyName>
{
    public BulkInsertCompanyNamesStmt(IReadOnlyCollection<CompanyName> companyNames)
        : base(nameof(BulkInsertCompanyNamesStmt), companyNames)
    { }

    protected override string GetCopyCommand() =>
        "COPY company_names (name_id, company_id, name) FROM STDIN (FORMAT BINARY)";

    protected override async Task WriteItemAsync(NpgsqlBinaryImporter writer, CompanyName companyName)
    {
        await writer.WriteAsync((long)companyName.NameId, NpgsqlTypes.NpgsqlDbType.Bigint);
        await writer.WriteAsync((long)companyName.CompanyId, NpgsqlTypes.NpgsqlDbType.Bigint);
        await writer.WriteAsync(companyName.Name, NpgsqlTypes.NpgsqlDbType.Varchar);
    }
}
