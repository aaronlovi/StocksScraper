using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Persistence.Services;

namespace Stocks.EDGARScraper.Tests.Scoring;

/// <summary>
/// E13: a line that appears only in a filing's comparative columns carries an earlier
/// period's amount and is not a fact of that filing's own period. Only facts ending no
/// more than 30 days before the report date are scored.
/// </summary>
public class FilingPeriodFactTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;
    private static readonly DataPointUnit Usd = new(1, "usd");
    private static readonly DataPointUnit Shares = new(2, "shares");
    private static readonly string[] Concepts = [
        "ProceedsFromIssuanceOfSeniorLongTermDebt", "Goodwill", "EntityCommonStockSharesOutstanding", "StockholdersEquity",
    ];

    private const ulong TenKId = 100;
    private const ulong TenQId = 101;
    private const long SeniorProceeds = 10;
    private const long Goodwill = 11;
    private const long CoverShares = 12;
    private const long Equity = 13;
    private static readonly DateOnly TenKDate = new(2024, 12, 31);
    private static readonly DateOnly TenQDate = new(2025, 6, 30);

    private async Task Seed(params DataPoint[] points) {
        await _dbm.BulkInsertTaxonomyConcepts([
            new ConceptDetailsDTO(SeniorProceeds, 1, 2, 2, false, "ProceedsFromIssuanceOfSeniorLongTermDebt", "Senior proceeds", ""),
            new ConceptDetailsDTO(Goodwill, 1, 1, 2, false, "Goodwill", "Goodwill", ""),
            new ConceptDetailsDTO(CoverShares, 1, 1, 0, false, "EntityCommonStockSharesOutstanding", "Cover shares", ""),
            new ConceptDetailsDTO(Equity, 1, 1, 1, false, "StockholdersEquity", "Equity", ""),
        ], _ct);
        await _dbm.BulkInsertCompanies([new Company(1, 111, "EDGAR")], _ct);
        await _dbm.BulkInsertSubmissions([
            new Submission(TenKId, 1, "10-K", FilingType.TenK, FilingCategory.Annual, TenKDate,
                new DateTime(2025, 2, 1, 12, 0, 0, DateTimeKind.Utc)),
            new Submission(TenQId, 1, "10-Q", FilingType.TenQ, FilingCategory.Quarterly, TenQDate,
                new DateTime(2025, 8, 1, 12, 0, 0, DateTimeKind.Utc)),
        ], _ct);
        await _dbm.BulkInsertDataPoints([.. points], _ct);
    }

    private static DataPoint Flow(ulong id, ulong submissionId, decimal value, DateOnly start, DateOnly end) =>
        new(id, 1, "fact", "ref", new DatePair(start, end), value, Usd, end, submissionId, SeniorProceeds);

    private static DataPoint Instant(ulong id, ulong submissionId, long conceptId, decimal value, DateOnly date) =>
        new(id, 1, "fact", "ref", new DatePair(date, date), value,
            conceptId == CoverShares ? Shares : Usd, date, submissionId, conceptId);

    /// <summary>Values of one concept at one report date, from the single-company fetch.</summary>
    private List<decimal> Single(string concept, DateOnly reportDate) {
        var values = new List<decimal>();
        foreach (ScoringConceptValue v in _dbm.GetInMemoryData().GetScoringDataPoints(1, Concepts)) {
            if (v.ConceptName == concept && v.ReportDate == reportDate)
                values.Add(v.Value);
        }
        return values;
    }

    /// <summary>The same from the batch fetch (live, or as of a date).</summary>
    private List<decimal> Batch(string concept, DateOnly reportDate, DateOnly? asOf = null) {
        var values = new List<decimal>();
        foreach (BatchScoringConceptValue v in _dbm.GetInMemoryData().GetAllScoringDataPoints(Concepts, 5, asOf)) {
            if (v.ConceptName == concept && v.ReportDate == reportDate)
                values.Add(v.Value);
        }
        return values;
    }

    [Fact]
    public async Task TenK_FlowOnlyInPriorYearColumn_Ignored() {
        // UFPI-shaped: the FY2024 10-K reports senior notes for 2023 only.
        await Seed(
            Flow(1, TenKId, 150m, new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31)),
            Instant(2, TenKId, Equity, 1000m, TenKDate));

        Assert.Empty(Single("ProceedsFromIssuanceOfSeniorLongTermDebt", TenKDate));
        Assert.Empty(Batch("ProceedsFromIssuanceOfSeniorLongTermDebt", TenKDate));
        Assert.Empty(Batch("ProceedsFromIssuanceOfSeniorLongTermDebt", TenKDate, new DateOnly(2025, 12, 31)));
        Assert.Equal([1000m], Single("StockholdersEquity", TenKDate));
        Assert.Equal([1000m], Batch("StockholdersEquity", TenKDate));
    }

    [Fact]
    public async Task TenK_BalanceOnlyInPriorYearColumn_Ignored() {
        await Seed(
            Instant(1, TenKId, Goodwill, 80m, new DateOnly(2023, 12, 31)),
            Instant(2, TenKId, Equity, 1000m, TenKDate));

        Assert.Empty(Single("Goodwill", TenKDate));
        Assert.Empty(Batch("Goodwill", TenKDate));
    }

    [Fact]
    public async Task TenQ_FlowOnlyInPriorYearColumn_Ignored() {
        await Seed(
            Flow(1, TenQId, 150m, new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30)),
            Instant(2, TenQId, Equity, 1100m, TenQDate));

        Assert.Empty(Single("ProceedsFromIssuanceOfSeniorLongTermDebt", TenQDate));
        Assert.Empty(Batch("ProceedsFromIssuanceOfSeniorLongTermDebt", TenQDate));
    }

    [Fact]
    public async Task TenQ_BalanceOnlyInYearEndColumn_Ignored() {
        await Seed(
            Instant(1, TenQId, Goodwill, 80m, TenKDate),
            Instant(2, TenQId, Equity, 1100m, TenQDate));

        Assert.Empty(Single("Goodwill", TenQDate));
        Assert.Empty(Batch("Goodwill", TenQDate));
    }

    [Fact]
    public async Task CurrentAndPriorColumns_CurrentPicked() {
        await Seed(
            Flow(1, TenKId, 150m, new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31)),
            Flow(2, TenKId, 40m, new DateOnly(2024, 1, 1), TenKDate));

        Assert.Equal([40m], Single("ProceedsFromIssuanceOfSeniorLongTermDebt", TenKDate));
        Assert.Equal([40m], Batch("ProceedsFromIssuanceOfSeniorLongTermDebt", TenKDate));
    }

    [Fact]
    public async Task CoverPageSharesDatedAfterReportDate_Kept() {
        await Seed(Instant(1, TenKId, CoverShares, 77m, new DateOnly(2025, 2, 14)));

        Assert.Equal([77m], Single("EntityCommonStockSharesOutstanding", TenKDate));
        Assert.Equal([77m], Batch("EntityCommonStockSharesOutstanding", TenKDate));
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public async Task FactEndingBeforeReportDate_KeptUpToThirtyDays(int daysEarly, bool kept) {
        await Seed(
            Instant(1, TenKId, Goodwill, 80m, TenKDate.AddDays(-daysEarly)),
            Instant(2, TenKId, Equity, 1000m, TenKDate));

        List<decimal> expected = kept ? [80m] : [];
        Assert.Equal(expected, Single("Goodwill", TenKDate));
        Assert.Equal(expected, Batch("Goodwill", TenKDate));
    }

    [Fact]
    public async Task TenQWithOnlyAStaleBalance_FilledFromLatestTenK() {
        // E9 fills the gap from the 10-K's own balance, not from an older column of the 10-Q.
        await Seed(
            Instant(1, TenKId, Goodwill, 80m, TenKDate),
            Instant(2, TenKId, Equity, 1000m, TenKDate),
            Instant(3, TenQId, Goodwill, 55m, new DateOnly(2024, 6, 30)),
            Instant(4, TenQId, Equity, 1100m, TenQDate));

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(
            _dbm.GetInMemoryData().GetScoringDataPoints(1, Concepts));

        Assert.Equal(80m, grouped.MostRecentSnapshot["Goodwill"]);
        Assert.Equal(1100m, grouped.MostRecentSnapshot["StockholdersEquity"]);
    }
}
