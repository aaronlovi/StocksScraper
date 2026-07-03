using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels.Scoring;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetGrahamSnapshotFundamentalsStmt : QueryDbStmtBase {
    private const string Sql = @"
SELECT as_of_date, company_id, years_of_data, book_value, debt_to_equity_ratio,
    average_net_cash_flow, average_owner_earnings, adjusted_retained_earnings,
    average_roe_cf, average_roe_oe, shares_outstanding
FROM graham_score_snapshots
WHERE company_id = ANY(@company_ids)";

    private readonly long[] _companyIds;
    private readonly List<GrahamSnapshotFundamentals> _results = [];

    private int _asOfDateIndex = -1;
    private int _companyIdIndex = -1;
    private int _yearsOfDataIndex = -1;
    private int _bookValueIndex = -1;
    private int _debtToEquityRatioIndex = -1;
    private int _averageNetCashFlowIndex = -1;
    private int _averageOwnerEarningsIndex = -1;
    private int _adjustedRetainedEarningsIndex = -1;
    private int _averageRoeCFIndex = -1;
    private int _averageRoeOEIndex = -1;
    private int _sharesOutstandingIndex = -1;

    public GetGrahamSnapshotFundamentalsStmt(IReadOnlyCollection<ulong> companyIds)
        : base(Sql, nameof(GetGrahamSnapshotFundamentalsStmt)) {
        _companyIds = new long[companyIds.Count];
        int i = 0;
        foreach (ulong id in companyIds)
            _companyIds[i++] = unchecked((long)id);
    }

    public IReadOnlyCollection<GrahamSnapshotFundamentals> Results => _results;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        _asOfDateIndex = reader.GetOrdinal("as_of_date");
        _companyIdIndex = reader.GetOrdinal("company_id");
        _yearsOfDataIndex = reader.GetOrdinal("years_of_data");
        _bookValueIndex = reader.GetOrdinal("book_value");
        _debtToEquityRatioIndex = reader.GetOrdinal("debt_to_equity_ratio");
        _averageNetCashFlowIndex = reader.GetOrdinal("average_net_cash_flow");
        _averageOwnerEarningsIndex = reader.GetOrdinal("average_owner_earnings");
        _adjustedRetainedEarningsIndex = reader.GetOrdinal("adjusted_retained_earnings");
        _averageRoeCFIndex = reader.GetOrdinal("average_roe_cf");
        _averageRoeOEIndex = reader.GetOrdinal("average_roe_oe");
        _sharesOutstandingIndex = reader.GetOrdinal("shares_outstanding");
    }

    protected override void ClearResults() => _results.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [
        new NpgsqlParameter("company_ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = _companyIds },
    ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        var value = new GrahamSnapshotFundamentals(
            DateOnly.FromDateTime(reader.GetDateTime(_asOfDateIndex)),
            (ulong)reader.GetInt64(_companyIdIndex),
            reader.GetInt32(_yearsOfDataIndex),
            reader.IsDBNull(_bookValueIndex) ? null : reader.GetDecimal(_bookValueIndex),
            reader.IsDBNull(_debtToEquityRatioIndex) ? null : reader.GetDecimal(_debtToEquityRatioIndex),
            reader.IsDBNull(_averageNetCashFlowIndex) ? null : reader.GetDecimal(_averageNetCashFlowIndex),
            reader.IsDBNull(_averageOwnerEarningsIndex) ? null : reader.GetDecimal(_averageOwnerEarningsIndex),
            reader.IsDBNull(_adjustedRetainedEarningsIndex) ? null : reader.GetDecimal(_adjustedRetainedEarningsIndex),
            reader.IsDBNull(_averageRoeCFIndex) ? null : reader.GetDecimal(_averageRoeCFIndex),
            reader.IsDBNull(_averageRoeOEIndex) ? null : reader.GetDecimal(_averageRoeOEIndex),
            reader.IsDBNull(_sharesOutstandingIndex) ? null : reader.GetInt64(_sharesOutstandingIndex));
        _results.Add(value);
        return true;
    }
}
