using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stocks.DataModels.Enums;
using Stocks.EDGARScraper.Models;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.EDGARScraper.Services;

public class UsGaap2025TaxonomyConceptsFileProcessor {
    private readonly List<TaxonomyConcept> _rawTaxonomyConcepts;
    private readonly List<TaxonomyConceptDTO> _taxonomyConceptDtos;
    private readonly string _csvFilePath;
    private readonly IDbmService _dbm;
    private readonly ILogger<UsGaap2025TaxonomyConceptsFileProcessor> _logger;
    private readonly CancellationToken _ct;

    public UsGaap2025TaxonomyConceptsFileProcessor(IOptions<UsGaap2025TaxonomyConceptsFileProcessorOptions> options, IDbmService dbm, ILoggerFactory loggerFactory, CancellationToken ct = default) {
        _rawTaxonomyConcepts = [];
        _taxonomyConceptDtos = [];
        _csvFilePath = options.Value.CsvPath;
        _dbm = dbm;
        _logger = loggerFactory.CreateLogger<UsGaap2025TaxonomyConceptsFileProcessor>();
        _ct = ct;

        _logger.LogInformation("TaxonomyConceptsFileProcessor initialized with CSV file path: {CsvPath}", _csvFilePath);
    }

    public async Task<Result> Process() {
        _logger.LogInformation("Processing taxonomy concepts from CSV: {CsvPath}", _csvFilePath);

        return await ParseTaxonomyConceptsFile().
            Then(ConvertRawConceptsToDTOs).
            Then(BulkInsertTaxonomyConcepts).
            OnCompletion(
                onSuccess: _ => _logger.LogInformation("UsGaap2025TaxonomyConceptsFileProcessor - Success"),
                onFailure: res => _logger.LogWarning("UsGaap2025TaxonomyConceptsFileProcessor Failed - {Error}", res.ErrorMessage));
    }

    private async Task<Result> ParseTaxonomyConceptsFile() {
        try {
            _logger.LogInformation("ParseTaxonomyConceptsFile");

            using var reader = new StreamReader(_csvFilePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            await foreach (dynamic r in csv.GetRecordsAsync<dynamic>(_ct)) {
                if (r.prefix != "us-gaap")
                    continue;

                var concept = new TaxonomyConcept(TaxonomyTypes.US_GAAP_2025, r.periodType, r.balance, r.@abstract, r.name, r.label, r.documentation);
                _rawTaxonomyConcepts.Add(concept);
            }
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.ParsingError, "ParseTaxonomyConceptsFile - Error: " + ex.Message);
        }

        _logger.LogInformation("ParseTaxonomyConceptsFile - Parsed {Count} raw taxonomy concepts from CSV file", _rawTaxonomyConcepts.Count);
        return Result.Success;
    }

    private async Task<Result> ConvertRawConceptsToDTOs() {
        try {
            _logger.LogInformation("ConvertRawConceptsToDTOs");

            foreach (TaxonomyConcept c in _rawTaxonomyConcepts) {
                long id = (long)await _dbm.GetNextId64(_ct);
                Result<TaxonomyConceptDTO> toDtoResult = c.ToTaxonomyConceptDTO(id);
                if (toDtoResult.IsFailure) {
                    return Result.Failure(toDtoResult);
                }

                _taxonomyConceptDtos.Add(toDtoResult.Value!);
            }
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.ParsingError, "ConvertRawConceptsToDTOs - Error: " + ex.Message);
        }

        _logger.LogInformation("ConvertRawConceptsToDTOs - Converted {Count} raw concepts to DTOs", _taxonomyConceptDtos.Count);
        return Result.Success;
    }

    private async Task<Result> BulkInsertTaxonomyConcepts() {
        try {
            _logger.LogInformation("BulkInsertTaxonomyConcepts");

            Result result = await _dbm.BulkInsertTaxonomyConcepts(_taxonomyConceptDtos, _ct);
            _logger.LogInformation("Bulk inserted {Count} taxonomy concepts", _taxonomyConceptDtos.Count);
            return result;
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.GenericError, "BulkInsertTaxonomyConcepts - Error: " + ex.Message);
        }
    }
}
