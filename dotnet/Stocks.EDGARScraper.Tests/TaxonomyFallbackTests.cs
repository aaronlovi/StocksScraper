using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class TaxonomyFallbackTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    [Fact]
    public async Task AtOrBefore_FallsBackToLatestEarlierVersion() {
        _ = await _dbm.EnsureTaxonomyType("us-gaap", 2024, _ct);
        _ = await _dbm.EnsureTaxonomyType("us-gaap", 2025, _ct);

        // A 2026 filing arrives before the 2026 taxonomy is published: resolve to 2025
        Result<TaxonomyTypeInfo> result = await _dbm.GetTaxonomyTypeByNameVersionAtOrBefore("us-gaap", 2026, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2025, result.Value!.TaxonomyTypeVersion);
    }

    [Fact]
    public async Task AtOrBefore_ExactMatchWins() {
        _ = await _dbm.EnsureTaxonomyType("us-gaap", 2024, _ct);
        _ = await _dbm.EnsureTaxonomyType("us-gaap", 2025, _ct);

        Result<TaxonomyTypeInfo> result = await _dbm.GetTaxonomyTypeByNameVersionAtOrBefore("us-gaap", 2024, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2024, result.Value!.TaxonomyTypeVersion);
    }

    [Fact]
    public async Task AtOrBefore_FailsWhenNothingOldEnough() {
        _ = await _dbm.EnsureTaxonomyType("us-gaap", 2024, _ct);

        Result<TaxonomyTypeInfo> result = await _dbm.GetTaxonomyTypeByNameVersionAtOrBefore("us-gaap", 2020, _ct);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AtOrBefore_DoesNotCrossTaxonomyNames() {
        _ = await _dbm.EnsureTaxonomyType("dei", 2025, _ct);

        Result<TaxonomyTypeInfo> result = await _dbm.GetTaxonomyTypeByNameVersionAtOrBefore("us-gaap", 2026, _ct);

        Assert.True(result.IsFailure);
    }
}
