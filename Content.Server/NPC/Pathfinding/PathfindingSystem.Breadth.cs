namespace Content.Server.NPC.Pathfinding;

public sealed partial class PathfindingSystem
{
    /*
     * Handles small tile-space searches. This does not use the NPC navmesh.
     */

    /// <summary>
    /// Pathfinding args for a 1-many path.
    /// </summary>
    public record struct BreadthPathArgs()
    {
        public required Vector2i Start;
        public required List<Vector2i> Ends;

        public bool Diagonals = false;

        /// <summary>
        /// Optional multiplier for traversing an edge. Return 0 or less to block traversal.
        /// </summary>
        public Func<Vector2i, Vector2i, float>? EdgeMultiplier;

        public int Limit = 10000;
    }

    /// <summary>
    /// Gets the lowest-cost tile path from start to any end.
    /// </summary>
    public SimplePathResult GetBreadthPath(BreadthPathArgs args)
    {
        var cameFrom = new Dictionary<Vector2i, Vector2i>();
        var costSoFar = new Dictionary<Vector2i, float>();
        var frontier = new PriorityQueue<Vector2i, float>();

        costSoFar[args.Start] = 0f;
        frontier.Enqueue(args.Start, 0f);
        var count = 0;

        while (frontier.TryDequeue(out var node, out _) && count < args.Limit)
        {
            count++;

            if (args.Ends.Contains(node))
            {
                var path = ReconstructPath(node, cameFrom);

                return new SimplePathResult()
                {
                    CameFrom = cameFrom,
                    Path = path,
                };
            }

            var gCost = costSoFar[node];

            if (args.Diagonals)
            {
                for (var x = -1; x <= 1; x++)
                {
                    for (var y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0)
                            continue;

                        var neighbor = node + new Vector2i(x, y);
                        var stepCost = x == 0 || y == 0 ? 1f : 1.41f;
                        var neighborCost = stepCost * (args.EdgeMultiplier?.Invoke(node, neighbor) ?? 1f);

                        if (neighborCost <= 0f)
                        {
                            continue;
                        }

                        // gScore is distance to the start node.
                        var gScore = gCost + neighborCost;

                        // Slower to get here so just ignore it.
                        if (costSoFar.TryGetValue(neighbor, out var nextValue) && gScore >= nextValue)
                        {
                            continue;
                        }

                        cameFrom[neighbor] = node;
                        costSoFar[neighbor] = gScore;
                        frontier.Enqueue(neighbor, gScore);
                    }
                }
            }
            else
            {
                for (var x = -1; x <= 1; x++)
                {
                    for (var y = -1; y <= 1; y++)
                    {
                        if ((x == 0) == (y == 0))
                            continue;

                        var neighbor = node + new Vector2i(x, y);
                        var neighborCost = args.EdgeMultiplier?.Invoke(node, neighbor) ?? 1f;

                        if (neighborCost <= 0f)
                            continue;

                        // gScore is distance to the start node.
                        var gScore = gCost + neighborCost;

                        // Slower to get here so just ignore it.
                        if (costSoFar.TryGetValue(neighbor, out var nextValue) && gScore >= nextValue)
                            continue;

                        cameFrom[neighbor] = node;
                        costSoFar[neighbor] = gScore;

                        frontier.Enqueue(neighbor, gScore);
                    }
                }
            }
        }

        return SimplePathResult.NoPath;
    }
}
