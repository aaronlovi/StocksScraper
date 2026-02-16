using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.EDGARScraper.Services.Taxonomies;

public sealed class UsGaapTaxonomyImporter {
    private const string UsGaapPrefix = "us-gaap";
    private const string ConceptsSuffix = "_GAAP_Taxonomy.worksheets.concepts.csv";
    private const string PresentationSuffix = "_GAAP_Taxonomy.worksheets.presentation.csv";

    private readonly IDbmService _dbm;
    private readonly ILogger<UsGaapTaxonomyImporter> _logger;

    public UsGaapTaxonomyImporter(IDbmService dbm, ILogger<UsGaapTaxonomyImporter> logger) {
        _dbm = dbm;
        _logger = logger;
    }

    public async Task<Result> ImportYearAsync(int year, string rootDir, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(rootDir))
            return Result.Failure(ErrorCodes.ValidationError, "Taxonomy root directory is required.");

        string conceptsPath = Path.Combine(rootDir, $"{year}{ConceptsSuffix}");
        string presentationPath = Path.Combine(rootDir, $"{year}{PresentationSuffix}");

        if (!File.Exists(conceptsPath))
            return Result.Failure(ErrorCodes.NotFound, $"Missing concepts CSV: {conceptsPath}");
        if (!File.Exists(presentationPath))
            return Result.Failure(ErrorCodes.NotFound, $"Missing presentation CSV: {presentationPath}");

        Result<TaxonomyTypeInfo> taxonomyTypeResult =
            await _dbm.EnsureTaxonomyType(UsGaapPrefix, year, ct);
        if (taxonomyTypeResult.IsFailure)
            return Result.Failure(taxonomyTypeResult);

        int taxonomyTypeId = taxonomyTypeResult.Value!.TaxonomyTypeId;

        Result conceptsResult = await ImportConceptsAsync(conceptsPath, taxonomyTypeId, ct);
        if (conceptsResult.IsFailure)
            return conceptsResult;

        Result presentationsResult = await ImportPresentationsAsync(presentationPath, taxonomyTypeId, ct);
        if (presentationsResult.IsFailure)
            return presentationsResult;

        return Result.Success;
    }

    public IReadOnlyList<int> DiscoverYears(string rootDir) {
        var years = new List<int>();
        if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            return years;

        foreach (string filePath in Directory.EnumerateFiles(rootDir, $"*{ConceptsSuffix}", SearchOption.TopDirectoryOnly)) {
            string fileName = Path.GetFileName(filePath);
            int year = TryParseLeadingYear(fileName);
            if (year == 0)
                continue;
            if (!ContainsYear(years, year))
                years.Add(year);
        }

        years.Sort();
        return years;
    }

    private async Task<Result> ImportConceptsAsync(string csvPath, int taxonomyTypeId, CancellationToken ct) {
        var concepts = new List<ConceptDetailsDTO>();

        try {
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            await foreach (dynamic r in csv.GetRecordsAsync<dynamic>(ct)) {
                if (!string.Equals((string)r.prefix, UsGaapPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                Result<TaxonomyPeriodTypes> periodResult = ParsePeriodType((string)r.periodType);
                if (periodResult.IsFailure)
                    return Result.Failure(periodResult);
                Result<TaxonomyBalanceTypes> balanceResult = ParseBalanceType((string)r.balance);
                if (balanceResult.IsFailure)
                    return Result.Failure(balanceResult);
                Result<bool> abstractResult = ParseIsAbstract((string)r.@abstract);
                if (abstractResult.IsFailure)
                    return Result.Failure(abstractResult);

                long id = (long)await _dbm.GetNextId64(ct);
                var dto = new ConceptDetailsDTO(
                    id,
                    taxonomyTypeId,
                    (int)periodResult.Value,
                    (int)balanceResult.Value,
                    abstractResult.Value,
                    ((string)r.name).Trim(),
                    ((string)r.label).Trim(),
                    ((string)r.documentation).Trim());
                concepts.Add(dto);
            }
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.ParsingError, $"ImportConceptsAsync failed: {ex.Message}");
        }

        _logger.LogInformation("ImportConceptsAsync - Parsed {Count} concepts from {CsvPath}", concepts.Count, csvPath);
        return await _dbm.BulkInsertTaxonomyConcepts(concepts, ct);
    }

    private async Task<Result> ImportPresentationsAsync(string csvPath, int taxonomyTypeId, CancellationToken ct) {
        Result<IReadOnlyCollection<ConceptDetailsDTO>> conceptsResult =
            await _dbm.GetTaxonomyConceptsByTaxonomyType(taxonomyTypeId, ct);
        if (conceptsResult.IsFailure)
            return Result.Failure(conceptsResult);

        var conceptIdsByName = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (ConceptDetailsDTO concept in conceptsResult.Value ?? []) {
            if (!string.IsNullOrWhiteSpace(concept.Name))
                conceptIdsByName[concept.Name.Trim()] = concept.ConceptId;
        }

        var presentationDtos = new List<PresentationDetailsDTO>();
        var presentationIdByKey = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        try {
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            await foreach (dynamic r in csv.GetRecordsAsync<dynamic>(ct)) {
                if (!string.Equals((string)r.prefix, UsGaapPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string conceptName = ((string)r.name).Trim();
                if (string.IsNullOrWhiteSpace(conceptName))
                    continue;

                string rawParent = ((string)r.parent).Trim();
                string parentPrefix = "";
                string parentName = rawParent;
                if (rawParent.Contains(':')) {
                    int idx = rawParent.IndexOf(':');
                    parentPrefix = rawParent[..idx];
                    parentName = rawParent[(idx + 1)..];
                }

                if (!conceptIdsByName.TryGetValue(conceptName, out long conceptId))
                    return Result.Failure(ErrorCodes.ValidationError, $"Concept name '{conceptName}' not found in concept dictionary.");

                long parentConceptId = 0;
                long parentPresentationId = 0;
                bool parentIsExternal = !string.IsNullOrEmpty(parentPrefix)
                    && !string.Equals(parentPrefix, UsGaapPrefix, StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(parentName) && !parentIsExternal) {
                    if (!conceptIdsByName.TryGetValue(parentName, out parentConceptId))
                        return Result.Failure(ErrorCodes.ValidationError, $"Parent concept '{parentName}' not found for '{conceptName}'.");
                    string parentKey = BuildPresentationKey((string)r.role_name, parentName);
                    if (!presentationIdByKey.TryGetValue(parentKey, out parentPresentationId))
                        return Result.Failure(ErrorCodes.ValidationError, $"Parent presentation not found for '{parentName}'.");
                }

                if (!int.TryParse((string)r.depth, out int depth))
                    return Result.Failure(ErrorCodes.ValidationError, $"Invalid depth '{(string)r.depth}' for concept '{conceptName}'.");
                if (!int.TryParse((string)r.order, out int order))
                    return Result.Failure(ErrorCodes.ValidationError, $"Invalid order '{(string)r.order}' for concept '{conceptName}'.");

                long presentationId = (long)await _dbm.GetNextId64(ct);
                var dto = new PresentationDetailsDTO(
                    presentationId,
                    conceptId,
                    depth,
                    order * 100,
                    parentConceptId,
                    parentPresentationId,
                    ((string)r.role_name).Trim());
                presentationDtos.Add(dto);

                string key = BuildPresentationKey((string)r.role_name, conceptName);
                if (!presentationIdByKey.ContainsKey(key))
                    presentationIdByKey[key] = presentationId;
            }
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.ParsingError, $"ImportPresentationsAsync failed: {ex.Message}");
        }

        _logger.LogInformation("ImportPresentationsAsync - Parsed {Count} rows from {CsvPath}", presentationDtos.Count, csvPath);
        return await _dbm.BulkInsertTaxonomyPresentations(presentationDtos, ct);
    }

    private static Result<TaxonomyPeriodTypes> ParsePeriodType(string value) {
        string trimmed = value?.Trim() ?? string.Empty;
        return trimmed switch {
            "duration" => Result<TaxonomyPeriodTypes>.Success(TaxonomyPeriodTypes.Duration),
            "instant" => Result<TaxonomyPeriodTypes>.Success(TaxonomyPeriodTypes.Instant),
            "" => Result<TaxonomyPeriodTypes>.Success(TaxonomyPeriodTypes.None),
            _ => Result<TaxonomyPeriodTypes>.Failure(ErrorCodes.ValidationError, $"Invalid period type: {trimmed}", trimmed)
        };
    }

    private static Result<TaxonomyBalanceTypes> ParseBalanceType(string value) {
        string trimmed = value?.Trim() ?? string.Empty;
        return trimmed switch {
            "credit" => Result<TaxonomyBalanceTypes>.Success(TaxonomyBalanceTypes.Credit),
            "debit" => Result<TaxonomyBalanceTypes>.Success(TaxonomyBalanceTypes.Debit),
            "" => Result<TaxonomyBalanceTypes>.Success(TaxonomyBalanceTypes.NotApplicable),
            _ => Result<TaxonomyBalanceTypes>.Failure(ErrorCodes.ValidationError, $"Invalid balance type: {trimmed}", trimmed)
        };
    }

    private static Result<bool> ParseIsAbstract(string value) {
        string trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
            return Result<bool>.Success(false);
        if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
            return Result<bool>.Success(true);
        if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
            return Result<bool>.Success(false);
        return Result<bool>.Failure(ErrorCodes.ValidationError, $"Invalid abstract value: {trimmed}", trimmed);
    }

    private static int TryParseLeadingYear(string fileName) {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length < 4)
            return 0;
        string yearStr = fileName.Substring(0, 4);
        return int.TryParse(yearStr, NumberStyles.None, CultureInfo.InvariantCulture, out int year) ? year : 0;
    }

    private static bool ContainsYear(List<int> years, int year) {
        foreach (int existing in years) {
            if (existing == year)
                return true;
        }
        return false;
    }

    private static string BuildPresentationKey(string roleName, string conceptName) {
        string role = roleName?.Trim() ?? string.Empty;
        string concept = conceptName?.Trim() ?? string.Empty;
        return $"{role}|{concept}";
    }
}
