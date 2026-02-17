using System;
using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;
using Stocks.DataModels.Enums;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetSubmissionsByCompanyIdStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT submission_id, company_id, filing_reference, filing_type, filing_category, report_date, acceptance_datetime
FROM submissions
WHERE company_id = @company_id AND report_date <= CURRENT_DATE
ORDER BY report_date DESC;
";

    private readonly ulong _companyId;
    private readonly List<Submission> _submissions;

    private static int _submissionIdIndex = -1;
    private static int _companyIdIndex = -1;
    private static int _filingReferenceIndex = -1;
    private static int _filingTypeIndex = -1;
    private static int _filingCategoryIndex = -1;
    private static int _reportDateIndex = -1;
    private static int _acceptanceDatetimeIndex = -1;

    public GetSubmissionsByCompanyIdStmt(ulong companyId)
        : base(sql, nameof(GetSubmissionsByCompanyIdStmt)) {
        _companyId = companyId;
        _submissions = [];
    }

    public IReadOnlyCollection<Submission> Submissions => _submissions;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);

        if (_submissionIdIndex != -1)
            return;

        _submissionIdIndex = reader.GetOrdinal("submission_id");
        _companyIdIndex = reader.GetOrdinal("company_id");
        _filingReferenceIndex = reader.GetOrdinal("filing_reference");
        _filingTypeIndex = reader.GetOrdinal("filing_type");
        _filingCategoryIndex = reader.GetOrdinal("filing_category");
        _reportDateIndex = reader.GetOrdinal("report_date");
        _acceptanceDatetimeIndex = reader.GetOrdinal("acceptance_datetime");
    }

    protected override void ClearResults() => _submissions.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [new NpgsqlParameter<long>("company_id", (long)_companyId)];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var reportDate = DateOnly.FromDateTime(reader.GetDateTime(_reportDateIndex));
        DateTime? acceptanceTime = reader.GetNullableValueType<DateTime>(_acceptanceDatetimeIndex);

        var item = new Submission(
            (ulong)reader.GetInt64(_submissionIdIndex),
            (ulong)reader.GetInt64(_companyIdIndex),
            reader.GetString(_filingReferenceIndex),
            (FilingType)reader.GetInt32(_filingTypeIndex),
            (FilingCategory)reader.GetInt32(_filingCategoryIndex),
            reportDate,
            acceptanceTime);
        _submissions.Add(item);
        return true;
    }
}
