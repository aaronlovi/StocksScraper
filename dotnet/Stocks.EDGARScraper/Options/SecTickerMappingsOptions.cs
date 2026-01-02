namespace EDGARScraper.Options;

public class SecTickerMappingsOptions {
    public string? UserAgent { get; set; }

    public string ResolveUserAgent()
        => string.IsNullOrWhiteSpace(UserAgent)
            ? "EDGARScraper (contact: inno.and.logic@gmail.com)"
            : UserAgent!;
}
