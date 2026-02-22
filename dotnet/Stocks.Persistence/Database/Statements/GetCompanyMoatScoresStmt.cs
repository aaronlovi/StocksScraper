using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetCompanyMoatScoresStmt : QueryDbStmtBase {
    private readonly PaginationRequest _pagination;
    private readonly MoatScoresSortBy _sortBy;
    private readonly SortDirection _sortDir;
    private readonly ScoresFilter? _filter;
    private readonly List<CompanyMoatScoreSummary> _results = [];

    private int _companyIdIndex = -1;
    private int _cikIndex = -1;
    private int _companyNameIndex = -1;
    private int _tickerIndex = -1;
    private int _exchangeIndex = -1;
    private int _overallScoreIndex = -1;
    private int _computableChecksIndex = -1;
    private int _yearsOfDataIndex = -1;
    private int _averageGrossMarginIndex = -1;
    private int _averageOperatingMarginIndex = -1;
    private int _averageRoeCFIndex = -1;
    private int _averageRoeOEIndex = -1;
    private int _estimatedReturnOeIndex = -1;
    private int _revenueCagrIndex = -1;
    private int _capexRatioIndex = -1;
    private int _interestCoverageIndex = -1;
    private int _debtToEquityRatioIndex = -1;
    private int _pricePerShareIndex = -1;
    private int _priceDateIndex = -1;
    private int _sharesOutstandingIndex = -1;
    private int _return1yIndex = -1;
    private int _computedAtIndex = -1;
    private int _totalCountIndex = -1;

    public GetCompanyMoatScoresStmt(PaginationRequest pagination, MoatScoresSortBy sortBy,
        SortDirection sortDir, ScoresFilter? filter)
        : base(BuildSql(sortBy, sortDir, filter), nameof(GetCompanyMoatScoresStmt)) {
        _pagination = pagination;
        _sortBy = sortBy;
        _sortDir = sortDir;
        _filter = filter;
    }

    public IReadOnlyCollection<CompanyMoatScoreSummary> Results => _results;
    public PaginationResponse PaginationResponse { get; private set; } = PaginationResponse.Empty;

    public PagedResults<CompanyMoatScoreSummary> GetPagedResults() => new(_results, PaginationResponse);

    private static string BuildSql(MoatScoresSortBy sortBy, SortDirection sortDir, ScoresFilter? filter) {
        string orderColumn = sortBy switch {
            MoatScoresSortBy.AverageGrossMargin => "average_gross_margin",
            MoatScoresSortBy.AverageOperatingMargin => "average_operating_margin",
            MoatScoresSortBy.AverageRoeCF => "average_roe_cf",
            MoatScoresSortBy.AverageRoeOE => "average_roe_oe",
            MoatScoresSortBy.EstimatedReturnOE => "estimated_return_oe",
            MoatScoresSortBy.RevenueCagr => "revenue_cagr",
            MoatScoresSortBy.CapexRatio => "capex_ratio",
            MoatScoresSortBy.InterestCoverage => "interest_coverage",
            MoatScoresSortBy.DebtToEquityRatio => "debt_to_equity_ratio",
            MoatScoresSortBy.Return1y => "return_1y",
            _ => "overall_score",
        };

        string direction = sortDir == SortDirection.Ascending ? "ASC" : "DESC";
        string nullsPosition = "NULLS LAST";

        var whereClauses = new List<string>();
        if (filter is not null) {
            if (filter.MinScore.HasValue)
                whereClauses.Add("overall_score >= @min_score");
            if (filter.MaxScore.HasValue)
                whereClauses.Add("overall_score <= @max_score");
            if (!string.IsNullOrWhiteSpace(filter.Exchange))
                whereClauses.Add("exchange = @exchange");
        }

        string whereClause = whereClauses.Count > 0
            ? "WHERE " + string.Join(" AND ", whereClauses)
            : string.Empty;

        return $@"
SELECT company_id, cik, company_name, ticker, exchange,
    overall_score, computable_checks, years_of_data,
    average_gross_margin, average_operating_margin,
    average_roe_cf, average_roe_oe, estimated_return_oe,
    revenue_cagr, capex_ratio, interest_coverage,
    debt_to_equity_ratio, price_per_share, price_date,
    shares_outstanding, return_1y, computed_at,
    COUNT(*) OVER() AS total_count
FROM company_moat_scores
{whereClause}
ORDER BY {orderColumn} {direction} {nullsPosition}, computable_checks DESC, company_id ASC
LIMIT @limit OFFSET @offset";
    }

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        _companyIdIndex = reader.GetOrdinal("company_id");
        _cikIndex = reader.GetOrdinal("cik");
        _companyNameIndex = reader.GetOrdinal("company_name");
        _tickerIndex = reader.GetOrdinal("ticker");
        _exchangeIndex = reader.GetOrdinal("exchange");
        _overallScoreIndex = reader.GetOrdinal("overall_score");
        _computableChecksIndex = reader.GetOrdinal("computable_checks");
        _yearsOfDataIndex = reader.GetOrdinal("years_of_data");
        _averageGrossMarginIndex = reader.GetOrdinal("average_gross_margin");
        _averageOperatingMarginIndex = reader.GetOrdinal("average_operating_margin");
        _averageRoeCFIndex = reader.GetOrdinal("average_roe_cf");
        _averageRoeOEIndex = reader.GetOrdinal("average_roe_oe");
        _estimatedReturnOeIndex = reader.GetOrdinal("estimated_return_oe");
        _revenueCagrIndex = reader.GetOrdinal("revenue_cagr");
        _capexRatioIndex = reader.GetOrdinal("capex_ratio");
        _interestCoverageIndex = reader.GetOrdinal("interest_coverage");
        _debtToEquityRatioIndex = reader.GetOrdinal("debt_to_equity_ratio");
        _pricePerShareIndex = reader.GetOrdinal("price_per_share");
        _priceDateIndex = reader.GetOrdinal("price_date");
        _sharesOutstandingIndex = reader.GetOrdinal("shares_outstanding");
        _return1yIndex = reader.GetOrdinal("return_1y");
        _computedAtIndex = reader.GetOrdinal("computed_at");
        _totalCountIndex = reader.GetOrdinal("total_count");
    }

    protected override void ClearResults() {
        _results.Clear();
        PaginationResponse = PaginationResponse.Empty;
    }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() {
        var parameters = new List<NpgsqlParameter> {
            new NpgsqlParameter<int>("limit", (int)_pagination.PageSize) { NpgsqlDbType = NpgsqlDbType.Integer },
            new NpgsqlParameter<int>("offset", (int)((_pagination.PageNumber - 1) * _pagination.PageSize)) { NpgsqlDbType = NpgsqlDbType.Integer },
        };

        if (_filter is not null) {
            if (_filter.MinScore.HasValue)
                parameters.Add(new NpgsqlParameter<int>("min_score", _filter.MinScore.Value) { NpgsqlDbType = NpgsqlDbType.Integer });
            if (_filter.MaxScore.HasValue)
                parameters.Add(new NpgsqlParameter<int>("max_score", _filter.MaxScore.Value) { NpgsqlDbType = NpgsqlDbType.Integer });
            if (!string.IsNullOrWhiteSpace(_filter.Exchange))
                parameters.Add(new NpgsqlParameter<string>("exchange", _filter.Exchange));
        }

        return parameters;
    }

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        if (_results.Count == 0) {
            uint totalItems = (uint)reader.GetInt64(_totalCountIndex);
            uint totalPages = (uint)Math.Ceiling(totalItems / (double)_pagination.PageSize);
            PaginationResponse = new PaginationResponse(_pagination.PageNumber, totalItems, totalPages);
        }

        var result = new CompanyMoatScoreSummary(
            (ulong)reader.GetInt64(_companyIdIndex),
            reader.GetInt64(_cikIndex).ToString(),
            reader.IsDBNull(_companyNameIndex) ? null : reader.GetString(_companyNameIndex),
            reader.IsDBNull(_tickerIndex) ? null : reader.GetString(_tickerIndex),
            reader.IsDBNull(_exchangeIndex) ? null : reader.GetString(_exchangeIndex),
            reader.GetInt32(_overallScoreIndex),
            reader.GetInt32(_computableChecksIndex),
            reader.GetInt32(_yearsOfDataIndex),
            reader.IsDBNull(_averageGrossMarginIndex) ? null : reader.GetDecimal(_averageGrossMarginIndex),
            reader.IsDBNull(_averageOperatingMarginIndex) ? null : reader.GetDecimal(_averageOperatingMarginIndex),
            reader.IsDBNull(_averageRoeCFIndex) ? null : reader.GetDecimal(_averageRoeCFIndex),
            reader.IsDBNull(_averageRoeOEIndex) ? null : reader.GetDecimal(_averageRoeOEIndex),
            reader.IsDBNull(_estimatedReturnOeIndex) ? null : reader.GetDecimal(_estimatedReturnOeIndex),
            reader.IsDBNull(_revenueCagrIndex) ? null : reader.GetDecimal(_revenueCagrIndex),
            reader.IsDBNull(_capexRatioIndex) ? null : reader.GetDecimal(_capexRatioIndex),
            reader.IsDBNull(_interestCoverageIndex) ? null : reader.GetDecimal(_interestCoverageIndex),
            reader.IsDBNull(_debtToEquityRatioIndex) ? null : reader.GetDecimal(_debtToEquityRatioIndex),
            reader.IsDBNull(_pricePerShareIndex) ? null : reader.GetDecimal(_pricePerShareIndex),
            reader.IsDBNull(_priceDateIndex) ? null : DateOnly.FromDateTime(reader.GetDateTime(_priceDateIndex)),
            reader.IsDBNull(_sharesOutstandingIndex) ? null : reader.GetInt64(_sharesOutstandingIndex),
            reader.IsDBNull(_return1yIndex) ? null : reader.GetDecimal(_return1yIndex),
            reader.GetDateTime(_computedAtIndex));
        _results.Add(result);
        return true;
    }
}
