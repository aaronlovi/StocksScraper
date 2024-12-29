#pragma warning disable IDE0290 // Use primary constructor

using System.Collections.Generic;
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

    protected override void WriteItem(NpgsqlBinaryImporter writer, Company company)
    {
        writer.Write((long)company.CompanyId, NpgsqlTypes.NpgsqlDbType.Bigint);
        writer.Write((long)company.Cik, NpgsqlTypes.NpgsqlDbType.Bigint);
        writer.Write(company.DataSource, NpgsqlTypes.NpgsqlDbType.Varchar);
    }
}
