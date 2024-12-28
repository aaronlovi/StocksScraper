#pragma warning disable IDE0290 // Use primary constructor

using System.Collections.Generic;
using DataModels;
using Npgsql;

namespace Stocks.Persistence;

internal sealed class InsertCompanyNamesBatchStmt : BulkInsertDbStmtBase<CompanyName>
{
    public InsertCompanyNamesBatchStmt(IReadOnlyCollection<CompanyName> companyNames)
        : base(nameof(InsertCompanyNamesBatchStmt), companyNames)
    { }

    protected override string GetCopyCommand() =>
        "COPY company_names (name_id, cik, name) FROM STDIN (FORMAT BINARY)";

    protected override void WriteItem(NpgsqlBinaryImporter writer, CompanyName companyName)
    {
        writer.Write((long)companyName.NameId, NpgsqlTypes.NpgsqlDbType.Bigint);
        writer.Write((long)companyName.Cik, NpgsqlTypes.NpgsqlDbType.Bigint);
        writer.Write(companyName.Name, NpgsqlTypes.NpgsqlDbType.Varchar);
    }
}
