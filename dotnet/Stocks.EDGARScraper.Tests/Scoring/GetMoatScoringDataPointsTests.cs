using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class GetMoatScoringDataPointsTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private const ulong CompanyId = 1;
    private const ulong CompanyCik = 320193;
    private const ulong Company2Id = 2;
    private const ulong Company2Cik = 789019;
    private const ulong Company3Id = 3;
    private const ulong Company3Cik = 66740;

    private async Task SeedCompanyAndTaxonomy() {
        await _dbm.BulkInsertCompanies([
            new Company(CompanyId, CompanyCik, "EDGAR"),
            new Company(Company2Id, Company2Cik, "EDGAR"),
            new Company(Company3Id, Company3Cik, "EDGAR"),
        ], _ct);

        await _dbm.EnsureTaxonomyType("us-gaap", 2024, _ct);
        await _dbm.BulkInsertTaxonomyConcepts([
            new ConceptDetailsDTO(100, 1, 1, 0, false, "StockholdersEquity", "Stockholders Equity", ""),
            new ConceptDetailsDTO(101, 1, 1, 0, false, "RetainedEarningsAccumulatedDeficit", "Retained Earnings", ""),
            new ConceptDetailsDTO(102, 1, 2, 0, false, "NetIncomeLoss", "Net Income", ""),
            new ConceptDetailsDTO(103, 1, 2, 0, false, "Revenues", "Revenues", ""),
            new ConceptDetailsDTO(104, 1, 2, 0, false, "OperatingIncomeLoss", "Operating Income", ""),
        ], _ct);
    }

    private DataPoint MakeDataPoint(ulong dpId, ulong companyId, ulong submissionId, long conceptId,
        decimal value, DateOnly startDate, DateOnly endDate) {
        return new DataPoint(
            dpId, companyId, "fact", "ref",
            new DatePair(startDate, endDate),
            value,
            new DataPointUnit(1, "USD"),
            endDate,
            submissionId,
            conceptId);
    }

    private async Task SeedYearsOfData(ulong companyId, ulong baseSubId, ulong baseDpId,
        int startYear, int endYear, long conceptId) {
        var submissions = new List<Submission>();
        var dataPoints = new List<DataPoint>();
        for (int year = startYear; year <= endYear; year++) {
            ulong subId = baseSubId + (ulong)year;
            var reportDate = new DateOnly(year, 9, 28);
            submissions.Add(new Submission(subId, companyId, $"ref-{year}", FilingType.TenK,
                FilingCategory.Annual, reportDate, null));
            dataPoints.Add(MakeDataPoint(baseDpId + (ulong)year, companyId, subId, conceptId,
                year * 1_000_000m, reportDate, reportDate));
        }
        await _dbm.BulkInsertSubmissions(submissions, _ct);
        await _dbm.BulkInsertDataPoints(dataPoints, _ct);
    }

    [Theory]
    [InlineData(8, 2018)]
    [InlineData(5, 2021)]
    public async Task GetScoringDataPoints_RespectsYearLimit(int yearLimit, int expectedMinYear) {
        await SeedCompanyAndTaxonomy();
        await SeedYearsOfData(CompanyId, 100, 2000, 2016, 2025, 100);

        Result<IReadOnlyCollection<ScoringConceptValue>> result = await _dbm.GetScoringDataPoints(
            CompanyId, ["StockholdersEquity"], yearLimit, _ct);

        Assert.True(result.IsSuccess);
        var list = new List<ScoringConceptValue>(result.Value!);
        Assert.Equal(yearLimit, list.Count);

        foreach (ScoringConceptValue v in list) {
            Assert.True(v.ReportDate.Year >= expectedMinYear,
                $"Expected year >= {expectedMinYear}, got {v.ReportDate.Year}");
        }
    }

    [Fact]
    public async Task GetAllScoringDataPoints_WithYearLimit8_Returns8YearsPerCompany() {
        await SeedCompanyAndTaxonomy();

        var submissions = new List<Submission>();
        var dataPoints = new List<DataPoint>();
        ulong dpId = 3000;

        // Seed 10 years for Company 1
        for (int year = 2016; year <= 2025; year++) {
            ulong subId = (ulong)(100 + year);
            var reportDate = new DateOnly(year, 9, 28);
            submissions.Add(new Submission(subId, CompanyId, $"ref-c1-{year}", FilingType.TenK,
                FilingCategory.Annual, reportDate, null));
            dataPoints.Add(MakeDataPoint(dpId++, CompanyId, subId, 100,
                year * 1_000_000m, reportDate, reportDate));
        }

        // Seed 10 years for Company 2
        for (int year = 2016; year <= 2025; year++) {
            ulong subId = (ulong)(200 + year);
            var reportDate = new DateOnly(year, 12, 31);
            submissions.Add(new Submission(subId, Company2Id, $"ref-c2-{year}", FilingType.TenK,
                FilingCategory.Annual, reportDate, null));
            dataPoints.Add(MakeDataPoint(dpId++, Company2Id, subId, 100,
                year * 2_000_000m, reportDate, reportDate));
        }

        await _dbm.BulkInsertSubmissions(submissions, _ct);
        await _dbm.BulkInsertDataPoints(dataPoints, _ct);

        Result<IReadOnlyCollection<BatchScoringConceptValue>> result = await _dbm.GetAllScoringDataPoints(
            ["StockholdersEquity"], 8, _ct);

        Assert.True(result.IsSuccess);
        var list = new List<BatchScoringConceptValue>(result.Value!);

        // Count per company
        int company1Count = 0;
        int company2Count = 0;
        foreach (BatchScoringConceptValue v in list) {
            if (v.CompanyId == CompanyId) company1Count++;
            else if (v.CompanyId == Company2Id) company2Count++;
        }

        Assert.Equal(8, company1Count);
        Assert.Equal(8, company2Count);
    }
}
