using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Stocks.DataModels;
using Stocks.EDGARScraper.Services.Statements;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.EDGARScraper.Tests;

public class StatementPrinterTests {
    [Fact]
    public async Task ListAvailableStatements_ReturnsRoleCsv() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var company = new Company(1UL, 1234UL, "TestSource");
        var companies = new List<Company> { company };
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Success(companies));

        var concepts = new List<ConceptDetailsDTO>
        {
            new(1, 1, 1, 1, true, "Assets", "Assets", "Assets doc"),
            new(2, 1, 1, 1, true, "Liabilities", "Liabilities", "Liabilities doc"),
            new(3, 1, 1, 1, false, "Cash", "Cash", "Cash doc")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(concepts));
        var presentations = new List<PresentationDetailsDTO>
        {
            new(1, 1, 1, 0, 0, 0, "Statement - Balance Sheet"),
            new(2, 2, 1, 0, 0, 0, "Statement - Liabilities")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyPresentationsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(presentations));

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "",
            date: DateOnly.FromDateTime(DateTime.Today),
            maxDepth: 10,
            roleName: null,
            format: "csv",
            listStatements: true,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );

        // Act
        int exitCode = await printer.PrintStatement();
        string output = stdout.ToString();
        string error = stderr.ToString();

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("RoleName,RootConceptName,RootLabel", output);
        Assert.Contains("Statement - Balance Sheet,Assets", output);
        Assert.Contains("Statement - Liabilities,Liabilities", output);
        Assert.True(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task ListAvailableStatements_CompanyNotFound_PrintsErrorAndNonZeroExit() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var companies = new List<Company>(); // No companies returned
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Success(companies));
        _ = mockDbmService.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success([]));

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "9999",
            concept: "",
            date: DateOnly.FromDateTime(DateTime.Today),
            maxDepth: 10,
            roleName: null,
            format: "csv",
            listStatements: true,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );

        // Act
        int exitCode = await printer.PrintStatement();
        string error = stderr.ToString();

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("ERROR: Company with CIK", error);
    }

    [Fact]
    public async Task ListAvailableStatements_NoRoles_PrintsHeaderOnly() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var company = new Company(1UL, 1234UL, "TestSource");
        var companies = new List<Company> { company };
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Success(companies));
        var concepts = new List<ConceptDetailsDTO>
        {
            new(3, 1, 1, 1, false, "Cash", "Cash", "Cash doc")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(concepts));
        var presentations = new List<PresentationDetailsDTO>();
        _ = mockDbmService.Setup(s => s.GetTaxonomyPresentationsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(presentations));

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "",
            date: DateOnly.FromDateTime(DateTime.Today),
            maxDepth: 10,
            roleName: null,
            format: "csv",
            listStatements: true,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );

        // Act
        int exitCode = await printer.PrintStatement();
        string output = stdout.ToString();
        string error = stderr.ToString();

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("RoleName,RootConceptName,RootLabel", output);
        Assert.True(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task ListAvailableStatements_InvalidCik_PrintsErrorAndNonZeroExit() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "notanumber",
            concept: "",
            date: DateOnly.FromDateTime(DateTime.Today),
            maxDepth: 10,
            roleName: null,
            format: "csv",
            listStatements: true,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );

        // Act
        int exitCode = await printer.PrintStatement();
        string error = stderr.ToString();

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("ERROR: Invalid CIK", error);
    }

    [Fact]
    public async Task ListAvailableStatements_CompaniesLoadFailure_PrintsErrorAndNonZeroExit() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Failure(ErrorCodes.GenericError, "db fail"));
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "",
            date: DateOnly.FromDateTime(DateTime.Today),
            maxDepth: 10,
            roleName: null,
            format: "csv",
            listStatements: true,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );

        // Act
        int exitCode = await printer.PrintStatement();
        string error = stderr.ToString();

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("ERROR: Could not load companies", error);
    }

    [Fact]
    public async Task ListAvailableStatements_TaxonomyConceptsLoadFailure_PrintsErrorAndNonZeroExit() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var company = new Company(1UL, 1234UL, "TestSource");
        var companies = new List<Company> { company };
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Success(companies));
        _ = mockDbmService.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Failure(ErrorCodes.GenericError, "taxonomy fail"));
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "",
            date: DateOnly.FromDateTime(DateTime.Today),
            maxDepth: 10,
            roleName: null,
            format: "csv",
            listStatements: true,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );

        // Act
        int exitCode = await printer.PrintStatement();
        string error = stderr.ToString();

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("ERROR: Could not load taxonomy concepts", error);
    }

    [Fact]
    public async Task PrintStatement_ConceptNotFound_PrintsErrorAndNonZeroExit() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var company = new Company(1UL, 1234UL, "TestSource");
        var companies = new List<Company> { company };
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Success(companies));
        var concepts = new List<ConceptDetailsDTO>
        {
            new(1, 1, 1, 1, true, "Assets", "Assets", "Assets doc")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(concepts));
        var presentations = new List<PresentationDetailsDTO>();
        _ = mockDbmService.Setup(s => s.GetTaxonomyPresentationsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(presentations));

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "NonexistentConcept",
            date: DateOnly.FromDateTime(DateTime.Today),
            maxDepth: 10,
            roleName: null,
            format: "csv",
            listStatements: false,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );

        // Act
        int exitCode = await printer.PrintStatement();
        string error = stderr.ToString();

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("ERROR: Concept 'NonexistentConcept' not found in taxonomy.", error);
    }

    [Fact]
    public async Task PrintStatement_HierarchyCsv_Works() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var company = new Company(1UL, 1234UL, "TestSource");
        var companies = new List<Company> { company };
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Success(companies));
        var concepts = new List<ConceptDetailsDTO>
        {
            new(1, 1, 1, 1, true, "Assets", "Assets", "Assets doc"),
            new(2, 1, 1, 1, false, "CurrentAssets", "Current Assets", "Current assets doc"),
            new(3, 1, 1, 1, false, "Cash", "Cash", "Cash doc")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(concepts));
        var presentations = new List<PresentationDetailsDTO>
        {
            new(1, 1, 1, 0, 0, 0, "Statement - Test"), // Assets (root)
            new(2, 2, 2, 0, 1, 1, "Statement - Test"), // CurrentAssets child of Assets
            new(3, 3, 3, 0, 2, 2, "Statement - Test")  // Cash child of CurrentAssets
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyPresentationsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(presentations));
        var submissions = new List<Submission> { new(1UL, 1UL, "ref", 0, 0, new DateOnly(2019, 3, 1), null) };
        _ = mockDbmService.Setup(s => s.GetSubmissions(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Submission>>.Success(submissions));
        var dataPoints = new List<DataPoint>
        {
            new(1UL, 1UL, "Assets", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 350000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 1),
            new(2UL, 1UL, "CurrentAssets", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 150000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 2),
            new(3UL, 1UL, "Cash", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 100000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 3)
        };
        _ = mockDbmService.Setup(s => s.GetDataPointsForSubmission(1UL, 1UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<DataPoint>>.Success(dataPoints));
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "Assets",
            date: new DateOnly(2019, 3, 1),
            maxDepth: 10,
            roleName: null,
            format: "csv",
            listStatements: false,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );
        // Act
        int exitCode = await printer.PrintStatement();
        string output = stdout.ToString();
        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("ConceptName,Label,Value,Depth,ParentConceptName", output);
        Assert.Contains("Assets,\"Assets\",350000,0,", output);
        Assert.Contains("CurrentAssets,\"Current Assets\",150000,1,Assets", output);
        Assert.Contains("Cash,\"Cash\",100000,2,CurrentAssets", output);
    }

    [Fact]
    public async Task PrintStatement_HierarchyHtml_Works() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var company = new Company(1UL, 1234UL, "TestSource");
        var companies = new List<Company> { company };
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Success(companies));
        var concepts = new List<ConceptDetailsDTO>
        {
            new(1, 1, 1, 1, true, "Assets", "Assets", "Assets doc"),
            new(2, 1, 1, 1, false, "CurrentAssets", "Current Assets", "Current assets doc"),
            new(3, 1, 1, 1, false, "Cash", "Cash", "Cash doc")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(concepts));
        var presentations = new List<PresentationDetailsDTO>
        {
            new(1, 1, 1, 0, 0, 0, "Statement - Test"),
            new(2, 2, 2, 0, 1, 1, "Statement - Test"),
            new(3, 3, 3, 0, 2, 2, "Statement - Test")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyPresentationsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(presentations));
        var submissions = new List<Submission> { new(1UL, 1UL, "ref", 0, 0, new DateOnly(2019, 3, 1), null) };
        _ = mockDbmService.Setup(s => s.GetSubmissions(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Submission>>.Success(submissions));
        var dataPoints = new List<DataPoint>
        {
            new(1UL, 1UL, "Assets", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 350000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 1),
            new(2UL, 1UL, "CurrentAssets", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 150000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 2),
            new(3UL, 1UL, "Cash", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 100000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 3)
        };
        _ = mockDbmService.Setup(s => s.GetDataPointsForSubmission(1UL, 1UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<DataPoint>>.Success(dataPoints));
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "Assets",
            date: new DateOnly(2019, 3, 1),
            maxDepth: 10,
            roleName: null,
            format: "html",
            listStatements: false,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );
        // Act
        int exitCode = await printer.PrintStatement();
        string output = stdout.ToString();
        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("statement-rows", output);
        Assert.Contains("toggle-breadcrumbs", output);
        Assert.Contains("breadcrumb", output);
        Assert.Contains("<span>Assets</span>", output);
        Assert.Contains("350,000 USD", output);
        Assert.Contains("<span>CurrentAssets</span>", output);
        Assert.Contains("150,000 USD", output);
        Assert.Contains("<span>Cash</span>", output);
        Assert.Contains("100,000 USD", output);
    }

    [Fact]
    public async Task PrintStatement_HierarchyJson_Works() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var company = new Company(1UL, 1234UL, "TestSource");
        var companies = new List<Company> { company };
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Success(companies));
        var concepts = new List<ConceptDetailsDTO>
        {
            new(1, 1, 1, 1, true, "Assets", "Assets", "Assets doc"),
            new(2, 1, 1, 1, false, "CurrentAssets", "Current Assets", "Current assets doc"),
            new(3, 1, 1, 1, false, "Cash", "Cash", "Cash doc")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(concepts));
        var presentations = new List<PresentationDetailsDTO>
        {
            new(1, 1, 1, 0, 0, 0, "Statement - Test"),
            new(2, 2, 2, 0, 1, 1, "Statement - Test"),
            new(3, 3, 3, 0, 2, 2, "Statement - Test")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyPresentationsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(presentations));
        var submissions = new List<Submission> { new(1UL, 1UL, "ref", 0, 0, new DateOnly(2019, 3, 1), null) };
        _ = mockDbmService.Setup(s => s.GetSubmissions(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Submission>>.Success(submissions));
        var dataPoints = new List<DataPoint>
        {
            new(1UL, 1UL, "Assets", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 350000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 1),
            new(2UL, 1UL, "CurrentAssets", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 150000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 2),
            new(3UL, 1UL, "Cash", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 100000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 3)
        };
        _ = mockDbmService.Setup(s => s.GetDataPointsForSubmission(1UL, 1UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<DataPoint>>.Success(dataPoints));
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "Assets",
            date: new DateOnly(2019, 3, 1),
            maxDepth: 10,
            roleName: null,
            format: "json",
            listStatements: false,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );
        // Act
        int exitCode = await printer.PrintStatement();
        string output = stdout.ToString();
        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("\"ConceptName\":\"Assets\"", output);
        Assert.Contains("\"Value\":350000", output);
        Assert.Contains("\"Children\":", output);
    }

    [Fact]
    public async Task PrintStatement_RecursionLimit_Enforced() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var company = new Company(1UL, 1234UL, "TestSource");
        var companies = new List<Company> { company };
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Success(companies));
        var concepts = new List<ConceptDetailsDTO>
        {
            new(1, 1, 1, 1, true, "Assets", "Assets", "Assets doc"),
            new(2, 1, 1, 1, false, "CurrentAssets", "Current Assets", "Current assets doc"),
            new(3, 1, 1, 1, false, "Cash", "Cash", "Cash doc")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(concepts));
        var presentations = new List<PresentationDetailsDTO>
        {
            new(1, 1, 1, 0, 0, 0, "Statement - Test"),
            new(2, 2, 2, 0, 1, 1, "Statement - Test"),
            new(3, 3, 3, 0, 2, 2, "Statement - Test")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyPresentationsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(presentations));
        var submissions = new List<Submission> { new(1UL, 1UL, "ref", 0, 0, new DateOnly(2019, 3, 1), null) };
        _ = mockDbmService.Setup(s => s.GetSubmissions(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Submission>>.Success(submissions));
        var dataPoints = new List<DataPoint>
        {
            new(1UL, 1UL, "Assets", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 350000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 1),
            new(2UL, 1UL, "CurrentAssets", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 150000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 2),
            new(3UL, 1UL, "Cash", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)), 100000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 3)
        };
        _ = mockDbmService.Setup(s => s.GetDataPointsForSubmission(1UL, 1UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<DataPoint>>.Success(dataPoints));
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "Assets",
            date: new DateOnly(2019, 3, 1),
            maxDepth: 1, // Only root and first child
            roleName: null,
            format: "csv",
            listStatements: false,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );
        // Act
        int exitCode = await printer.PrintStatement();
        string output = stdout.ToString();
        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Assets,\"Assets\",350000,0,", output);
        Assert.Contains("CurrentAssets,\"Current Assets\",150000,1,Assets", output);
        Assert.DoesNotContain("Cash,\"Cash\",100000,2,CurrentAssets", output); // Should not appear
    }

    [Fact]
    public async Task PrintStatement_MissingSubmissionForDate_PrintsError() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var company = new Company(1UL, 1234UL, "TestSource");
        var companies = new List<Company> { company };
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Success(companies));
        var concepts = new List<ConceptDetailsDTO>
        {
            new(1, 1, 1, 1, true, "Assets", "Assets", "Assets doc")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(concepts));
        var presentations = new List<PresentationDetailsDTO>
        {
            new(1, 1, 1, 0, 0, 0, "Statement - Test")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyPresentationsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(presentations));
        // Only submission is after the requested date, so none should be found
        var submissions = new List<Submission> { new(1UL, 1UL, "ref", 0, 0, new DateOnly(2020, 1, 1), null) };
        _ = mockDbmService.Setup(s => s.GetSubmissions(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Submission>>.Success(submissions));
        var dataPoints = new List<DataPoint>();
        _ = mockDbmService.Setup(s => s.GetDataPointsForSubmission(1UL, 1UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<DataPoint>>.Success(dataPoints));
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "Assets",
            date: new DateOnly(2019, 3, 1), // No submission on or before this date
            maxDepth: 10,
            roleName: null,
            format: "csv",
            listStatements: false,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );
        // Act
        int exitCode = await printer.PrintStatement();
        string error = stderr.ToString();
        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("ERROR: No submission found for CIK", error);
    }

    [Fact]
    public async Task PrintStatement_MissingDataPoint_PrintsWarning() {
        // Arrange
        var mockDbmService = new Mock<IDbmService>();
        var company = new Company(1UL, 1234UL, "TestSource");
        var companies = new List<Company> { company };
        _ = mockDbmService.Setup(s => s.GetAllCompaniesByDataSource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Company>>.Success(companies));
        var concepts = new List<ConceptDetailsDTO>
        {
            new(1, 1, 1, 1, true, "Assets", "Assets", "Assets doc")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(concepts));
        var presentations = new List<PresentationDetailsDTO>
        {
            new(1, 1, 1, 0, 0, 0, "Statement - Test")
        };
        _ = mockDbmService.Setup(s => s.GetTaxonomyPresentationsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(presentations));
        var submissions = new List<Submission> { new(1UL, 1UL, "ref", 0, 0, new DateOnly(2019, 3, 1), null) };
        _ = mockDbmService.Setup(s => s.GetSubmissions(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<Submission>>.Success(submissions));
        var dataPoints = new List<DataPoint>(); // No data points
        _ = mockDbmService.Setup(s => s.GetDataPointsForSubmission(1UL, 1UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<DataPoint>>.Success(dataPoints));
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "Assets",
            date: new DateOnly(2019, 3, 1),
            maxDepth: 10,
            roleName: null,
            format: "csv",
            listStatements: false,
            taxonomyTypeId: 1,
            stdout: stdout,
            stderr: stderr
        );
        // Act
        int exitCode = await printer.PrintStatement();
        string error = stderr.ToString();
        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("WARNING: No data point found for concept 'Assets", error);
    }
}
