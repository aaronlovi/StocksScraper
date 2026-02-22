using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetCompanyScoresStmt : QueryDbStmtBase {
    private readonly PaginationRequest _pagination;
    private readonly ScoresSortBy _sortBy;
    private readonly SortDirection _sortDir;
    private readonly ScoresFilter? _filter;
    private readonly List<CompanyScoreSummary> _results = [];

    private int _companyIdIndex = -1;
    private int _cikIndex = -1;
    private int _companyNameIndex = -1;
    private int _tickerIndex = -1;
    private int _exchangeIndex = -1;
    private int _overallScoreIndex = -1;
    private int _computableChecksIndex = -1;
    private int _yearsOfDataIndex = -1;
    private int _bookValueIndex = -1;
    private int _marketCapIndex = -1;
    private int _debtToEquityRatioIndex = -1;
    private int _priceToBookRatioIndex = -1;
    private int _debtToBookRatioIndex = -1;
    private int _adjustedRetainedEarningsIndex = -1;
    private int _averageNetCashFlowIndex = -1;
    private int _averageOwnerEarningsIndex = -1;
    private int _averageRoeCFIndex = -1;
    private int _averageRoeOEIndex = -1;
    private int _estimatedReturnCfIndex = -1;
    private int _estimatedReturnOeIndex = -1;
    private int _pricePerShareIndex = -1;
    private int _priceDateIndex = -1;
    private int _sharesOutstandingIndex = -1;
    private int _currentDividendsPaidIndex = -1;
    private int _maxBuyPriceIndex = -1;
    private int _percentageUpsideIndex = -1;
    private int _computedAtIndex = -1;
    private int _totalCountIndex = -1;

    public GetCompanyScoresStmt(PaginationRequest pagination, ScoresSortBy sortBy,
        SortDirection sortDir, ScoresFilter? filter)
        : base(BuildSql(sortBy, sortDir, filter), nameof(GetCompanyScoresStmt)) {
        _pagination = pagination;
        _sortBy = sortBy;
        _sortDir = sortDir;
        _filter = filter;
    }

    public IReadOnlyCollection<CompanyScoreSummary> Results => _results;
    public PaginationResponse PaginationResponse { get; private set; } = PaginationResponse.Empty;

    public PagedResults<CompanyScoreSummary> GetPagedResults() => new(_results, PaginationResponse);

    private static string BuildSql(ScoresSortBy sortBy, SortDirection sortDir, ScoresFilter? filter) {
        string orderColumn = sortBy switch {
            ScoresSortBy.BookValue => "book_value",
            ScoresSortBy.MarketCap => "market_cap",
            ScoresSortBy.EstimatedReturnCF => "estimated_return_cf",
            ScoresSortBy.EstimatedReturnOE => "estimated_return_oe",
            ScoresSortBy.DebtToEquityRatio => "debt_to_equity_ratio",
            ScoresSortBy.PriceToBookRatio => "price_to_book_ratio",
            ScoresSortBy.MaxBuyPrice => "max_buy_price",
            ScoresSortBy.PercentageUpside => "percentage_upside",
            ScoresSortBy.AverageRoeCF => "average_roe_cf",
            ScoresSortBy.AverageRoeOE => "average_roe_oe",
            _ => "overall_score",
        };

        string direction = sortDir == SortDirection.Ascending ? "ASC" : "DESC";
        string nullsPosition = sortDir == SortDirection.Ascending ? "NULLS LAST" : "NULLS LAST";

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
    book_value, market_cap, debt_to_equity_ratio,
    price_to_book_ratio, debt_to_book_ratio,
    adjusted_retained_earnings, average_net_cash_flow,
    average_owner_earnings, average_roe_cf, average_roe_oe,
    estimated_return_cf, estimated_return_oe,
    price_per_share, price_date, shares_outstanding,
    current_dividends_paid, max_buy_price, percentage_upside, computed_at,
    COUNT(*) OVER() AS total_count
FROM company_scores
{whereClause}
ORDER BY {orderColumn} {direction} {nullsPosition}, company_id ASC
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
        _bookValueIndex = reader.GetOrdinal("book_value");
        _marketCapIndex = reader.GetOrdinal("market_cap");
        _debtToEquityRatioIndex = reader.GetOrdinal("debt_to_equity_ratio");
        _priceToBookRatioIndex = reader.GetOrdinal("price_to_book_ratio");
        _debtToBookRatioIndex = reader.GetOrdinal("debt_to_book_ratio");
        _adjustedRetainedEarningsIndex = reader.GetOrdinal("adjusted_retained_earnings");
        _averageNetCashFlowIndex = reader.GetOrdinal("average_net_cash_flow");
        _averageOwnerEarningsIndex = reader.GetOrdinal("average_owner_earnings");
        _averageRoeCFIndex = reader.GetOrdinal("average_roe_cf");
        _averageRoeOEIndex = reader.GetOrdinal("average_roe_oe");
        _estimatedReturnCfIndex = reader.GetOrdinal("estimated_return_cf");
        _estimatedReturnOeIndex = reader.GetOrdinal("estimated_return_oe");
        _pricePerShareIndex = reader.GetOrdinal("price_per_share");
        _priceDateIndex = reader.GetOrdinal("price_date");
        _sharesOutstandingIndex = reader.GetOrdinal("shares_outstanding");
        _currentDividendsPaidIndex = reader.GetOrdinal("current_dividends_paid");
        _maxBuyPriceIndex = reader.GetOrdinal("max_buy_price");
        _percentageUpsideIndex = reader.GetOrdinal("percentage_upside");
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

        var result = new CompanyScoreSummary(
            (ulong)reader.GetInt64(_companyIdIndex),
            reader.GetInt64(_cikIndex).ToString(),
            reader.IsDBNull(_companyNameIndex) ? null : reader.GetString(_companyNameIndex),
            reader.IsDBNull(_tickerIndex) ? null : reader.GetString(_tickerIndex),
            reader.IsDBNull(_exchangeIndex) ? null : reader.GetString(_exchangeIndex),
            reader.GetInt32(_overallScoreIndex),
            reader.GetInt32(_computableChecksIndex),
            reader.GetInt32(_yearsOfDataIndex),
            reader.IsDBNull(_bookValueIndex) ? null : reader.GetDecimal(_bookValueIndex),
            reader.IsDBNull(_marketCapIndex) ? null : reader.GetDecimal(_marketCapIndex),
            reader.IsDBNull(_debtToEquityRatioIndex) ? null : reader.GetDecimal(_debtToEquityRatioIndex),
            reader.IsDBNull(_priceToBookRatioIndex) ? null : reader.GetDecimal(_priceToBookRatioIndex),
            reader.IsDBNull(_debtToBookRatioIndex) ? null : reader.GetDecimal(_debtToBookRatioIndex),
            reader.IsDBNull(_adjustedRetainedEarningsIndex) ? null : reader.GetDecimal(_adjustedRetainedEarningsIndex),
            reader.IsDBNull(_averageNetCashFlowIndex) ? null : reader.GetDecimal(_averageNetCashFlowIndex),
            reader.IsDBNull(_averageOwnerEarningsIndex) ? null : reader.GetDecimal(_averageOwnerEarningsIndex),
            reader.IsDBNull(_averageRoeCFIndex) ? null : reader.GetDecimal(_averageRoeCFIndex),
            reader.IsDBNull(_averageRoeOEIndex) ? null : reader.GetDecimal(_averageRoeOEIndex),
            reader.IsDBNull(_estimatedReturnCfIndex) ? null : reader.GetDecimal(_estimatedReturnCfIndex),
            reader.IsDBNull(_estimatedReturnOeIndex) ? null : reader.GetDecimal(_estimatedReturnOeIndex),
            reader.IsDBNull(_pricePerShareIndex) ? null : reader.GetDecimal(_pricePerShareIndex),
            reader.IsDBNull(_priceDateIndex) ? null : DateOnly.FromDateTime(reader.GetDateTime(_priceDateIndex)),
            reader.IsDBNull(_sharesOutstandingIndex) ? null : reader.GetInt64(_sharesOutstandingIndex),
            reader.IsDBNull(_currentDividendsPaidIndex) ? null : reader.GetDecimal(_currentDividendsPaidIndex),
            reader.IsDBNull(_maxBuyPriceIndex) ? null : reader.GetDecimal(_maxBuyPriceIndex),
            reader.IsDBNull(_percentageUpsideIndex) ? null : reader.GetDecimal(_percentageUpsideIndex),
            reader.GetDateTime(_computedAtIndex));
        _results.Add(result);
        return true;
    }
}
