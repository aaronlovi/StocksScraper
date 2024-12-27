using DataModels;
using DataModels.XbrlFileModels;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Utilities;

namespace EDGARScraper;

internal class XBRLFileParser
{
    private XbrlJson? _xbrlJson;
    private readonly string _content;
    private readonly Dictionary<string, Dictionary<string, Dictionary<DatePair, DataPoint>>> _dataPoints;

    internal XBRLFileParser(string content)
    {
        _content = content;
        _dataPoints = [];
    }

    public XbrlJson? XbrlJson => _xbrlJson;
    internal IReadOnlyDictionary<string, Dictionary<string, Dictionary<DatePair, DataPoint>>> DataPoints => _dataPoints;

    internal Results Parse()
    {
        try
        {
            _xbrlJson = JsonSerializer.Deserialize<XbrlJson>(_content);
            if (_xbrlJson is null)
                return Results.FailureResult("Failed to deserialize XBRL JSON.");

            foreach ((string factName, Fact fact) in _xbrlJson.Facts.UsGaap)
            {
                ProcessFact(factName, fact);
            }

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

        foreach ((string unit, List<Unit> units) in fact.Units)
        {
            ProcessUnitsForFact(unitsDataPoints, unit, units);
        }
    }

    private static void ProcessUnitsForFact(Dictionary<string, Dictionary<DatePair, DataPoint>> unitsDataPoints, string unit, List<Unit> units)
    {
        Dictionary<DatePair, DataPoint> dataPointsByDatePair =
            unitsDataPoints.GetOrCreateEntry(unit);

        foreach (Unit unitData in units)
        {
            ProcessUnitItemForFact(unit, dataPointsByDatePair, unitData);
        }
    }

    private static void ProcessUnitItemForFact(string unit, Dictionary<DatePair, DataPoint> dataPointsByDatePair, Unit unitData)
    {
        DatePair datePair = unitData.DatePair;

        if (dataPointsByDatePair.TryGetValue(datePair, out DataPoint? existingDataPoint)
            && unitData.FiledDate <= existingDataPoint.FiledDate)
        {
            return;
        }

        dataPointsByDatePair[datePair] = new DataPoint(
            datePair,
            unitData.Value,
            new DataPointUnit(unit),
            unitData.FiledDate);
    }
}
