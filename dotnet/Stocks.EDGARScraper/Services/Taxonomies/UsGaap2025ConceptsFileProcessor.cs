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
using Stocks.EDGARScraper.Models.Taxonomies;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.EDGARScraper.Services.Taxonomies;

public class UsGaap2025ConceptsFileProcessor {
    private readonly List<ConceptDetails> _rawConceptDetails;
    private readonly List<ConceptDetailsDTO> _conceptDetailsDtos;
    private readonly string _csvFilePath;
    private readonly IDbmService _dbm;
    private readonly ILogger<UsGaap2025ConceptsFileProcessor> _logger;
    private readonly CancellationToken _ct;

    public UsGaap2025ConceptsFileProcessor(IOptions<UsGaap2025ConceptsFileProcessorOptions> options, IDbmService dbm, ILoggerFactory loggerFactory, CancellationToken ct = default) {
        _rawConceptDetails = [];
        _conceptDetailsDtos = [];
        _csvFilePath = options.Value.CsvPath;
        _dbm = dbm;
        _logger = loggerFactory.CreateLogger<UsGaap2025ConceptsFileProcessor>();
        _ct = ct;

        _logger.LogInformation("UsGaap2025ConceptsFileProcessor initialized with CSV file path: {CsvPath}", _csvFilePath);
    }

    public async Task<Result> Process() {
        _logger.LogInformation("Process beginning");

        return await ParseTaxonomyConceptsFile().
            Then(ConvertRawConceptsToDTOs).
            Then(BulkInsertTaxonomyConcepts).
            OnCompletion(
                onSuccess: _ => _logger.LogInformation("Process - Success"),
                onFailure: res => _logger.LogWarning("Process Failed - {Error}", res.ErrorMessage));
    }

    private async Task<Result> ParseTaxonomyConceptsFile() {
        try {
            _logger.LogInformation("ParseTaxonomyConceptsFile");

            using var reader = new StreamReader(_csvFilePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            await foreach (dynamic r in csv.GetRecordsAsync<dynamic>(_ct)) {
                if (r.prefix != "us-gaap")
                    continue;

                var concept = new ConceptDetails(TaxonomyTypes.US_GAAP_2025, r.periodType, r.balance, r.@abstract, r.name, r.label, r.documentation);
                _rawConceptDetails.Add(concept);
            }
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.ParsingError, "ParseTaxonomyConceptsFile - Error: " + ex.Message);
        }

        _logger.LogInformation("ParseTaxonomyConceptsFile - Parsed {Count} raw taxonomy concepts from CSV file", _rawConceptDetails.Count);
        return Result.Success;
    }

    private async Task<Result> ConvertRawConceptsToDTOs() {
        try {
            _logger.LogInformation("ConvertRawConceptsToDTOs");

            foreach (ConceptDetails c in _rawConceptDetails) {
                long id = (long)await _dbm.GetNextId64(_ct);
                Result<ConceptDetailsDTO> toDtoResult = c.ToConceptDetailsDTO(id);
                if (toDtoResult.IsFailure)
                    return Result.Failure(toDtoResult);

                _conceptDetailsDtos.Add(toDtoResult.Value!);
            }
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.ParsingError, "ConvertRawConceptsToDTOs - Error: " + ex.Message);
        }

        _logger.LogInformation("ConvertRawConceptsToDTOs - Converted {Count} raw concepts to DTOs", _conceptDetailsDtos.Count);
        return Result.Success;
    }

    private async Task<Result> BulkInsertTaxonomyConcepts() {
        try {
            _logger.LogInformation("BulkInsertTaxonomyConcepts");

            Result result = await _dbm.BulkInsertTaxonomyConcepts(_conceptDetailsDtos, _ct);
            if (result.IsFailure)
                return Result.Failure(result);
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.GenericError, "BulkInsertTaxonomyConcepts - Error: " + ex.Message);
        }

        _logger.LogInformation("Bulk inserted {Count} taxonomy concepts", _conceptDetailsDtos.Count);
        return Result.Success;
    }
}
