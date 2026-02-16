using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.Persistence.Services;

public class StatementDataService {
    private readonly IDbmService _dbmService;

    public StatementDataService(IDbmService dbmService) {
        _dbmService = dbmService;
    }

    public async Task<Result<StatementData>> GetStatementData(
        ulong companyId,
        ulong submissionId,
        string concept,
        int taxonomyTypeId,
        int maxDepth,
        string? roleName,
        CancellationToken ct,
        TextWriter? warningWriter = null) {

        // 1. Load taxonomy concepts
        Result<IReadOnlyCollection<ConceptDetailsDTO>> conceptsResult =
            await _dbmService.GetTaxonomyConceptsByTaxonomyType(taxonomyTypeId, ct);
        if (conceptsResult.IsFailure || conceptsResult.Value is null) {
            return Result<StatementData>.Failure(ErrorCodes.GenericError,
                $"Could not load taxonomy concepts for taxonomy type {taxonomyTypeId}.");
        }

        IReadOnlyCollection<ConceptDetailsDTO> concepts = conceptsResult.Value;

        // 2. Load presentation hierarchy
        Result<IReadOnlyCollection<PresentationDetailsDTO>> presentationsResult =
            await _dbmService.GetTaxonomyPresentationsByTaxonomyType(taxonomyTypeId, ct);
        if (presentationsResult.IsFailure || presentationsResult.Value is null) {
            return Result<StatementData>.Failure(ErrorCodes.GenericError,
                $"Could not load taxonomy presentation hierarchy for taxonomy type {taxonomyTypeId}.");
        }


        IReadOnlyCollection<PresentationDetailsDTO> presentations = presentationsResult.Value;

        // 3. Find root concept
        ConceptDetailsDTO? rootConcept = null;
        foreach (ConceptDetailsDTO c in concepts) {
            if (c.Name.EqualsOrdinalIgnoreCase(concept)) {
                rootConcept = c;
                break;
            }
            if (long.TryParse(concept, out long conceptId) && c.ConceptId == conceptId) {
                rootConcept = c;
                break;
            }
        }
        if (rootConcept is null) {
            return Result<StatementData>.Failure(ErrorCodes.NotFound,
                $"Concept '{concept}' not found in taxonomy.");
        }

        // 4. Resolve role name

        string? roleNameToUse = roleName;
        if (string.IsNullOrWhiteSpace(roleNameToUse)) {
            var matchingRoles = new List<string>();
            foreach (PresentationDetailsDTO p in presentations) {
                if (p.ConceptId != rootConcept.ConceptId)
                    continue;
                if (string.IsNullOrWhiteSpace(p.RoleName))
                    continue;
                bool alreadyAdded = false;
                foreach (string role in matchingRoles) {
                    if (role.EqualsOrdinalIgnoreCase(p.RoleName)) {
                        alreadyAdded = true;
                        break;
                    }
                }
                if (!alreadyAdded)
                    matchingRoles.Add(p.RoleName);
            }
            if (matchingRoles.Count == 1) {
                roleNameToUse = matchingRoles[0];
            } else if (matchingRoles.Count == 0) {
                return Result<StatementData>.Failure(ErrorCodes.NotFound,
                    $"No presentation role found for concept '{rootConcept.Name}'.");
            } else {
                return Result<StatementData>.Failure(ErrorCodes.GenericError,
                    $"Multiple presentation roles found for concept '{rootConcept.Name}'. Please specify --role.");
            }
        }

        Dictionary<long, List<PresentationDetailsDTO>> parentToChildren =
            BuildParentToChildrenMap(presentations, roleNameToUse!);

        // 5. Load data points for the submission
        Result<IReadOnlyCollection<DataPoint>> dataPointsResult =
            await _dbmService.GetDataPointsForSubmission(companyId, submissionId, ct);
        if (dataPointsResult.IsFailure || dataPointsResult.Value is null) {

            return Result<StatementData>.Failure(ErrorCodes.GenericError,
                "Could not load data points for company/submission.");
        }


        var dataPointMap = new Dictionary<long, DataPoint>();
        foreach (DataPoint dp in dataPointsResult.Value)
            dataPointMap[dp.TaxonomyConceptId] = dp;

        // 6. Traverse the taxonomy tree
        Dictionary<long, ConceptDetailsDTO> conceptMap = new();
        foreach (ConceptDetailsDTO c in concepts)
            conceptMap[c.ConceptId] = c;
        var traverseCtx = new TraverseContext(
            rootConcept.ConceptId,
            parentToChildren,
            conceptMap,
            0,
            maxDepth,
            null,
            new HashSet<long>()
        );
        Result<List<HierarchyNode>> hierarchyResult = TraverseConceptTree(traverseCtx, warningWriter);
        if (hierarchyResult.IsFailure || hierarchyResult.Value is null) {

            return Result<StatementData>.Failure(ErrorCodes.GenericError,
                hierarchyResult.ErrorMessage ?? "Failed to traverse concept tree.");
        }


        List<HierarchyNode> hierarchy = hierarchyResult.Value;

        // 7. Build children map and compute included concepts
        var childrenMap = new Dictionary<long, List<HierarchyNode>>();
        var rootNodes = new List<HierarchyNode>();
        BuildChildrenMap(hierarchy, childrenMap, rootNodes);
        var includedConceptIds = new HashSet<long>();
        foreach (HierarchyNode rootNode in rootNodes)
            _ = HasValueOrChildValue(rootNode.ConceptId, childrenMap, dataPointMap, includedConceptIds);

        return Result<StatementData>.Success(
            new StatementData(hierarchy, dataPointMap, includedConceptIds, childrenMap, rootNodes));
    }

    public async Task<Result<IReadOnlyCollection<StatementListItem>>> ListStatements(
        int taxonomyTypeId,
        CancellationToken ct) {

        Result<IReadOnlyCollection<ConceptDetailsDTO>> conceptsResult =
            await _dbmService.GetTaxonomyConceptsByTaxonomyType(taxonomyTypeId, ct);
        if (conceptsResult.IsFailure || conceptsResult.Value is null) {
            return Result<IReadOnlyCollection<StatementListItem>>.Failure(ErrorCodes.GenericError,
                $"Could not load taxonomy concepts for taxonomy type {taxonomyTypeId}.");
        }

        IReadOnlyCollection<ConceptDetailsDTO> concepts = conceptsResult.Value;

        Result<IReadOnlyCollection<PresentationDetailsDTO>> presentationsResult =
            await _dbmService.GetTaxonomyPresentationsByTaxonomyType(taxonomyTypeId, ct);
        if (presentationsResult.IsFailure || presentationsResult.Value is null) {
            return Result<IReadOnlyCollection<StatementListItem>>.Failure(ErrorCodes.GenericError,
                $"Could not load taxonomy presentation hierarchy for taxonomy type {taxonomyTypeId}.");
        }


        IReadOnlyCollection<PresentationDetailsDTO> presentations = presentationsResult.Value;

        var roleRoots = new Dictionary<string, long>();
        foreach (PresentationDetailsDTO p in presentations) {
            if (p.Depth != 1)
                continue;
            if (string.IsNullOrWhiteSpace(p.RoleName))
                continue;
            if (!roleRoots.ContainsKey(p.RoleName))
                roleRoots[p.RoleName] = p.ConceptId;
        }

        var items = new List<StatementListItem>();
        foreach ((string roleNameEntry, long rootConceptId) in roleRoots) {
            ConceptDetailsDTO? root = null;
            foreach (ConceptDetailsDTO c in concepts) {
                if (c.ConceptId == rootConceptId) {
                    root = c;
                    break;
                }
            }
            items.Add(new StatementListItem(
                roleNameEntry,
                root?.Name ?? string.Empty,
                root?.Label ?? string.Empty));
        }

        return Result<IReadOnlyCollection<StatementListItem>>.Success(items);
    }

    // --- Internal helpers ---

    private static Result<List<HierarchyNode>> TraverseConceptTree(TraverseContext ctx, TextWriter? warningWriter) {
        var result = new List<HierarchyNode>();
        string? error = Traverse(ctx, result, warningWriter);
        if (error is not null)
            return Result<List<HierarchyNode>>.Failure(ErrorCodes.GenericError, error);
        return Result<List<HierarchyNode>>.Success(result);
    }

    private static string? Traverse(TraverseContext ctx, List<HierarchyNode> result, TextWriter? warningWriter) {
        if (ctx.Depth > ctx.MaxDepth)
            return null;
        if (!ctx.ConceptMap.TryGetValue(ctx.ConceptId, out ConceptDetailsDTO? concept))
            return $"ConceptId {ctx.ConceptId} not found in concept map.";
        if (!ctx.Visited.Add(ctx.ConceptId)) {
            warningWriter?.WriteLine(
                $"WARNING: Cycle detected at conceptId {ctx.ConceptId}, skipping to prevent infinite recursion.");
            return null;
        }
        result.Add(new HierarchyNode {
            ConceptId = concept.ConceptId,
            Name = concept.Name ?? string.Empty,
            Label = concept.Label ?? string.Empty,
            Depth = ctx.Depth,
            ParentConceptId = ctx.ParentConceptId,
        });
        if (ctx.ParentToChildren.TryGetValue(ctx.ConceptId, out List<PresentationDetailsDTO>? children)) {
            var seenChildConceptIds = new HashSet<long>();
            foreach (PresentationDetailsDTO child in children) {
                if (!seenChildConceptIds.Add(child.ConceptId))
                    continue;
                TraverseContext childCtx = ctx with {
                    ConceptId = child.ConceptId,
                    Depth = ctx.Depth + 1,
                    ParentConceptId = ctx.ConceptId
                };
                string? err = Traverse(childCtx, result, warningWriter);
                if (err is not null)
                    return err;
            }
        }
        _ = ctx.Visited.Remove(ctx.ConceptId);
        return null;
    }

    internal static Dictionary<long, List<PresentationDetailsDTO>> BuildParentToChildrenMap(
        IEnumerable<PresentationDetailsDTO> presentations,
        string roleName) {
        var parentToChildren = new Dictionary<long, List<PresentationDetailsDTO>>();
        foreach (PresentationDetailsDTO pres in presentations) {
            if (!string.Equals(pres.RoleName, roleName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!parentToChildren.TryGetValue(pres.ParentConceptId, out List<PresentationDetailsDTO>? children)) {
                children = new List<PresentationDetailsDTO>();
                parentToChildren[pres.ParentConceptId] = children;
            }
            children.Add(pres);
        }
        return parentToChildren;
    }

    internal static void BuildChildrenMap(
        List<HierarchyNode> nodes,
        Dictionary<long, List<HierarchyNode>> childrenMap,
        List<HierarchyNode> rootNodes) {
        foreach (HierarchyNode node in nodes) {
            if (node.ParentConceptId.HasValue) {
                long parentId = node.ParentConceptId.Value;
                if (!childrenMap.TryGetValue(parentId, out List<HierarchyNode>? children)) {
                    children = new List<HierarchyNode>();
                    childrenMap[parentId] = children;
                }
                children.Add(node);
            } else {
                rootNodes.Add(node);
            }
        }
    }

    internal static bool HasValueOrChildValue(
        long conceptId,
        Dictionary<long, List<HierarchyNode>> childrenMap,
        Dictionary<long, DataPoint> dataPointMap,
        HashSet<long> includedConceptIds) {
        bool hasValue = dataPointMap.ContainsKey(conceptId);
        bool hasChildValue = false;
        if (childrenMap.TryGetValue(conceptId, out List<HierarchyNode>? children)) {
            foreach (HierarchyNode child in children) {
                if (HasValueOrChildValue(child.ConceptId, childrenMap, dataPointMap, includedConceptIds))
                    hasChildValue = true;
            }
        }
        if (hasValue || hasChildValue)
            _ = includedConceptIds.Add(conceptId);
        return hasValue || hasChildValue;
    }

    private record TraverseContext(
        long ConceptId,
        Dictionary<long, List<PresentationDetailsDTO>> ParentToChildren,
        Dictionary<long, ConceptDetailsDTO> ConceptMap,
        int Depth,
        int MaxDepth,
        long? ParentConceptId,
        HashSet<long> Visited
    );
}
