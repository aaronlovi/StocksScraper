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
    public async Task ListAvailableStatements_ReturnsAbstractConceptsCsv() {
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

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "",
            date: DateOnly.FromDateTime(DateTime.Today),
            maxDepth: 10,
            format: "csv",
            listStatements: true,
            stdout: stdout,
            stderr: stderr
        );

        // Act
        int exitCode = await printer.PrintStatement();
        string output = stdout.ToString();
        string error = stderr.ToString();

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("ConceptName,Label,Documentation", output);
        Assert.Contains("Assets", output);
        Assert.Contains("Liabilities", output);
        Assert.DoesNotContain("Cash", output); // Not abstract
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
            format: "csv",
            listStatements: true,
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
    public async Task ListAvailableStatements_NoAbstractConcepts_PrintsHeaderOnly() {
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

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var printer = new StatementPrinter(
            mockDbmService.Object,
            cik: "1234",
            concept: "",
            date: DateOnly.FromDateTime(DateTime.Today),
            maxDepth: 10,
            format: "csv",
            listStatements: true,
            stdout: stdout,
            stderr: stderr
        );

        // Act
        int exitCode = await printer.PrintStatement();
        string output = stdout.ToString();
        string error = stderr.ToString();

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("ConceptName,Label,Documentation", output);
        Assert.DoesNotContain("Cash", output);
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
            format: "csv",
            listStatements: true,
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
            format: "csv",
            listStatements: true,
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
            format: "csv",
            listStatements: true,
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
    public async Task PrintStatement_ConceptNotFound_PrintsErrorAndNonZeroExit()
    {
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
            format: "csv",
            listStatements: false,
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
}
