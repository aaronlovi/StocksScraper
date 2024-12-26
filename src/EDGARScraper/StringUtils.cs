namespace EDGARScraper;

internal static class StringUtils
{
    internal static string GetTruncatedStringForLogs(this string input, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        if (input.Length <= maxLength) return input;

        return input[..maxLength] + "...";
    }
}
