#pragma warning disable IDE0290 // Use primary constructor

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stocks.DataModels.EdgarFileModels;
using Stocks.DataModels.Enums;

namespace Stocks.DataModels;

public class SubmissionJsonConverter
{
    private readonly ILogger<SubmissionJsonConverter> _logger;

    public SubmissionJsonConverter(IServiceProvider svp)
    {
        _logger = svp.GetRequiredService<ILogger<SubmissionJsonConverter>>();
    }

    public IReadOnlyCollection<Submission> ToSubmissions(FilingsDetails submissionJson)
    {
        int count = submissionJson.AccessionNumbersList.Count;
        if (submissionJson.FilingDatesList.Count != count ||
            submissionJson.ReportDatesList.Count != count ||
            submissionJson.AcceptanceDateTimesList.Count != count ||
            submissionJson.ActsList.Count != count ||
            submissionJson.FormsList.Count != count ||
            submissionJson.FileNumbersList.Count != count ||
            submissionJson.FilmNumbersList.Count != count ||
            submissionJson.ItemsList.Count != count ||
            submissionJson.CoreTypesList.Count != count ||
            submissionJson.IsXbrlList.Count != count ||
            submissionJson.IsInlineXbrlList.Count != count ||
            submissionJson.PrimaryDocumentsList.Count != count ||
            submissionJson.PrimaryDocDescriptionsList.Count != count)
        {
            return [];
        }

        return GetSubmissions(submissionJson);
    }

    private List<Submission> GetSubmissions(FilingsDetails submissionJson)
    {
        var retVal = new List<Submission>();

        for (int i = 0; i < submissionJson.AccessionNumbersList.Count; i++)
        {
            try
            {
                FilingType filingType = submissionJson.GetFilingTypeAtIndex(i);
                if (filingType is FilingType.Invalid) continue;

                FilingCategory filingCategory = submissionJson.GetFilingCategoryAtIndex(i);
                if (filingCategory is FilingCategory.Invalid) continue;

                bool res = DateOnly.TryParseExact(submissionJson.ReportDatesList[i], "yyyy-MM-dd", out DateOnly reportDate);
                if (!res) continue;

                bool parsedAcceptanceRes = DateTime.TryParse(
                    submissionJson.AcceptanceDateTimesList[i],
                    null,
                    DateTimeStyles.AdjustToUniversal,
                    out DateTime acceptanceTime_);
                DateTime? acceptanceTime = parsedAcceptanceRes ? acceptanceTime_ : null;

                retVal.Add(new Submission(
                    0ul, // SubmissionId is not available from the JSON
                    0ul, // CompanyId is not available from the JSON
                    submissionJson.AccessionNumbersList[i],
                    filingType,
                    filingCategory,
                    reportDate,
                    acceptanceTime));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSubmissions failed");
            }
        }

        return retVal;
    }
}
