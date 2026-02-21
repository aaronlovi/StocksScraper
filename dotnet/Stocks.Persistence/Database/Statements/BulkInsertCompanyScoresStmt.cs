using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels.Scoring;

namespace Stocks.Persistence.Database.Statements;

internal sealed class BulkInsertCompanyScoresStmt : BulkInsertDbStmtBase<CompanyScoreSummary> {
    public BulkInsertCompanyScoresStmt(IReadOnlyCollection<CompanyScoreSummary> scores)
        : base(nameof(BulkInsertCompanyScoresStmt), scores) { }

    protected override string GetCopyCommand() => "COPY company_scores"
        + " (company_id, cik, company_name, ticker, exchange,"
        + " overall_score, computable_checks, years_of_data,"
        + " book_value, market_cap, debt_to_equity_ratio,"
        + " price_to_book_ratio, debt_to_book_ratio,"
        + " adjusted_retained_earnings, average_net_cash_flow,"
        + " average_owner_earnings, estimated_return_cf, estimated_return_oe,"
        + " price_per_share, price_date, shares_outstanding,"
        + " current_dividends_paid, max_buy_price, percentage_upside, computed_at)"
        + " FROM STDIN (FORMAT BINARY)";

    protected override async Task WriteItemAsync(NpgsqlBinaryImporter writer, CompanyScoreSummary s) {
        await writer.WriteAsync(unchecked((long)s.CompanyId), NpgsqlDbType.Bigint);
        await writer.WriteAsync(long.Parse(s.Cik), NpgsqlDbType.Bigint);
        await writer.WriteNullableAsync(s.CompanyName, NpgsqlDbType.Varchar);
        await writer.WriteNullableAsync(s.Ticker, NpgsqlDbType.Varchar);
        await writer.WriteNullableAsync(s.Exchange, NpgsqlDbType.Varchar);
        await writer.WriteAsync(s.OverallScore, NpgsqlDbType.Integer);
        await writer.WriteAsync(s.ComputableChecks, NpgsqlDbType.Integer);
        await writer.WriteAsync(s.YearsOfData, NpgsqlDbType.Integer);
        await writer.WriteNullableAsync(s.BookValue, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.MarketCap, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.DebtToEquityRatio, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.PriceToBookRatio, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.DebtToBookRatio, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.AdjustedRetainedEarnings, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.AverageNetCashFlow, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.AverageOwnerEarnings, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.EstimatedReturnCF, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.EstimatedReturnOE, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.PricePerShare, NpgsqlDbType.Numeric);
        if (s.PriceDate.HasValue)
            await writer.WriteAsync(s.PriceDate.Value.ToDateTime(TimeOnly.MinValue), NpgsqlDbType.Date);
        else
            await writer.WriteNullAsync();
        await writer.WriteNullableAsync(s.SharesOutstanding, NpgsqlDbType.Bigint);
        await writer.WriteNullableAsync(s.CurrentDividendsPaid, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.MaxBuyPrice, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.PercentageUpside, NpgsqlDbType.Numeric);
        await writer.WriteAsync(s.ComputedAt, NpgsqlDbType.TimestampTz);
    }
}
