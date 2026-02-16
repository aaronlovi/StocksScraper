using System.Collections.Generic;

namespace Stocks.DataModels;

public record StatementData(
    List<HierarchyNode> Hierarchy,
    Dictionary<long, DataPoint> DataPointMap,
    HashSet<long> IncludedConceptIds,
    Dictionary<long, List<HierarchyNode>> ChildrenMap,
    List<HierarchyNode> RootNodes
);
