using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;

namespace EDGARScraper;

internal class ParseBulkXbrlArchiveContext(IServiceProvider svp) {
    private readonly ILogger<ParseBulkXbrlArchiveContext> _logger = svp.GetRequiredService<ILogger<ParseBulkXbrlArchiveContext>>();

    public int NumFiles { get; set; }
    public int NumDataPoints { get; set; }
    public int NumDataPointUnits { get; set; }
    public long TotalLength { get; set; }
    public string CurrentFileName { get; set; } = string.Empty;
    public List<DataPoint> DataPointsBatch { get; init; } = [];
    public List<Task> Tasks { get; init; } = [];
    public Dictionary<ulong, ulong> CompanyIdsByCik { get; init; } = [];
    public Dictionary<string, DataPointUnit> UnitsByUnitName { get; init; } = [];
    public Dictionary<ulong, List<Submission>> SubmissionsByCompanyId { get; init; } = [];

    public void LogProgress() {
        _logger.LogInformation("Processed {NumFiles} files; {NumDataPoints} data points; {NumDataPointUnits} data point units; Total length: {TotalLength} bytes",
            NumFiles, NumDataPoints, NumDataPointUnits, TotalLength);
    }
}
