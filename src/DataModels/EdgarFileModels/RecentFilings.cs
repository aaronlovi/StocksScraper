﻿using System.Collections.Generic;
using System.Text.Json.Serialization;
using Stocks.Utilities.JsonUtils;

namespace Stocks.DataModels.EdgarFileModels;

/// <summary>
/// Contained in a "submissions" file
/// </summary>
public record RecentFilingsContainer
{
    [JsonPropertyName("cik"), JsonConverter(typeof(StringToUlongConverter))] public ulong Cik { get; init; }
    [JsonPropertyName("filings")] public RecentFilings Filings { get; init; } = new();
}

/// <summary>
/// Contained in a "submissions" file
/// </summary>
public record RecentFilings
{
    [JsonPropertyName("recent")] public FilingsDetails Recent { get; init; } = new();
}

/// <summary>
/// Contained in a "submissions" file
/// </summary>
public record FilingsDetails
{
    [JsonPropertyName("accessionNumber")] public List<string> AccessionNumbersList { get; init; } = [];
    [JsonPropertyName("filingDate")] public List<string> FilingDatesList { get; init; } = [];
    [JsonPropertyName("reportDate")] public List<string> ReportDatesList { get; init; } = [];
    [JsonPropertyName("acceptanceDateTime")] public List<string> AcceptanceDateTimesList { get; init; } = [];
    [JsonPropertyName("act")] public List<string> ActsList { get; init; } = [];
    [JsonPropertyName("form")] public List<string> FormsList { get; init; } = [];
    [JsonPropertyName("fileNumber")] public List<string> FileNumbersList { get; init; } = [];
    [JsonPropertyName("filmNumber")] public List<string> FilmNumbersList { get; init; } = [];
    [JsonPropertyName("items")] public List<string> ItemsList { get; init; } = [];
    [JsonPropertyName("core_type")] public List<string> CoreTypesList { get; init; } = [];
    [JsonPropertyName("isXBRL"), JsonConverter(typeof(IntListToBoolListConverter))] public List<bool> IsXbrlList { get; init; } = [];
    [JsonPropertyName("isInlineXBRL"), JsonConverter(typeof(IntListToBoolListConverter))] public List<bool> IsInlineXbrlList { get; init; } = [];
    [JsonPropertyName("primaryDocument")] public List<string> PrimaryDocumentsList { get; init; } = [];
    [JsonPropertyName("primaryDocDescription")] public List<string> PrimaryDocDescriptionsList { get; init; } = [];
}