using System;
using System.Collections.Generic;

namespace Stocks.DataModels;

public record DashboardStats(
    long TotalCompanies,
    long TotalSubmissions,
    long TotalDataPoints,
    DateOnly? EarliestFilingDate,
    DateOnly? LatestFilingDate,
    long CompaniesWithPriceData,
    IReadOnlyDictionary<string, long> SubmissionsByFilingType
);
