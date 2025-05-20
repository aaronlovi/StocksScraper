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

            PresentationDetails? parentNode = null;
            int previousDepth = 0;
            var parentsStack = new Stack<PresentationDetails>();
            int rowNumber = 0;

            await foreach (dynamic r in csv.GetRecordsAsync<dynamic>(_ct)) {
                ++rowNumber;

                if (!int.TryParse(r.depth, out int currentDepth))
                    return Result.Failure(ErrorCodes.ValidationError, $"Invalid depth '{r.depth}' for concept '{r.name}' at row {rowNumber}", r.name, $"{rowNumber}");

                string parentName = r.parent.Trim();
                if (!string.IsNullOrEmpty(parentName)) {
                    int colonIndex = parentName.IndexOf(':') + 1;
                    parentName = parentName[colonIndex..].Trim();
                }

                if (currentDepth == 0) {
                    parentsStack.Clear();

                    parentNode = CreateRawPresentationDetails(r, null);
                    numNodesToRecord += parentNode.IsUsGaapPresentationDetail ? 1 : 0;

                    _rawPresentationDetails.Add(parentNode);
                    previousDepth = 0;
                } else if (currentDepth == previousDepth + 1) {
                    parentsStack.Push(parentNode!);

                    PresentationDetails currentNode = CreateRawPresentationDetails(r, parentNode);
                    numNodesToRecord += currentNode.IsUsGaapPresentationDetail ? 1 : 0;

                    _rawPresentationDetails.Add(currentNode);
                    parentNode = currentNode;
                    previousDepth = currentDepth;
                } else if (currentDepth < previousDepth) {
                    int numToPop = previousDepth - currentDepth;
                    if (numToPop > parentsStack.Count)
                        return Result.Failure(ErrorCodes.ValidationError, $"Invalid depth '{r.depth}' for concept '{r.name}' at row {rowNumber}.", r.name, $"{rowNumber}");
                    for (int i = 0; i < numToPop; ++i)
                        _ = parentsStack.Pop();
                    parentNode = parentsStack.Peek();
                    if (parentNode.ConceptName != parentName)
                        return Result.Failure(ErrorCodes.ValidationError, $"Invalid parent '{parentName}' for concept '{r.name}' at row {rowNumber}.", r.name, $"{rowNumber}");

                    PresentationDetails currentNode = CreateRawPresentationDetails(r, parentNode);
                    numNodesToRecord += currentNode.IsUsGaapPresentationDetail ? 1 : 0;

                    _rawPresentationDetails.Add(currentNode);
                    parentNode = currentNode;
                    previousDepth = currentDepth;
                } else if (currentDepth == previousDepth) {
                    PresentationDetails currentNode = CreateRawPresentationDetails(r, parentsStack.Peek());
                    numNodesToRecord += currentNode.IsUsGaapPresentationDetail ? 1 : 0;

                    _rawPresentationDetails.Add(currentNode);
                    parentNode = currentNode;
                    previousDepth = currentDepth;
                }
            }
        } catch (Exception ex) {
            return Result.Failure(ErrorCodes.ParsingError, "ParseTaxonomyPresentationFile - Error: " + ex.Message);
        }

        _logger.LogInformation("ParseTaxonomyPresentationFile - Parsed {Count} raw taxonomy presentations from CSV file. Found {CountToRecord} presentations to record",
            _rawPresentationDetails.Count, numNodesToRecord);
        return Result.Success;
    }

    private static PresentationDetails CreateRawPresentationDetails(dynamic r, PresentationDetails? parent) {
        bool isUsGaap = r.prefix == "us-gaap" && (parent?.IsUsGaapPresentationDetail ?? true);
        return new PresentationDetails(
            isUsGaap,
            TaxonomyTypes.US_GAAP_2025,
            r.prefix,
            r.name,
            r.depth,
            r.order,
            parent);
    }
}
