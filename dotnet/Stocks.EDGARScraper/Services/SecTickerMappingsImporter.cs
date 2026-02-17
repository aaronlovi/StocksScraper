using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace EDGARScraper.Services;

public class SecTickerMappingsImporter {
    private readonly IDbmService _dbm;
    private readonly ILogger _logger;

    public SecTickerMappingsImporter(IDbmService dbm, ILogger logger) {
        _dbm = dbm;
        _logger = logger;
    }

    public async Task<Result> ImportAsync(string edgarDataDir, int batchSize, CancellationToken ct) {
        string baseMappingPath = Path.Combine(edgarDataDir, "company_tickers.json");
        string exchangeMappingPath = Path.Combine(edgarDataDir, "company_tickers_exchange.json");

        if (!File.Exists(baseMappingPath))
            return Result.Failure(ErrorCodes.NotFound, $"Missing mapping file: {baseMappingPath}");

        var exchangeByCik = new Dictionary<ulong, string>();
        var exchangeByTicker = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(exchangeMappingPath))
            SecTickerJsonParser.LoadExchangeMappings(exchangeMappingPath, exchangeByCik, exchangeByTicker);

        List<SecTickerMapping> mappings = SecTickerJsonParser.LoadBaseMappings(baseMappingPath, exchangeByCik, exchangeByTicker);
        _logger.LogInformation("Parsed {Count} ticker mappings from JSON files", mappings.Count);

        if (mappings.Count == 0)
            return Result.Failure(ErrorCodes.NotFound, "No ticker mappings found in JSON files.");

        Result<IReadOnlyCollection<Company>> companyResult =
            await _dbm.GetAllCompaniesByDataSource(ModelsConstants.EdgarDataSource, ct);
        if (companyResult.IsFailure)
            return Result.Failure(companyResult.ErrorCode, companyResult.ErrorMessage ?? "Failed to load companies.");

        var companyIdByCik = new Dictionary<ulong, ulong>();
        foreach (Company company in companyResult.Value!)
            companyIdByCik[company.Cik] = company.CompanyId;

        _logger.LogInformation("Loaded {Count} companies from database", companyIdByCik.Count);

        var batch = new List<CompanyTicker>();
        int matched = 0;
        int skipped = 0;
        int inserted = 0;

        foreach (SecTickerMapping mapping in mappings) {
            if (!companyIdByCik.TryGetValue(mapping.Cik, out ulong companyId)) {
                skipped++;
                continue;
            }

            matched++;
            batch.Add(new CompanyTicker(companyId, mapping.Ticker, mapping.Exchange));

            if (batch.Count >= batchSize) {
                Result batchResult = await _dbm.BulkInsertCompanyTickers(batch, ct);
                if (batchResult.IsFailure) {
                    _logger.LogError("Failed to insert batch of {Count} tickers: {Error}", batch.Count, batchResult.ErrorMessage);
                    return batchResult;
                }
                inserted += batch.Count;
                batch.Clear();
            }
        }

        if (batch.Count > 0) {
            Result batchResult = await _dbm.BulkInsertCompanyTickers(batch, ct);
            if (batchResult.IsFailure) {
                _logger.LogError("Failed to insert final batch of {Count} tickers: {Error}", batch.Count, batchResult.ErrorMessage);
                return batchResult;
            }
            inserted += batch.Count;
        }

        _logger.LogInformation(
            "SEC ticker mappings import complete. Parsed: {Parsed}, Matched: {Matched}, Skipped: {Skipped}, Inserted: {Inserted}",
            mappings.Count, matched, skipped, inserted);

        return Result.Success;
    }
}
