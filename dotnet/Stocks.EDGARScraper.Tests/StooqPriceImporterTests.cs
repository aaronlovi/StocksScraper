using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDGARScraper.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class StooqPriceImporterTests {
    [Fact]
    public async Task ImportAsync_UsesOldestImportFirst() {
        string tempDir = Path.Combine(Path.GetTempPath(), $"stooq-import-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempDir);

        string mappingsPath = Path.Combine(tempDir, "company_tickers.json");
        string exchangePath = Path.Combine(tempDir, "company_tickers_exchange.json");
        string outputDir = Path.Combine(tempDir, "prices", "stooq");
        _ = Directory.CreateDirectory(outputDir);

        File.WriteAllText(mappingsPath, "{ \"0\": { \"cik_str\": 320193, \"ticker\": \"AAPL\" }, \"1\": { \"cik_str\": 789019, \"ticker\": \"MSFT\" } }");
        File.WriteAllText(exchangePath, "{ \"fields\": [\"cik\", \"ticker\", \"exchange\"], \"data\": [[320193, \"AAPL\", \"NASDAQ\"], [789019, \"MSFT\", \"NASDAQ\"]] }");

        string aaplPath = Path.Combine(outputDir, "AAPL.csv");
        string msftPath = Path.Combine(outputDir, "MSFT.csv");

        File.WriteAllText(aaplPath,
            "Cik,Ticker,Exchange,StooqSymbol,Date,Open,High,Low,Close,Volume\n" +
            "320193,AAPL,NASDAQ,aapl.us,2025-12-31,1,2,0.5,1.5,100\n");
        File.WriteAllText(msftPath,
            "Cik,Ticker,Exchange,StooqSymbol,Date,Open,High,Low,Close,Volume\n" +
            "789019,MSFT,NASDAQ,msft.us,2025-12-31,3,4,2.5,3.5,200\n");

        var dbm = new DbmInMemoryService();
        _ = await dbm.UpsertPriceImport(new PriceImportStatus(320193, "AAPL", "NASDAQ", DateTime.UtcNow), CancellationToken.None);

        NullLogger<StooqPriceImporter> logger = NullLogger<StooqPriceImporter>.Instance;
        var importer = new StooqPriceImporter(dbm, logger);

        try {
            Result result = await importer.ImportAsync(tempDir, outputDir, 1, 10, CancellationToken.None);
            Assert.True(result.IsSuccess);

            Result<IReadOnlyCollection<PriceImportStatus>> importsResult = await dbm.GetPriceImportStatuses(CancellationToken.None);
            Assert.True(importsResult.IsSuccess);
            IReadOnlyCollection<PriceImportStatus> imports = importsResult.Value ?? Array.Empty<PriceImportStatus>();
            Assert.Equal(2, imports.Count);
            bool sawMsftImport = false;
            foreach (PriceImportStatus import in imports) {
                if (import.Cik == 789019 && import.Ticker == "MSFT" && import.Exchange == "NASDAQ")
                    sawMsftImport = true;
            }
            Assert.True(sawMsftImport);

            Result<IReadOnlyCollection<PriceRow>> pricesResult = await dbm.GetPricesByTicker("MSFT", CancellationToken.None);
            Assert.True(pricesResult.IsSuccess);
            IReadOnlyCollection<PriceRow> prices = pricesResult.Value ?? Array.Empty<PriceRow>();
            _ = Assert.Single(prices);
            string? ticker = null;
            foreach (PriceRow price in prices)
                ticker = price.Ticker;
            Assert.Equal("MSFT", ticker);
        } finally {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
