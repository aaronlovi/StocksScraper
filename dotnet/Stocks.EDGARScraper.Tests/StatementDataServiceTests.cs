using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Persistence.Services;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class StatementDataServiceTests {
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static Mock<IDbmService> CreateMockWithHierarchy() {
        var mock = new Mock<IDbmService>();
        var concepts = new List<ConceptDetailsDTO> {
            new(1, 1, 1, 1, true, "Assets", "Assets", "Assets doc"),
            new(2, 1, 1, 1, false, "CurrentAssets", "Current Assets", "Current assets doc"),
            new(3, 1, 1, 1, false, "Cash", "Cash", "Cash doc")
        };
        _ = mock.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(concepts));
        var presentations = new List<PresentationDetailsDTO> {
            new(1, 1, 1, 0, 0, 0, "Statement - Test"),
            new(2, 2, 2, 0, 1, 1, "Statement - Test"),
            new(3, 3, 3, 0, 2, 2, "Statement - Test")
        };
        _ = mock.Setup(s => s.GetTaxonomyPresentationsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(presentations));
        var dataPoints = new List<DataPoint> {
            new(1UL, 1UL, "Assets", "ref",
                new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)),
                350000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 1),
            new(2UL, 1UL, "CurrentAssets", "ref",
                new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)),
                150000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 2),
            new(3UL, 1UL, "Cash", "ref",
                new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31)),
                100000, new DataPointUnit(1UL, "USD"), new DateOnly(2019, 3, 1), 1UL, 3)
        };
        _ = mock.Setup(s => s.GetDataPointsForSubmission(1UL, 1UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<DataPoint>>.Success(dataPoints));
        return mock;
    }

    [Fact]
    public async Task GetStatementData_ReturnsHierarchyWithDataPoints() {
        Mock<IDbmService> mock = CreateMockWithHierarchy();
        var service = new StatementDataService(mock.Object);

        Result<StatementData> result = await service.GetStatementData(
            1UL, 1UL, "Assets", 1, 10, null, Ct);

        Assert.True(result.IsSuccess);
        StatementData data = result.Value!;
        Assert.Equal(3, data.Hierarchy.Count);
        Assert.Equal("Assets", data.Hierarchy[0].Name);
        Assert.Equal("CurrentAssets", data.Hierarchy[1].Name);
        Assert.Equal("Cash", data.Hierarchy[2].Name);
        Assert.Equal(3, data.DataPointMap.Count);
        Assert.Equal(3, data.IncludedConceptIds.Count);
    }

    [Fact]
    public async Task GetStatementData_RespectsMaxDepth() {
        Mock<IDbmService> mock = CreateMockWithHierarchy();
        var service = new StatementDataService(mock.Object);

        Result<StatementData> result = await service.GetStatementData(
            1UL, 1UL, "Assets", 1, 1, null, Ct);

        Assert.True(result.IsSuccess);
        StatementData data = result.Value!;
        // maxDepth=1 means root (depth 0) and first children (depth 1), but not depth 2
        foreach (HierarchyNode node in data.Hierarchy)
            Assert.True(node.Depth <= 1, $"Node '{node.Name}' has depth {node.Depth} > 1");
        Assert.Equal(2, data.Hierarchy.Count);
    }

    [Fact]
    public async Task GetStatementData_UnknownConcept_ReturnsFailure() {
        Mock<IDbmService> mock = CreateMockWithHierarchy();
        var service = new StatementDataService(mock.Object);

        Result<StatementData> result = await service.GetStatementData(
            1UL, 1UL, "NonexistentConcept", 1, 10, null, Ct);

        Assert.True(result.IsFailure);
        Assert.Contains("not found in taxonomy", result.ErrorMessage!);
    }

    [Fact]
    public void BuildChildrenMap_DeduplicatesChildrenUnderSameParent() {
        // Simulates a DAG where concept 2 appears under concept 1 twice
        // (because concept 1 was traversed from two different parents)
        var nodes = new List<HierarchyNode> {
            new() { ConceptId = 1, Name = "Parent", Label = "Parent", Depth = 0, ParentConceptId = null },
            new() { ConceptId = 2, Name = "Child", Label = "Child", Depth = 1, ParentConceptId = 1 },
            new() { ConceptId = 2, Name = "Child", Label = "Child", Depth = 1, ParentConceptId = 1 }
        };
        var childrenMap = new Dictionary<long, List<HierarchyNode>>();
        var rootNodes = new List<HierarchyNode>();

        StatementDataService.BuildChildrenMap(nodes, childrenMap, rootNodes);

        Assert.Single(rootNodes);
        Assert.True(childrenMap.ContainsKey(1));
        Assert.Single(childrenMap[1]);
        Assert.Equal(2, childrenMap[1][0].ConceptId);
    }

    [Fact]
    public async Task ListStatements_ReturnsAvailableRoles() {
        var mock = new Mock<IDbmService>();
        var concepts = new List<ConceptDetailsDTO> {
            new(1, 1, 1, 1, true, "Assets", "Assets", "Assets doc"),
            new(2, 1, 1, 1, true, "Liabilities", "Liabilities", "Liabilities doc")
        };
        _ = mock.Setup(s => s.GetTaxonomyConceptsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(concepts));
        var presentations = new List<PresentationDetailsDTO> {
            new(1, 1, 1, 0, 0, 0, "Statement - Balance Sheet"),
            new(2, 2, 1, 0, 0, 0, "Statement - Liabilities")
        };
        _ = mock.Setup(s => s.GetTaxonomyPresentationsByTaxonomyType(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(presentations));

        var service = new StatementDataService(mock.Object);
        Result<IReadOnlyCollection<StatementListItem>> result =
            await service.ListStatements(1, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }
}
