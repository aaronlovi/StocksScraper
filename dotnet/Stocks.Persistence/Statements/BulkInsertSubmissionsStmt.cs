#pragma warning disable IDE0290 // Use primary constructor

using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;
using Stocks.Persistence.Statements;

namespace Stocks.Persistence;

internal sealed class BulkInsertSubmissionsStmt : BulkInsertDbStmtBase<Submission>
{
    public BulkInsertSubmissionsStmt(IReadOnlyCollection<Submission> submissions)
        : base(nameof(BulkInsertSubmissionsStmt), submissions)
    { }
    protected override string GetCopyCommand() => "COPY submissions"
        + " (submission_id, company_id, filing_reference, filing_type, filing_category, report_date, acceptance_datetime)"
        + " FROM STDIN (FORMAT BINARY)";

    protected override async Task WriteItemAsync(NpgsqlBinaryImporter writer, Submission submission)
    {
        await writer.WriteAsync((long)submission.SubmissionId, NpgsqlDbType.Bigint);
        await writer.WriteAsync((long)submission.CompanyId, NpgsqlDbType.Bigint);
        await writer.WriteAsync(submission.FilingReference, NpgsqlDbType.Varchar);
        await writer.WriteAsync((int)submission.FilingType, NpgsqlDbType.Integer);
        await writer.WriteAsync((int)submission.FilingCategory, NpgsqlDbType.Integer);
        await writer.WriteAsync(submission.ReportDate, NpgsqlDbType.Date);
        await writer.WriteNullableAsync(submission.AcceptanceTime, NpgsqlDbType.TimestampTz);
    }
}
