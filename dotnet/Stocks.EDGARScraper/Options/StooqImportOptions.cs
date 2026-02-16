namespace EDGARScraper.Options;

public class StooqImportOptions {
    public int MaxTickersPerRun { get; set; }
    public int BatchSize { get; set; }

    public int ResolveMaxTickersPerRun()
        => MaxTickersPerRun > 0 ? MaxTickersPerRun : 0;

    public int ResolveBatchSize()
        => BatchSize > 0 ? BatchSize : 500;
}
