using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class SearchCompaniesStmt : QueryDbStmtBase {
    private const string sql = @"
WITH matches AS (
    SELECT DISTINCT ON (c.company_id)
        c.company_id,
        c.cik,
        cn.name AS company_name,
        ct.ticker,
        ct.exchange,
        GREATEST(
            COALESCE(similarity(cn.name, @query), 0),
            COALESCE(similarity(ct.ticker, @query), 0),
            CASE WHEN c.cik::text = @query THEN 1.0 ELSE 0 END
        ) AS rank
    FROM companies c
    JOIN company_names cn ON cn.company_id = c.company_id
    LEFT JOIN company_tickers ct ON ct.company_id = c.company_id
    WHERE cn.name % @query OR ct.ticker % @query OR c.cik::text = @query
    ORDER BY c.company_id, GREATEST(
        COALESCE(similarity(cn.name, @query), 0),
        COALESCE(similarity(ct.ticker, @query), 0),
        CASE WHEN c.cik::text = @query THEN 1.0 ELSE 0 END
    ) DESC
)
SELECT m.company_id, m.cik, m.company_name, m.ticker, m.exchange, m.rank,
    COUNT(*) OVER() AS total_count,
    lp.latest_price, lp.latest_price_date
FROM matches m
LEFT JOIN LATERAL (
    SELECT p.close AS latest_price, p.price_date AS latest_price_date
    FROM prices p
    WHERE p.ticker = m.ticker
    ORDER BY p.price_date DESC
    LIMIT 1
) lp ON true
ORDER BY rank DESC, company_name ASC
LIMIT @limit OFFSET @offset;
";

    private readonly string _query;
    private readonly PaginationRequest _pagination;
    private readonly List<CompanySearchResult> _results;

    private static int _companyIdIndex = -1;
    private static int _cikIndex = -1;
    private static int _companyNameIndex = -1;
    private static int _tickerIndex = -1;
    private static int _exchangeIndex = -1;
    private static int _totalCountIndex = -1;
    private static int _latestPriceIndex = -1;
    private static int _latestPriceDateIndex = -1;

    public SearchCompaniesStmt(string query, PaginationRequest pagination)
        : base(sql, nameof(SearchCompaniesStmt)) {
        _query = query;
        _pagination = pagination;
        _results = [];
        PaginationResponse = PaginationResponse.Empty;
    }

    public IReadOnlyCollection<CompanySearchResult> Results => _results;
    public PaginationResponse PaginationResponse { get; private set; }

    public PagedResults<CompanySearchResult> GetPagedResults() => new(_results, PaginationResponse);

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);

        if (_companyIdIndex != -1)
            return;

        _companyIdIndex = reader.GetOrdinal("company_id");
        _cikIndex = reader.GetOrdinal("cik");
        _companyNameIndex = reader.GetOrdinal("company_name");
        _tickerIndex = reader.GetOrdinal("ticker");
        _exchangeIndex = reader.GetOrdinal("exchange");
        _totalCountIndex = reader.GetOrdinal("total_count");
        _latestPriceIndex = reader.GetOrdinal("latest_price");
        _latestPriceDateIndex = reader.GetOrdinal("latest_price_date");
    }

    protected override void ClearResults() {
        _results.Clear();
        PaginationResponse = PaginationResponse.Empty;
    }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [
        new NpgsqlParameter<string>("query", _query),
        new NpgsqlParameter<int>("limit", (int)_pagination.PageSize) { NpgsqlDbType = NpgsqlDbType.Integer },
        new NpgsqlParameter<int>("offset", (int)((_pagination.PageNumber - 1) * _pagination.PageSize)) { NpgsqlDbType = NpgsqlDbType.Integer }
    ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        if (_results.Count == 0) {
            uint totalItems = (uint)reader.GetInt64(_totalCountIndex);
            uint totalPages = (uint)Math.Ceiling(totalItems / (double)_pagination.PageSize);
            PaginationResponse = new PaginationResponse(_pagination.PageNumber, totalItems, totalPages);
        }

        string? ticker = reader.IsDBNull(_tickerIndex) ? null : reader.GetString(_tickerIndex);
        string? exchange = reader.IsDBNull(_exchangeIndex) ? null : reader.GetString(_exchangeIndex);
        decimal? latestPrice = reader.IsDBNull(_latestPriceIndex) ? null : reader.GetDecimal(_latestPriceIndex);
        DateOnly? latestPriceDate = reader.IsDBNull(_latestPriceDateIndex) ? null : DateOnly.FromDateTime(reader.GetDateTime(_latestPriceDateIndex));

        var result = new CompanySearchResult(
            (ulong)reader.GetInt64(_companyIdIndex),
            reader.GetInt64(_cikIndex).ToString(),
            reader.GetString(_companyNameIndex),
            ticker,
            exchange,
            latestPrice,
            latestPriceDate);
        _results.Add(result);
        return true;
    }
}
