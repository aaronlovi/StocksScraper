using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.DataModels.EdgarFileModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace EDGARScraper.Services;

/// <summary>Counts from one <see cref="CompanySicImporter"/> run.</summary>
public record CompanySicImportStats(int CompanyFiles, int WithSic, int UnmatchedCiks, int Updated, int Failed);

/// <summary>
/// Reads <c>sic</c> and <c>sicDescription</c> from each company file in the EDGAR submissions
/// archive and stores them on <c>companies</c> (<c>--import-company-sic</c>). Every known
/// company in the archive is written, so an empty SIC clears an earlier one. Re-runnable.
/// </summary>
public class CompanySicImporter {
    private const string PaginatedFileMarker = "-submissions-";

    private readonly IDbmService _dbm;
    private readonly ILogger _logger;

    public CompanySicImporter(IDbmService dbm, ILogger logger) {
        _dbm = dbm;
        _logger = logger;
    }

    public async Task<Result<CompanySicImportStats>> ImportAsync(string archivePath, int batchSize, CancellationToken ct) {
        if (!File.Exists(archivePath))
            return Result<CompanySicImportStats>.Failure(ErrorCodes.NotFound, $"Missing submissions archive: {archivePath}");

        Result<IReadOnlyCollection<Company>> companyResult =
            await _dbm.GetAllCompaniesByDataSource(ModelsConstants.EdgarDataSource, ct);
        if (companyResult.IsFailure || companyResult.Value is null)
            return Result<CompanySicImportStats>.Failure(companyResult.ErrorCode, companyResult.ErrorMessage ?? "Failed to load companies.");

        var companyIdByCik = new Dictionary<ulong, ulong>();
        foreach (Company company in companyResult.Value)
            companyIdByCik[company.Cik] = company.CompanyId;

        int companyFiles = 0;
        int withSic = 0;
        int unmatched = 0;
        int updated = 0;
        int failed = 0;
        var batch = new List<CompanySic>(batchSize);

        using var zipReader = new ZipFileReader(archivePath);
        foreach (string fileName in zipReader.EnumerateFileNames()) {
            ct.ThrowIfCancellationRequested();
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || fileName.Contains(PaginatedFileMarker, StringComparison.OrdinalIgnoreCase))
                continue;

            ++companyFiles;
            SubmissionsCompanyHeader? header;
            try {
                header = JsonSerializer.Deserialize<SubmissionsCompanyHeader>(
                    zipReader.ExtractFileContent(fileName), Conventions.DefaultOptions);
            } catch (Exception ex) when (ex is JsonException or InvalidDataException) {
                _logger.LogWarning("CompanySicImporter - Failed to parse {FileName}: {Error}", fileName, ex.Message);
                ++failed;
                continue;
            }
            if (header is null) {
                ++failed;
                continue;
            }

            int? sicCode = IndustryClassification.ParseSic(header.Sic);
            if (sicCode.HasValue)
                ++withSic;

            if (!companyIdByCik.TryGetValue(header.Cik, out ulong companyId)) {
                ++unmatched;
                continue;
            }

            string? description = sicCode.HasValue && !string.IsNullOrWhiteSpace(header.SicDescription)
                ? header.SicDescription.Trim()
                : null;
            batch.Add(new CompanySic(companyId, sicCode, description));
            if (batch.Count >= batchSize) {
                Result flush = await Flush(batch, ct);
                if (flush.IsFailure)
                    return Result<CompanySicImportStats>.Failure(flush);
                updated += batch.Count;
                batch.Clear();
            }

            if ((companyFiles % 100_000) == 0)
                _logger.LogInformation("CompanySicImporter - {Files} company files read, {WithSic} with SIC", companyFiles, withSic);
        }

        if (batch.Count > 0) {
            Result flush = await Flush(batch, ct);
            if (flush.IsFailure)
                return Result<CompanySicImportStats>.Failure(flush);
            updated += batch.Count;
        }

        var stats = new CompanySicImportStats(companyFiles, withSic, unmatched, updated, failed);
        _logger.LogInformation(
            "CompanySicImporter - Done: {Files} company files, {WithSic} with SIC, {Unmatched} unmatched CIKs, {Updated} companies updated, {Failed} failed",
            stats.CompanyFiles, stats.WithSic, stats.UnmatchedCiks, stats.Updated, stats.Failed);
        return Result<CompanySicImportStats>.Success(stats);
    }

    private Task<Result> Flush(List<CompanySic> batch, CancellationToken ct) =>
        _dbm.UpdateCompanySic(new List<CompanySic>(batch), ct);
}
