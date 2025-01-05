using System;
using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence;

internal sealed class InsertSubmissionStmt : NonQueryDbStmtBase
{
    private const string sql = "INSERT INTO submissions (submission_id, company_id, filing_reference, filing_type, filing_category, report_date, acceptance_datetime)"
        + " VALUES (@submission_id, @company_id, @filing_reference, @filing_type, @filing_category, @report_date, @acceptance_datetime)";

    public InsertSubmissionStmt() : base(sql, nameof(InsertSubmissionStmt)) { }

    public Submission? Submission { get; set; }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters()
    {
        if (Submission is null) throw new InvalidOperationException("Submission is not set.");

        return [
            new NpgsqlParameter<long>("submission_id", (long)Submission.SubmissionId),
            new NpgsqlParameter<long>("company_id", (long)Submission.CompanyId),
            new NpgsqlParameter<string>("filing_reference", Submission.FilingReference),
            new NpgsqlParameter<int>("filing_type", (int)Submission.FilingType),
            new NpgsqlParameter<int>("filing_category", (int)Submission.FilingCategory),
            new NpgsqlParameter<DateTime>("report_date", Submission.ReportTime),
            new NpgsqlParameter<DateTime?>("acceptance_datetime", Submission.AcceptanceTime)];
    }
}
