using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;

namespace Content.Shared.Construction;

public abstract partial class SharedConstructionSystem
{
    private readonly Dictionary<ConstructionPrototype, ConstructionGuide> _guideCache = new();

    /// <summary>
    ///     Returns a <see cref="ConstructionGuide"/> for a given <see cref="ConstructionPrototype"/>,
    ///     generating and caching it as needed.
    /// </summary>
    /// <param name="construction">The construction prototype to generate the guide for. We must be able to pathfind
    ///                            from its starting node to its ending node to be able to generate a guide for it.</param>
    /// <returns>The guide for the given construction, or null if we can't pathfind from the start node to the
    ///          end node on that construction.</returns>
    public ConstructionGuide? GetGuide(ConstructionPrototype construction)
    {
        // NOTE: This method might allocate a fair bit, but do not worry!
        // This method is specifically designed to generate guides once and cache the results,
        // therefore we don't need to worry too much about the performance of this.

        if (_guideCache.TryGetValue(construction, out var guide))
            return guide;

        if (!PrototypeManager.Resolve(construction.Graph, out ConstructionGraphPrototype? graph))
            return null;

        if (!graph.Nodes.TryGetValue(construction.StartNode, out var startNode)
            || !graph.Nodes.TryGetValue(construction.TargetNode, out var targetNode))
            return null;

        if (graph.Path(construction.StartNode, construction.TargetNode) is not { } path
            || path.Length == 0)
            return null;

        var step = 1;

        var entries = new List<ConstructionGuideEntry>()
        {
            new()
            {
                Localization = construction.Type == ConstructionType.Structure
                    ? "construction-presenter-to-build" : "construction-presenter-to-craft",
                EntryNumber = step,
            }
        };

        var conditions = new HashSet<string>();

        var node = startNode;
        var index = 0;
        while (node != targetNode)
        {
            if (!node.TryGetEdge(path[index].Name, out var edge))
                return null;

            if (step == 1)
            {
                foreach (var graphStep in edge.Steps)
                {
                    if (graphStep is not EntityInsertConstructionGraphStep insertStep)
                        return null;

                    entries.Add(insertStep.GenerateGuideEntry());
                }

                foreach (var condition in construction.Conditions)
                {
                    if (condition.GenerateGuideEntry() is not { } conditionEntry)
                        continue;

                    conditionEntry.Padding += 4;
                    entries.Add(conditionEntry);
                }

                step++;
                node = path[index++];

                if (node != targetNode)
                    entries.Add(new ConstructionGuideEntry());

                continue;
            }

            var old = conditions;
            conditions = new HashSet<string>();

            foreach (var condition in edge.Conditions)
            {
                foreach (var conditionEntry in condition.GenerateGuideEntry())
                {
                    conditions.Add(conditionEntry.Localization);

                    if (conditionEntry.EntryNumber != null)
                    {
                        conditionEntry.EntryNumber = step++;
                    }
                    else
                    {
                        if (old.Contains(conditionEntry.Localization))
                            continue;

                        conditionEntry.Padding += 4;
                    }

                    entries.Add(conditionEntry);
                }
            }

            foreach (var graphStep in edge.Steps)
            {
                var entry = graphStep.GenerateGuideEntry();
                entry.EntryNumber = step++;
                entries.Add(entry);
            }

            node = path[index++];
        }

        guide = new ConstructionGuide(entries.ToArray());
        _guideCache[construction] = guide;
        return guide;
    }
}
