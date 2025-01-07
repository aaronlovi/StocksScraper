using System;
using Stocks.DataModels.Enums;
using Stocks.Shared;

namespace Stocks.DataModels;

public record Submission(
    ulong SubmissionId,
    ulong CompanyId,
    string FilingReference,
    FilingType FilingType,
    FilingCategory FilingCategory,
    DateOnly ReportDate,
    DateTime? AcceptanceTime)
{
    public DateTime ReportTime => ReportDate.AsUtcTime();
}
