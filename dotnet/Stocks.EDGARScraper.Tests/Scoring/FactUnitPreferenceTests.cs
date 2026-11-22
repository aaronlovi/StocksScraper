using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;

namespace Stocks.EDGARScraper.Tests.Scoring;

/// <summary>
/// E10: when a filing tags the same concept and period in several units, the scoring
/// fetch prefers USD (or shares) and otherwise breaks the tie by the lowest id.
/// </summary>
public class FactUnitPreferenceTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;
    private static readonly DataPointUnit Usd = new(1, "usd");
    private static readonly DataPointUnit Cad = new(2, "cad");
    private static readonly DataPointUnit Cny = new(3, "cny");
    private static readonly string[] Concepts = ["Revenues"];

    private async Task Seed(params DataPoint[] points) {
        await _dbm.BulkInsertTaxonomyConcepts([new ConceptDetailsDTO(10, 1, 1, 1, false, "Revenues", "Revenues", "")], _ct);
        await _dbm.BulkInsertCompanies([new Company(1, 111, "EDGAR")], _ct);
        await _dbm.BulkInsertSubmissions([
            new Submission(100, 1, "ref", FilingType.TenK, FilingCategory.Annual, new DateOnly(2024, 12, 31), null),
        ], _ct);
        await _dbm.BulkInsertDataPoints([.. points], _ct);
    }

    private static DataPoint Fact(ulong id, decimal value, DataPointUnit unit, DateOnly start, DateOnly end) =>
        new(id, 1, "revenues", "ref", new DatePair(start, end), value, unit, new DateOnly(2025, 2, 1), 100, 10);

    private static readonly DateOnly Jan1 = new(2024, 1, 1);
    private static readonly DateOnly Dec31 = new(2024, 12, 31);

    private decimal SingleValue() {
        IReadOnlyCollection<ScoringConceptValue> values = _dbm.GetInMemoryData().GetScoringDataPoints(1, Concepts);
        return Assert.Single(values).Value;
    }

    private decimal BatchValue() {
        IReadOnlyCollection<BatchScoringConceptValue> values = _dbm.GetInMemoryData().GetAllScoringDataPoints(Concepts);
        return Assert.Single(values).Value;
    }

    [Fact]
    public async Task ForeignCurrencyFirst_UsdPicked() {
        await Seed(Fact(1, 500m, Cad, Jan1, Dec31), Fact(2, 1496m, Usd, Jan1, Dec31));
        Assert.Equal(1496m, SingleValue());
        Assert.Equal(1496m, BatchValue());
    }

    [Fact]
    public async Task TwoForeignCurrencies_LowestIdPicked() {
        await Seed(Fact(7, 70m, Cny, Jan1, Dec31), Fact(5, 50m, Cad, Jan1, Dec31));
        Assert.Equal(50m, SingleValue());
        Assert.Equal(50m, BatchValue());
    }

    [Fact]
    public async Task LaterEndDateStillWinsOverUnit() {
        await Seed(Fact(1, 900m, Usd, new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31)),
            Fact(2, 500m, Cad, Jan1, Dec31));
        Assert.Equal(500m, SingleValue());
        Assert.Equal(500m, BatchValue());
    }
}
