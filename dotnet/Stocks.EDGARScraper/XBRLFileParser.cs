using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.DataModels.EdgarFileModels;
using Stocks.DataModels.Enums;
using Stocks.EDGARScraper.Enums;
using Stocks.Shared;

namespace EDGARScraper;

internal class XBRLFileParser {
    private ulong _companyId;
    private XbrlJson? _xbrlJson;
    private readonly string _content;
    private readonly Dictionary<ulong, ulong> _companyIdsByCiks;
    private readonly Dictionary<ulong, List<Submission>> _submissionsByCompanyId;
    private readonly Dictionary<string, Dictionary<string, Dictionary<DatePair, DataPoint>>> _dataPoints;
    private readonly Dictionary<string, Submission> _submissionsByFilingReference;
    private readonly HashSet<string> _filingReferencesWithNoSubmissions;
    private readonly ILogger<XBRLFileParser> _logger;

    private ulong Cik => _xbrlJson?.Cik ?? 0;

    internal XBRLFileParser(string content, ParseBulkXbrlArchiveContext parserContext, IServiceProvider svp) {
        _content = content;
        _companyIdsByCiks = parserContext.CompanyIdsByCik;
        _submissionsByCompanyId = parserContext.SubmissionsByCompanyId;
        _dataPoints = [];
        _submissionsByFilingReference = [];
        _filingReferencesWithNoSubmissions = [];
        _logger = svp.GetRequiredService<ILogger<XBRLFileParser>>();
    }

    internal IEnumerable<DataPoint> DataPoints => _dataPoints.Values.SelectMany(x => x.Values.SelectMany(y => y.Values));

    internal XBRLParserResult Parse() {
        try {
            _xbrlJson = JsonSerializer.Deserialize<XbrlJson>(_content, Conventions.DefaultOptions);
            if (_xbrlJson is null) {
                _logger.LogWarning("Parse - Failed to deserialize XBRL JSON.");
                return XBRLParserResult.FailedToDeserializeXbrlJson();
            }

            if (Cik == 0) {
                _logger.LogInformation("Parse - CIK is 0, aborting");
                return XBRLParserResult.Failure("CIK is 0", XBRLFileParserFailureReason.CikIsZero);
            }

            if (!_companyIdsByCiks.TryGetValue(Cik, out _companyId)) {
                _logger.LogWarning("Parse - Failed to find company ID for CIK {Cik}, aborting", Cik);
                return XBRLParserResult.CikIsZero();
            }

            using IDisposable? logContext = CreateLogContext();

            if (!_submissionsByCompanyId.TryGetValue(_companyId, out List<Submission>? submissions)) {
                _logger.LogInformation("Parse - Failed to find submissions for company ID {_companyId}, aborting", _companyId);
                return XBRLParserResult.FailedToFindSubmissions(_companyId);
            }

            foreach (Submission submission in submissions)
                _submissionsByFilingReference[submission.FilingReference] = submission;

            foreach ((string factName, Fact fact) in _xbrlJson.Facts.UsGaap)
                ProcessFact(factName, fact);

            return XBRLParserResult.Success;
        } catch (Exception ex) {
            _logger.LogError(ex, "Parse - Exception occurred");
            return XBRLParserResult.GeneralFault(ex.Message);
        }

        // Local helper methods

        IDisposable? CreateLogContext() => _logger.BeginScope(new Dictionary<string, object> {
            [LogUtils.CikContext] = Cik,
            [LogUtils.CompanyIdContext] = _companyId
        });
    }

    private void ProcessFact(string factName, Fact fact) {
        Dictionary<string, Dictionary<DatePair, DataPoint>> unitsDataPoints =
            _dataPoints.GetOrCreateEntry(factName);

        foreach ((string unitName, List<Unit> units) in fact.Units)
            ProcessUnitsForFact(factName, unitsDataPoints, unitName.ToLowerInvariant(), units);
    }

    private void ProcessUnitsForFact(
        string factName,
        Dictionary<string, Dictionary<DatePair, DataPoint>> unitsDataPoints,
        string unitName,
        List<Unit> units) {
        Dictionary<DatePair, DataPoint> dataPointsByDatePair =
            unitsDataPoints.GetOrCreateEntry(unitName);

        foreach (Unit unitData in units) {
            if (!VerifyFilingReference(unitData))
                continue;

            ProcessUnitItemForFact(factName, unitName, dataPointsByDatePair, unitData);
        }

        // Local helper methods

        bool VerifyFilingReference(Unit unitData) {
            if (_submissionsByFilingReference.TryGetValue(unitData.FilingReference, out Submission? submission)) {
                if (submission.FilingCategory is FilingCategory.Other) {
                    _logger.LogDebug("ProcessUnitsForFact - Skipping data point with filing reference {FilingReference} because it has a filing category of 'Other'. Filing category: {FilingCategory}",
                        unitData.FilingReference, submission.FilingCategory);
                    return false;
                }

                return true;
            }

            if (_filingReferencesWithNoSubmissions.Add(unitData.FilingReference)) {
                _logger.LogWarning("ProcessUnitsForFact - Failed to find submission for filing reference {FilingReference}",
                    unitData.FilingReference);
            }

            return false;
        }
    }

    private void ProcessUnitItemForFact(
        string factName, string unit, Dictionary<DatePair, DataPoint> dataPointsByDatePair, Unit unitData) {
        DatePair datePair = unitData.DatePair;

        if (dataPointsByDatePair.TryGetValue(datePair, out DataPoint? existingDataPoint)
            && unitData.FiledDate <= existingDataPoint.FiledDate) {
            return;
        }

        if (!_submissionsByFilingReference.TryGetValue(unitData.FilingReference, out Submission? submission)) {
            _logger.LogWarning("ProcessUnitItemForFact - Failed to find submission for filing reference {FilingReference}",
                unitData.FilingReference);
            return;
        }

        dataPointsByDatePair[datePair] = new DataPoint(
            0, // Data point ID is not known at this point
            _companyId,
            factName.ToLowerInvariant(),
            unitData.FilingReference,
            datePair,
            unitData.Value,
            new DataPointUnit(0, unit), // Data point unit ID is not known at this point
            unitData.FiledDate,
            submission.SubmissionId,
            0L // taxonomy_concept_id placeholder, to be set in ETL
        );
    }
}
