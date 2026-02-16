namespace EDGARScraper.Options;

public class StooqBulkImportOptions {
    public string? RootDir { get; set; }
    public int BatchSize { get; set; }

    public string ResolveRootDir()
        => string.IsNullOrWhiteSpace(RootDir)
            ? string.Empty
            : RootDir!;

    public int ResolveBatchSize()
        => BatchSize > 0 ? BatchSize : 500;
}
