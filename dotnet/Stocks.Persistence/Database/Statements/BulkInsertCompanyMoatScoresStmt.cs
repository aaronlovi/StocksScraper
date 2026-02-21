using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels.Scoring;

namespace Stocks.Persistence.Database.Statements;

internal sealed class BulkInsertCompanyMoatScoresStmt : BulkInsertDbStmtBase<CompanyMoatScoreSummary> {
    public BulkInsertCompanyMoatScoresStmt(IReadOnlyCollection<CompanyMoatScoreSummary> scores)
        : base(nameof(BulkInsertCompanyMoatScoresStmt), scores) { }

    protected override string GetCopyCommand() => "COPY company_moat_scores"
        + " (company_id, cik, company_name, ticker, exchange,"
        + " overall_score, computable_checks, years_of_data,"
        + " average_gross_margin, average_operating_margin,"
        + " average_roe_cf, average_roe_oe, estimated_return_oe,"
        + " revenue_cagr, capex_ratio, interest_coverage,"
        + " debt_to_equity_ratio, price_per_share, price_date,"
        + " shares_outstanding, computed_at)"
        + " FROM STDIN (FORMAT BINARY)";

    protected override async Task WriteItemAsync(NpgsqlBinaryImporter writer, CompanyMoatScoreSummary s) {
        await writer.WriteAsync(unchecked((long)s.CompanyId), NpgsqlDbType.Bigint);
        await writer.WriteAsync(long.Parse(s.Cik), NpgsqlDbType.Bigint);
        await writer.WriteNullableAsync(s.CompanyName, NpgsqlDbType.Varchar);
        await writer.WriteNullableAsync(s.Ticker, NpgsqlDbType.Varchar);
        await writer.WriteNullableAsync(s.Exchange, NpgsqlDbType.Varchar);
        await writer.WriteAsync(s.OverallScore, NpgsqlDbType.Integer);
        await writer.WriteAsync(s.ComputableChecks, NpgsqlDbType.Integer);
        await writer.WriteAsync(s.YearsOfData, NpgsqlDbType.Integer);
        await writer.WriteNullableAsync(s.AverageGrossMargin, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.AverageOperatingMargin, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.AverageRoeCF, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.AverageRoeOE, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.EstimatedReturnOE, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.RevenueCagr, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.CapexRatio, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.InterestCoverage, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.DebtToEquityRatio, NpgsqlDbType.Numeric);
        await writer.WriteNullableAsync(s.PricePerShare, NpgsqlDbType.Numeric);
        if (s.PriceDate.HasValue)
            await writer.WriteAsync(s.PriceDate.Value.ToDateTime(TimeOnly.MinValue), NpgsqlDbType.Date);
        else
            await writer.WriteNullAsync();
        await writer.WriteNullableAsync(s.SharesOutstanding, NpgsqlDbType.Bigint);
        await writer.WriteAsync(s.ComputedAt, NpgsqlDbType.TimestampTz);
    }
}
