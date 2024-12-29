#pragma warning disable IDE0290 // Use primary constructor

using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence;

internal sealed class BulkInsertCompanyNamesStmt : BulkInsertDbStmtBase<CompanyName>
{
    public BulkInsertCompanyNamesStmt(IReadOnlyCollection<CompanyName> companyNames)
        : base(nameof(BulkInsertCompanyNamesStmt), companyNames)
    { }

    protected override string GetCopyCommand() =>
        "COPY company_names (name_id, company_id, name) FROM STDIN (FORMAT BINARY)";

    protected override void WriteItem(NpgsqlBinaryImporter writer, CompanyName companyName)
    {
        writer.Write((long)companyName.NameId, NpgsqlTypes.NpgsqlDbType.Bigint);
        writer.Write((long)companyName.CompanyId, NpgsqlTypes.NpgsqlDbType.Bigint);
        writer.Write(companyName.Name, NpgsqlTypes.NpgsqlDbType.Varchar);
    }
}
