#pragma warning disable IDE0290 // Use primary constructor

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using DataModels;
using Npgsql;
using MongoDB.Driver;

namespace Stocks.Persistence;

internal sealed class InsertCompaniesBatchStmt : BulkInsertDbStmtBase<Company>
{
    public InsertCompaniesBatchStmt(IReadOnlyCollection<Company> companies)
        : base(nameof(InsertCompaniesBatchStmt), companies)
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
