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

public class UsGaap2025PresentationFileProcessor {
    private readonly List<PresentationDetails> _rawPresentationDetails;
    private readonly List<PresentationDetailsDTO> _presentationDetailsDtos;
    private readonly Dictionary<string, long> _conceptIdsByName;
    private readonly string _csvFilePath;
    private readonly IDbmService _dbm;
    private readonly ILogger<UsGaap2025PresentationFileProcessor> _logger;
    private readonly CancellationToken _ct;

    public UsGaap2025PresentationFileProcessor(IOptions<UsGaap2025PresentationFileProcessorOptions> options, IDbmService dbm, ILoggerFactory loggerFactory, CancellationToken ct = default) {
        _rawPresentationDetails = [];
        _presentationDetailsDtos = [];
        _conceptIdsByName = [];
        _csvFilePath = options.Value.CsvPath;
        _dbm = dbm;
        _logger = loggerFactory.CreateLogger<UsGaap2025PresentationFileProcessor>();
        _ct = ct;

        _logger.LogInformation("UsGaap2025PresentationFileProcessor initialized with CSV file path: {CsvPath}", _csvFilePath);
    }

    public async Task<Result> Process() {
        _logger.LogInformation("Process beginning");

        return await GetConceptDetailsFromDb().
            Then(ParseRawPresentationDetailsFromFile).
            Then(ConvertRawPresentationDetailsToDTOs).
            Then(BulkInsertPresentationDetailDTOs).
            OnCompletion(
                onSuccess: _ => _logger.LogInformation("Process - Success"),
                onFailure: res => _logger.LogWarning("Process Failed - {Error}", res.ErrorMessage));
    }

    private async Task<Result> GetConceptDetailsFromDb() {
        _logger.LogInformation("GetConceptDetails");

        Result<IReadOnlyCollection<ConceptDetailsDTO>> results = await _dbm.GetTaxonomyConceptsByTaxonomyType((int)TaxonomyTypes.US_GAAP_2025, _ct);
        if (results.IsFailure)
            return Result.Failure(results);

        foreach (ConceptDetailsDTO concept in results.Value!)
            _conceptIdsByName[concept.Name] = concept.ConceptId;

        _logger.LogInformation("GetConceptDetails - Retrieved {Count} concept details from database", results.Value!.Count);
        return Result.Success;
    }

    private async Task<Result> ParseRawPresentationDetailsFromFile() {
        _logger.LogInformation("ParseTaxonomyPresentationFile");

        int numNodesToRecord = 0;

        try {
            using var reader = new StreamReader(_csvFilePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            int rowNumber = 0;

            if (!await csv.ReadAsync())
                return Result.Failure(ErrorCodes.ValidationError, "Presentation CSV is empty.");
            _ = csv.ReadHeader();
            ++rowNumber;

            string[] header = csv.HeaderRecord ?? [];
            bool hasPrefix = false;
            bool hasName = false;
            bool hasDepth = false;
            bool hasOrder = false;
            bool hasParent = false;
            foreach (string h in header) {
                if (h.Equals("prefix", StringComparison.OrdinalIgnoreCase))
                    hasPrefix = true;
                else if (h.Equals("name", StringComparison.OrdinalIgnoreCase))
                    hasName = true;
                else if (h.Equals("depth", StringComparison.OrdinalIgnoreCase))
                    hasDepth = true;
                else if (h.Equals("order", StringComparison.OrdinalIgnoreCase))
                    hasOrder = true;
                else if (h.Equals("parent", StringComparison.OrdinalIgnoreCase))
                    hasParent = true;
            }

            if (!hasPrefix || !hasName || !hasDepth || !hasOrder || !hasParent) {
                return Result.Failure(
                    ErrorCodes.ValidationError,
                    "Unexpected presentation CSV format. Expected columns: prefix,name,depth,order,parent.");
            }

            var parentChain = new List<PresentationDetails>();

            while (await csv.ReadAsync()) {
                ++rowNumber;

                string prefix = csv.GetField("prefix") ?? string.Empty;
                string name = csv.GetField("name") ?? string.Empty;
                string depthValue = csv.GetField("depth") ?? string.Empty;
                string orderValue = csv.GetField("order") ?? string.Empty;
                string parentValue = csv.GetField("parent") ?? string.Empty;

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!int.TryParse(depthValue, out int currentDepth))
                    return Result.Failure(ErrorCodes.ValidationError, $"Invalid depth '{depthValue}' for concept '{name}' at row {rowNumber}", name, $"{rowNumber}");

                if (currentDepth == 0)
                    parentChain.Clear();

                int depthIndex = currentDepth > 0 ? currentDepth - 1 : 0;
                if (currentDepth > 0 && parentChain.Count < depthIndex) {
                    return Result.Failure(ErrorCodes.ValidationError, $"Missing parent for concept '{name}' at row {rowNumber}.", name, $"{rowNumber}");
                }

                PresentationDetails? parentNode = null;
                if (currentDepth > 1 && parentChain.Count >= depthIndex)
                    parentNode = parentChain[depthIndex - 1];
                string expectedParentName = string.Empty;
                if (!string.IsNullOrWhiteSpace(parentValue)) {
                    int colonIndex = parentValue.IndexOf(':');
                    expectedParentName = colonIndex >= 0 ? parentValue[(colonIndex + 1)..].Trim() : parentValue.Trim();
                }

                if (!string.IsNullOrEmpty(expectedParentName) && parentNode is not null && parentNode.ConceptName != expectedParentName) {
                    return Result.Failure(
                        ErrorCodes.ValidationError,
                        $"Invalid parent '{expectedParentName}' for concept '{name}' at row {rowNumber}.",
                        name,
                        $"{rowNumber}");
                }

                PresentationDetails currentNode = CreateRawPresentationDetails(prefix, name, depthValue, orderValue, parentNode);
                numNodesToRecord += currentNode.IsUsGaapPresentationDetail ? 1 : 0;
                _rawPresentationDetails.Add(currentNode);

                if (depthIndex == parentChain.Count) {
                    parentChain.Add(currentNode);
                } else if (depthIndex < parentChain.Count) {
                    parentChain[depthIndex] = currentNode;
                }
                if (parentChain.Count > depthIndex + 1)
                    parentChain.RemoveRange(depthIndex + 1, parentChain.Count - depthIndex - 1);
            }
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.ParsingError, "ParseTaxonomyPresentationFile - Error: " + ex.Message);
        }

        _logger.LogInformation("ParseTaxonomyPresentationFile - Parsed {Count} raw taxonomy presentations from CSV file. Found {CountToRecord} presentations to record",
            _rawPresentationDetails.Count, numNodesToRecord);
        return Result.Success;
    }

    private static PresentationDetails CreateRawPresentationDetails(string prefix, string name, string depth, string order, PresentationDetails? parent) {
        bool isUsGaap = prefix == "us-gaap" && (parent?.IsUsGaapPresentationDetail ?? true);
        return new PresentationDetails(
            isUsGaap,
            TaxonomyTypes.US_GAAP_2025,
            prefix,
            name,
            depth,
            order,
            parent);
    }

    private async Task<Result> ConvertRawPresentationDetailsToDTOs() {
        _logger.LogInformation("ConvertRawPresentationDetailsToDTOs");

        var presentationIdLookup = new Dictionary<PresentationDetails, long>();

        try {
            foreach (PresentationDetails rawPresentationDetails in _rawPresentationDetails) {
                if (rawPresentationDetails.Depth == "0")
                    presentationIdLookup.Clear();

                if (!rawPresentationDetails.IsUsGaapPresentationDetail)
                    continue;

                long presentationId = (long)await _dbm.GetNextId64(_ct);
                presentationIdLookup[rawPresentationDetails] = presentationId;

                long parentPresentationId = 0;
                if (rawPresentationDetails.ParentPresentationDetails is not null
                    && presentationIdLookup.TryGetValue(rawPresentationDetails.ParentPresentationDetails!, out long parentId)) {
                    parentPresentationId = parentId;
                }

                Result<PresentationDetailsDTO> result = rawPresentationDetails.ToPresentationDetailsDTO(
                    presentationId, parentPresentationId, _conceptIdsByName);
                if (result.IsFailure)
                    return Result.Failure(result);
                _presentationDetailsDtos.Add(result.Value!);
            }
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.ParsingError, "ConvertRawPresentationDetailsToDTOs - Error: " + ex.Message);
        }

        _logger.LogInformation("ConvertRawPresentationDetailsToDTOs - Converted {Count} raw presentation details to DTOs",
            _presentationDetailsDtos.Count);
        return Result.Success;
    }

    private async Task<Result> BulkInsertPresentationDetailDTOs() {
        try {
            _logger.LogInformation("BulkInsertPresentationDetailDTOs");

            Result result = await _dbm.BulkInsertTaxonomyPresentations(_presentationDetailsDtos, _ct);
            if (result.IsFailure)
                return Result.Failure(result);
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.ParsingError, "BulkInsertPresentationDetailDTOs - Error: " + ex.Message);
        }

        _logger.LogInformation("BulkInsertPresentationDetailDTOs - Inserted {Count} presentation details into database", _presentationDetailsDtos.Count);
        return Result.Success;
    }
}
