using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DataModels.XbrlFileModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Utilities;

namespace EDGARScraper;

internal class XBRLFileParser
{
    private ulong _companyId;
    private XbrlJson? _xbrlJson;
    private readonly string _content;
    private readonly Dictionary<ulong, ulong> _companyIdsByCiks;
    private readonly Dictionary<string, Dictionary<string, Dictionary<DatePair, DataPoint>>> _dataPoints;
    private readonly ILogger<XBRLFileParser> _logger;

    private ulong Cik => _xbrlJson?.Cik ?? 0;

    internal XBRLFileParser(
        string content,
        Dictionary<ulong, ulong> companyIdsByCiks,
        IServiceProvider svp)
    {
        _content = content;
        _companyIdsByCiks = companyIdsByCiks;
        _dataPoints = [];
        _logger = svp.GetRequiredService<ILogger<XBRLFileParser>>();
    }

    internal IEnumerable<DataPoint> DataPoints => _dataPoints.Values.SelectMany(x => x.Values.SelectMany(y => y.Values));

    internal Results Parse()
    {
        try
        {
            _xbrlJson = JsonSerializer.Deserialize<XbrlJson>(_content);
            if (_xbrlJson is null)
            {
                _logger.LogWarning("Parse - Failed to deserialize XBRL JSON.");
                return Results.FailureResult("Failed to deserialize XBRL JSON.");
            }

            if (!_companyIdsByCiks.TryGetValue(Cik, out _companyId))
            {
                _logger.LogWarning("Parse - Failed to find company ID for CIK {Cik}, aborting", Cik);
                return Results.FailureResult($"Failed to find company ID for CIK {Cik}, aborting");
            }

            foreach ((string factName, Fact fact) in _xbrlJson.Facts.UsGaap)
                ProcessFact(factName, fact);

            return Results.Success;
        }
        catch (Exception ex)
        {
            return Results.FailureResult(ex.Message);
        }
    }

    private void ProcessFact(string factName, Fact fact)
    {
        Dictionary<string, Dictionary<DatePair, DataPoint>> unitsDataPoints =
            _dataPoints.GetOrCreateEntry(factName);

        foreach ((string unitName, List<Unit> units) in fact.Units)
            ProcessUnitsForFact(factName, unitsDataPoints, unitName.ToLowerInvariant(), units);
    }

    private void ProcessUnitsForFact(
        string factName, Dictionary<string, Dictionary<DatePair, DataPoint>> unitsDataPoints, string unitName, List<Unit> units)
    {
        Dictionary<DatePair, DataPoint> dataPointsByDatePair =
            unitsDataPoints.GetOrCreateEntry(unitName);

        foreach (Unit unitData in units)
            ProcessUnitItemForFact(factName, unitName, dataPointsByDatePair, unitData);
    }

    private void ProcessUnitItemForFact(
        string factName, string unit, Dictionary<DatePair, DataPoint> dataPointsByDatePair, Unit unitData)
    {
        DatePair datePair = unitData.DatePair;

        if (dataPointsByDatePair.TryGetValue(datePair, out DataPoint? existingDataPoint)
            && unitData.FiledDate <= existingDataPoint.FiledDate)
        {
            return;
        }

        dataPointsByDatePair[datePair] = new DataPoint(
            0,
            _companyId,
            factName.ToLowerInvariant(),
            datePair,
            unitData.Value,
            new DataPointUnit(0, unit),
            unitData.FiledDate);
    }
}
