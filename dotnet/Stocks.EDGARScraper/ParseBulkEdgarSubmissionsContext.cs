using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;

namespace EDGARScraper;

internal class ParseBulkEdgarSubmissionsContext(IServiceProvider svp) {
    private readonly ILogger<ParseBulkEdgarSubmissionsContext> _logger = svp.GetRequiredService<ILogger<ParseBulkEdgarSubmissionsContext>>();

    public int NumFiles { get; set; }
    public int NumSubmissions { get; set; }
    public long TotalLength { get; set; }
    public bool IsCurrentFileSubmissionsFile { get; set; }
    public string CurrentFileName { get; set; } = string.Empty;
    public List<Submission> SubmissionsBatch { get; init; } = [];
    public List<Task> Tasks { get; init; } = [];
    public Dictionary<ulong, ulong> CompanyIdsByCiks { get; init; } = [];
    public SubmissionJsonConverter JsonConverter { get; init; } = new(svp);

    public void LogProgress() {
        _logger.LogInformation("Processed {NumFiles} files; {NumSubmissions} submissions; Total length: {TotalLength} bytes",
            NumFiles, NumSubmissions, TotalLength);
    }
}
