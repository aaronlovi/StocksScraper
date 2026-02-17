using System;
using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetDashboardStatsStmt : QueryDbStmtBase {
    private const string sql = @"
WITH company_count AS (SELECT COUNT(*) AS cnt FROM companies),
     submission_count AS (SELECT COUNT(*) AS cnt FROM submissions WHERE report_date <= CURRENT_DATE),
     datapoint_count AS (SELECT COUNT(*) AS cnt FROM data_points),
     filing_dates AS (SELECT MIN(report_date) AS earliest, MAX(report_date) AS latest FROM submissions WHERE report_date <= CURRENT_DATE),
     price_companies AS (SELECT COUNT(DISTINCT ticker) AS cnt FROM price_imports),
     submissions_by_type AS (
         SELECT ft.filing_type_name AS filing_type, COUNT(*) AS cnt
         FROM submissions s
         JOIN filing_types ft ON ft.filing_type_id = s.filing_type
         WHERE s.report_date <= CURRENT_DATE
         GROUP BY ft.filing_type_name
     )
SELECT cc.cnt AS total_companies,
       sc.cnt AS total_submissions,
       dc.cnt AS total_data_points,
       fd.earliest,
       fd.latest,
       pc.cnt AS companies_with_price_data,
       sbt.filing_type,
       sbt.cnt AS filing_type_count
FROM company_count cc
CROSS JOIN submission_count sc
CROSS JOIN datapoint_count dc
CROSS JOIN filing_dates fd
CROSS JOIN price_companies pc
LEFT JOIN submissions_by_type sbt ON true";

    // Outputs
    private long _totalCompanies;
    private long _totalSubmissions;
    private long _totalDataPoints;
    private DateOnly? _earliestFilingDate;
    private DateOnly? _latestFilingDate;
    private long _companiesWithPriceData;
    private readonly Dictionary<string, long> _submissionsByFilingType = new();

    private static int _totalCompaniesIndex = -1;
    private static int _totalSubmissionsIndex = -1;
    private static int _totalDataPointsIndex = -1;
    private static int _earliestIndex = -1;
    private static int _latestIndex = -1;
    private static int _companiesWithPriceDataIndex = -1;
    private static int _filingTypeIndex = -1;
    private static int _filingTypeCountIndex = -1;

    public GetDashboardStatsStmt() : base(sql, nameof(GetDashboardStatsStmt)) { }

    public DashboardStats Stats => new(
        _totalCompanies,
        _totalSubmissions,
        _totalDataPoints,
        _earliestFilingDate,
        _latestFilingDate,
        _companiesWithPriceData,
        _submissionsByFilingType);

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);

        if (_totalCompaniesIndex != -1)
            return;

        _totalCompaniesIndex = reader.GetOrdinal("total_companies");
        _totalSubmissionsIndex = reader.GetOrdinal("total_submissions");
        _totalDataPointsIndex = reader.GetOrdinal("total_data_points");
        _earliestIndex = reader.GetOrdinal("earliest");
        _latestIndex = reader.GetOrdinal("latest");
        _companiesWithPriceDataIndex = reader.GetOrdinal("companies_with_price_data");
        _filingTypeIndex = reader.GetOrdinal("filing_type");
        _filingTypeCountIndex = reader.GetOrdinal("filing_type_count");
    }

    protected override void ClearResults() {
        _totalCompanies = 0;
        _totalSubmissions = 0;
        _totalDataPoints = 0;
        _earliestFilingDate = null;
        _latestFilingDate = null;
        _companiesWithPriceData = 0;
        _submissionsByFilingType.Clear();
    }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        _totalCompanies = reader.GetInt64(_totalCompaniesIndex);
        _totalSubmissions = reader.GetInt64(_totalSubmissionsIndex);
        _totalDataPoints = reader.GetInt64(_totalDataPointsIndex);

        if (!reader.IsDBNull(_earliestIndex))
            _earliestFilingDate = DateOnly.FromDateTime(reader.GetDateTime(_earliestIndex));
        if (!reader.IsDBNull(_latestIndex))
            _latestFilingDate = DateOnly.FromDateTime(reader.GetDateTime(_latestIndex));

        _companiesWithPriceData = reader.GetInt64(_companiesWithPriceDataIndex);

        if (!reader.IsDBNull(_filingTypeIndex)) {
            string filingType = reader.GetString(_filingTypeIndex);
            long count = reader.GetInt64(_filingTypeCountIndex);
            _submissionsByFilingType[filingType] = count;
        }

        return true;
    }
}
