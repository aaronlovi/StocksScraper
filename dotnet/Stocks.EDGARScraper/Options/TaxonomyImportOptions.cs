namespace EDGARScraper.Options;

public class TaxonomyImportOptions {
    public string? RootDir { get; set; }

    public string ResolveRootDir()
        => string.IsNullOrWhiteSpace(RootDir) ? string.Empty : RootDir!;
}
