using System;
using System.Collections.Generic;
using System.IO;
using Stocks.EDGARScraper.Services.Taxonomies;
using Microsoft.Extensions.Logging.Abstractions;
using Stocks.Persistence.Database;

namespace Stocks.EDGARScraper.Tests;

public class TaxonomyImportTests {
    [Fact]
    public void DiscoverYears_ReturnsSortedYears() {
        string tempDir = Path.Combine(Path.GetTempPath(), $"taxonomy-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try {
            File.WriteAllText(Path.Combine(tempDir, "2024_GAAP_Taxonomy.worksheets.concepts.csv"), "header");
            File.WriteAllText(Path.Combine(tempDir, "2023_GAAP_Taxonomy.worksheets.concepts.csv"), "header");
            File.WriteAllText(Path.Combine(tempDir, "notes.txt"), "ignore");

            var dbm = new DbmInMemoryService();
            var logger = NullLogger<UsGaapTaxonomyImporter>.Instance;
            var importer = new UsGaapTaxonomyImporter(dbm, logger);

            IReadOnlyList<int> years = importer.DiscoverYears(tempDir);
            Assert.Equal(2, years.Count);
            Assert.Equal(2023, years[0]);
            Assert.Equal(2024, years[1]);
        } finally {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
