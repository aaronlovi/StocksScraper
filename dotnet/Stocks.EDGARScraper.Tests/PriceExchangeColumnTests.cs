using Stocks.Persistence.Database.Statements;

namespace Stocks.EDGARScraper.Tests;

/// <summary>
/// H1: price_imports / price_downloads key on (cik, ticker, exchange), so the exchange
/// column cannot hold NULL. A missing exchange is stored as '' and read back as null.
/// </summary>
public class PriceExchangeColumnTests {
    [Fact]
    public void ToDb_MissingExchange_IsEmptyString() {
        Assert.Equal(string.Empty, PriceExchangeColumn.ToDb(null));
    }

    [Fact]
    public void ToDb_KnownExchange_Unchanged() {
        Assert.Equal("NASDAQ", PriceExchangeColumn.ToDb("NASDAQ"));
    }

    [Fact]
    public void FromDb_EmptyString_IsNull() {
        Assert.Null(PriceExchangeColumn.FromDb(string.Empty));
        Assert.Null(PriceExchangeColumn.FromDb(null));
    }

    [Fact]
    public void FromDb_KnownExchange_Unchanged() {
        Assert.Equal("NYSE", PriceExchangeColumn.FromDb("NYSE"));
    }
}
