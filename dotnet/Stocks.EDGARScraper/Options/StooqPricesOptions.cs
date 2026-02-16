using System.IO;

namespace EDGARScraper.Options;

public class StooqPricesOptions {
    public string? OutputDir { get; set; }
    public string? UserAgent { get; set; }
    public int DelayMilliseconds { get; set; }
    public int MaxRetries { get; set; }

    public string ResolveOutputDir(string edgarDataDir) {
        if (!string.IsNullOrWhiteSpace(OutputDir))
            return OutputDir!;
        return Path.Combine(edgarDataDir, "prices", "stooq");
    }

    public string ResolveUserAgent()
        => string.IsNullOrWhiteSpace(UserAgent)
            ? "EDGARScraper (contact: inno.and.logic@gmail.com)"
            : UserAgent!;

    public int ResolveDelayMilliseconds()
        => DelayMilliseconds > 0 ? DelayMilliseconds : 0;

    public int ResolveMaxRetries()
        => MaxRetries > 0 ? MaxRetries : 5;
}
