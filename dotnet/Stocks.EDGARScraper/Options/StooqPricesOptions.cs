using System.IO;

namespace EDGARScraper.Options;

public class StooqPricesOptions {
    public string? OutputDir { get; set; }

    public string ResolveOutputDir(string edgarDataDir) {
        if (!string.IsNullOrWhiteSpace(OutputDir))
            return OutputDir!;
        return Path.Combine(edgarDataDir, "prices", "stooq");
    }
}
