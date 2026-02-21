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

public class StooqBulkPriceImporterTests {
    [Fact]
    public async Task ImportAsync_WritesPricesAndStatuses() {
        string tempDir = Path.Combine(Path.GetTempPath(), $"stooq-bulk-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempDir);
        string dataDir = Path.Combine(tempDir, "nasdaq_stocks", "1");
        _ = Directory.CreateDirectory(dataDir);

        string mappingDir = tempDir;
        string mappingsPath = Path.Combine(mappingDir, "company_tickers.json");
        string exchangePath = Path.Combine(mappingDir, "company_tickers_exchange.json");

        File.WriteAllText(mappingsPath, "{ \"0\": { \"cik_str\": 1234, \"ticker\": \"HIHO\" } }");
        File.WriteAllText(exchangePath, "{ \"fields\": [\"cik\", \"ticker\", \"exchange\"], \"data\": [[1234, \"HIHO\", \"NASDAQ\"]] }");

        string filePath = Path.Combine(dataDir, "hiho.us.txt");
        File.WriteAllText(filePath,
            "<TICKER>,<PER>,<DATE>,<TIME>,<OPEN>,<HIGH>,<LOW>,<CLOSE>,<VOL>,<OPENINT>\n" +
            "HIHO.US,D,20050225,000000,2.05734,2.05734,2.05734,2.05734,1759.160289426,0\n");

        var dbm = new DbmInMemoryService();
        NullLogger<StooqBulkPriceImporter> logger = NullLogger<StooqBulkPriceImporter>.Instance;
        var importer = new StooqBulkPriceImporter(dbm, logger);

        try {
            Result result = await importer.ImportAsync(tempDir, mappingDir, 100, CancellationToken.None);
            Assert.True(result.IsSuccess);

            Result<IReadOnlyCollection<PriceRow>> pricesResult = await dbm.GetPricesByTicker("HIHO", CancellationToken.None);
            Assert.True(pricesResult.IsSuccess);
            IReadOnlyCollection<PriceRow> prices = pricesResult.Value ?? Array.Empty<PriceRow>();
            _ = Assert.Single(prices);

            Result<IReadOnlyCollection<PriceImportStatus>> importResult = await dbm.GetPriceImportStatuses(CancellationToken.None);
            Assert.True(importResult.IsSuccess);
            IReadOnlyCollection<PriceImportStatus> imports = importResult.Value ?? Array.Empty<PriceImportStatus>();
            _ = Assert.Single(imports);

        } finally {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
