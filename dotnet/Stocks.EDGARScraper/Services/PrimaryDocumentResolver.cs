using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stocks.DataModels.EdgarFileModels;
using Stocks.DataModels.Enums;
using Stocks.Shared;

namespace EDGARScraper.Services;

internal sealed class PrimaryDocumentResolver {
    private readonly ILogger _logger;

    internal PrimaryDocumentResolver(ILogger logger) {
        _logger = logger;
    }

    /// <summary>
    /// Reads submissions.zip and builds a mapping of filing reference (accession number)
    /// to primary document filename, filtered to 10-K filings for the specified CIKs.
    /// </summary>
    internal Dictionary<string, string> Resolve(
        string submissionsZipPath,
        HashSet<ulong> targetCiks,
        Dictionary<ulong, ulong> companyIdsByCiks) {

        var primaryDocsByFilingRef = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var zipReader = new ZipFileReader(submissionsZipPath);
        int numFiles = 0;

        foreach (string fileName in zipReader.EnumerateFileNames()) {
            if (!fileName.EndsWith(".json"))
                continue;

            ++numFiles;
            if (numFiles % 500 == 0)
                _logger.LogInformation("PrimaryDocumentResolver - Processed {NumFiles} files, found {NumDocs} primary docs",
                    numFiles, primaryDocsByFilingRef.Count);

            try {
                ProcessOneFile(zipReader, fileName, targetCiks, companyIdsByCiks, primaryDocsByFilingRef);
            } catch (Exception ex) {
                _logger.LogWarning(ex, "PrimaryDocumentResolver - Failed to process {FileName}", fileName);
            }
        }

        _logger.LogInformation("PrimaryDocumentResolver - Done. Processed {NumFiles} files, found {NumDocs} primary docs",
            numFiles, primaryDocsByFilingRef.Count);

        return primaryDocsByFilingRef;
    }

    private void ProcessOneFile(
        ZipFileReader zipReader,
        string fileName,
        HashSet<ulong> targetCiks,
        Dictionary<ulong, ulong> companyIdsByCiks,
        Dictionary<string, string> primaryDocsByFilingRef) {

        bool isSubmissionsFile = fileName.Contains("-submissions-");
        string content = zipReader.ExtractFileContent(fileName);

        ulong cik;
        FilingsDetails? filingsDetails;

        if (isSubmissionsFile) {
            // Format: CIK0000829323-submissions-001.json
            if (fileName.Length < 13)
                return;

            string cikStr = fileName[..13][3..];
            if (!ulong.TryParse(cikStr, out cik))
                return;

            // Skip if not a target company
            if (!targetCiks.Contains(cik))
                return;

            filingsDetails = JsonSerializer.Deserialize<FilingsDetails>(content, Conventions.DefaultOptions);
        } else {
            // Main submissions file: parse JSON to get CIK and filings
            RecentFilingsContainer? container = JsonSerializer.Deserialize<RecentFilingsContainer>(
                content, Conventions.DefaultOptions);
            if (container is null)
                return;

            cik = container.Cik;

            // Skip if not a target company
            if (!targetCiks.Contains(cik))
                return;

            filingsDetails = container.Filings.Recent;
        }

        if (filingsDetails is null)
            return;

        ExtractPrimaryDocs(filingsDetails, primaryDocsByFilingRef);
    }

    private static void ExtractPrimaryDocs(
        FilingsDetails filingsDetails,
        Dictionary<string, string> primaryDocsByFilingRef) {

        int count = filingsDetails.AccessionNumbersList.Count;
        if (filingsDetails.PrimaryDocumentsList.Count != count ||
            filingsDetails.FormsList.Count != count)
            return;

        for (int i = 0; i < count; i++) {
            // Only process annual report filings (10-K, 20-F, 40-F and their variants)
            FilingType filingType = filingsDetails.GetFilingTypeAtIndex(i);
            if (filingType is not FilingType.TenK and
                not FilingType.TenK_A and
                not FilingType.TenKT and
                not FilingType.TenKT_A and
                not FilingType.TwentyF and
                not FilingType.TwentyF_A and
                not FilingType.FortyF and
                not FilingType.FortyF_A)
                continue;

            string accessionNumber = filingsDetails.AccessionNumbersList[i];
            string primaryDocument = filingsDetails.PrimaryDocumentsList[i];

            if (string.IsNullOrWhiteSpace(primaryDocument))
                continue;

            primaryDocsByFilingRef[accessionNumber] = primaryDocument;
        }
    }
}
