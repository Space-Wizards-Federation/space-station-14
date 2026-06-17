using System.Linq;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Content.Shared.Interaction.SharedInteractionSystem;

namespace Content.Shared.Construction
{
    public abstract partial class SharedConstructionSystem : EntitySystem
    {
        [Dependency] private IMapManager _mapManager = default!;
        [Dependency] private SharedMapSystem _map = default!;
        [Dependency] private SharedPopupSystem _popup = default!;
        [Dependency] protected IPrototypeManager PrototypeManager = default!;
        [Dependency] protected SharedTransformSystem TransformSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ConstructionComponent, GetVerbsEvent<Verb>>(AddDeconstructVerb);
            SubscribeLocalEvent<ConstructionComponent, ExaminedEvent>(HandleConstructionExamined);
        }

        /// <summary>
        ///     Get predicate for construction obstruction checks.
        /// </summary>
        public Ignored? GetPredicate(bool canBuildInImpassable, MapCoordinates coords)
        {
            if (!canBuildInImpassable)
                return null;

            if (!_mapManager.TryFindGridAt(coords, out var gridUid, out var grid))
                return null;

            var ignored = _map.GetAnchoredEntities((gridUid, grid), coords).ToHashSet();
            return e => ignored.Contains(e);
        }

        public string GetExamineName(GenericPartInfo info)
        {
            if (info.ExamineName is not null)
                return Loc.GetString(info.ExamineName.Value);

            return PrototypeManager.Index(info.DefaultPrototype).Name;
        }

        private void AddDeconstructVerb(EntityUid uid, ConstructionComponent component, GetVerbsEvent<Verb> args)
        {
            if (!args.CanAccess || !args.CanInteract || args.Hands == null)
                return;

            if (component.TargetNode == component.DeconstructionNode ||
                component.Node == component.DeconstructionNode)
                return;

            if (!PrototypeManager.TryIndex(component.Graph, out ConstructionGraphPrototype? graph))
                return;

            if (component.DeconstructionNode == null)
                return;

            if (GetCurrentNode(uid, component) is not { } currentNode)
                return;

            if (graph.Path(currentNode.Name, component.DeconstructionNode) is not { } path || path.Length == 0)
                return;

            var user = args.User;
            var verb = new Verb
            {
                Text = Loc.GetString("deconstructible-verb-begin-deconstruct"),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/hammer_scaled.svg.192dpi.png")),
                Act = () =>
                {
                    SetPathfindingTarget(uid, component.DeconstructionNode, component);
                    if (component.TargetNode == null)
                    {
                        _popup.PopupPredicted(Loc.GetString("deconstructible-verb-activate-no-target-text"), uid, user);
                    }
                    else
                    {
                        _popup.PopupPredicted(Loc.GetString("deconstructible-verb-activate-text"), user, user);
                    }
                }
            };

            args.Verbs.Add(verb);
        }

        private void HandleConstructionExamined(EntityUid uid, ConstructionComponent component, ExaminedEvent args)
        {
            using (args.PushGroup(nameof(ConstructionComponent)))
            {
                if (GetTargetNode(uid, component) is { } target)
                {
                    if (target.Name == component.DeconstructionNode)
                    {
                        args.PushMarkup(Loc.GetString("deconstruction-header-text") + "\n");
                    }
                    else
                    {
                        args.PushMarkup(Loc.GetString(
                            "construction-component-to-create-header",
                            ("targetName", target.Name)) + "\n");
                    }
                }

                if (component.EdgeIndex == null && GetTargetEdge(uid, component) is { } targetEdge)
                {
                    var preventStepExamine = false;

                    foreach (var condition in targetEdge.Conditions)
                    {
                        preventStepExamine |= condition.DoExamine(args);
                    }

                    if (!preventStepExamine)
                        targetEdge.Steps[0].DoExamine(args);
                    return;
                }

                if (GetCurrentEdge(uid, component) is { } edge)
                {
                    var preventStepExamine = false;

                    foreach (var condition in edge.Conditions)
                    {
                        preventStepExamine |= condition.DoExamine(args);
                    }

                    if (!preventStepExamine && component.StepIndex < edge.Steps.Count)
                        edge.Steps[component.StepIndex].DoExamine(args);
                }
            }
        }

        /// <summary>
        ///     Gets the current construction graph of an entity, or null.
        /// </summary>
        /// <param name="uid">The target entity.</param>
        /// <param name="construction">The construction component of the target entity. Will be resolved if null.</param>
        /// <returns>The current construction graph of an entity or null if invalid. Also returns null if the entity
        ///          does not have a <see cref="ConstructionComponent"/>.</returns>
        /// <remarks>An entity with a valid construction state will always have a valid graph.</remarks>
        public ConstructionGraphPrototype? GetCurrentGraph(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction, false))
                return null;

            return PrototypeManager.TryIndex(construction.Graph, out ConstructionGraphPrototype? graph) ? graph : null;
        }

        /// <summary>
        ///     Gets the construction graph node the entity is currently at, or null.
        /// </summary>
        /// <param name="uid">The target entity.</param>
        /// <param name="construction">The construction component of the target entity. Will be resolved if null.</param>
        /// <returns>The current construction graph node the entity is currently at, or null if invalid. Also returns
        ///          null if the entity does not have a <see cref="ConstructionComponent"/>.</returns>
        /// <remarks>An entity with a valid construction state will always be at a valid node.</remarks>
        public ConstructionGraphNode? GetCurrentNode(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction, false))
                return null;

            if (construction.Node is not { } nodeIdentifier)
                return null;

            return GetCurrentGraph(uid, construction) is not { } graph ? null : GetNodeFromGraph(graph, nodeIdentifier);
        }

        /// <summary>
        ///     Gets the construction graph edge the entity is currently at, or null.
        /// </summary>
        /// <param name="uid">The target entity.</param>
        /// <param name="construction">The construction component of the target entity. Will be resolved if null.</param>
        /// <returns>The construction graph edge the entity is currently at, if any. Also returns null if the entity
        ///          does not have a <see cref="ConstructionComponent"/>.</returns>
        /// <remarks>An entity with a valid construction state might not always be at an edge.</remarks>
        public ConstructionGraphEdge? GetCurrentEdge(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction, false))
                return null;

            if (construction.EdgeIndex is not { } edgeIndex)
                return null;

            return GetCurrentNode(uid, construction) is not { } node ? null : GetEdgeFromNode(node, edgeIndex);
        }

        /// <summary>
        ///     Gets the construction graph node the entity's construction pathfinding is currently targeting, if any.
        /// </summary>
        /// <param name="uid">The target entity.</param>
        /// <param name="construction">The construction component of the target entity. Will be resolved if null.</param>
        /// <returns>The construction graph node the entity's construction pathfinding is currently targeting, or null
        ///          if it's not currently targeting any node. Also returns null if the entity does not have a
        ///          <see cref="ConstructionComponent"/>.</returns>
        /// <remarks>Target nodes are entirely optional and only used for pathfinding purposes.</remarks>
        public ConstructionGraphNode? GetTargetNode(EntityUid uid, ConstructionComponent? construction)
        {
            if (!Resolve(uid, ref construction))
                return null;

            if (construction.TargetNode is not { } targetNodeId)
                return null;

            if (GetCurrentGraph(uid, construction) is not { } graph)
                return null;

            return GetNodeFromGraph(graph, targetNodeId);
        }

        /// <summary>
        ///     Gets the construction graph edge the entity's construction pathfinding is currently targeting, if any.
        /// </summary>
        /// <param name="uid">The target entity.</param>
        /// <param name="construction">The construction component of the target entity. Will be resolved if null.</param>
        /// <returns>The construction graph edge the entity's construction pathfinding is currently targeting, or null
        ///          if it's not currently targeting any edge. Also returns null if the entity does not have a
        ///          <see cref="ConstructionComponent"/>.</returns>
        /// <remarks>Target edges are entirely optional and only used for pathfinding purposes. The targeted edge will
        ///          be an edge on the current construction node the entity is at.</remarks>
        public ConstructionGraphEdge? GetTargetEdge(EntityUid uid, ConstructionComponent? construction)
        {
            if (!Resolve(uid, ref construction))
                return null;

            if (construction.TargetEdgeIndex is not { } targetEdgeIndex)
                return null;

            if (GetCurrentNode(uid, construction) is not { } node)
                return null;

            return GetEdgeFromNode(node, targetEdgeIndex);
        }

        /// <summary>
        ///     Gets a node from a construction graph given its identifier.
        /// </summary>
        /// <param name="graph">The construction graph where to get the node.</param>
        /// <param name="id">The identifier that corresponds to the node.</param>
        /// <returns>The node that corresponds to the identifier, or null if it doesn't exist.</returns>
        public ConstructionGraphNode? GetNodeFromGraph(ConstructionGraphPrototype graph, string id)
        {
            return graph.Nodes.TryGetValue(id, out var node) ? node : null;
        }

        /// <summary>
        ///     Gets an edge from a construction node given its index.
        /// </summary>
        /// <param name="node">The construction node where to get the edge.</param>
        /// <param name="index">The index or position of the edge on the node.</param>
        /// <returns>The edge on that index in the construction node, or null if none.</returns>
        public ConstructionGraphEdge? GetEdgeFromNode(ConstructionGraphNode node, int index)
        {
            return node.Edges.Count > index ? node.Edges[index] : null;
        }

        /// <summary>
        ///     Gets a step from a construction edge given its index.
        /// </summary>
        /// <param name="edge">The construction edge where to get the step.</param>
        /// <param name="index">The index or position of the step on the edge.</param>
        /// <returns>The edge on that index in the construction edge, or null if none.</returns>
        public ConstructionGraphStep? GetStepFromEdge(ConstructionGraphEdge edge, int index)
        {
            return edge.Steps.Count > index ? edge.Steps[index] : null;
        }

        /// <summary>
        ///     Sets or clears a pathfinding target node for a given construction entity.
        /// </summary>
        /// <param name="uid">The target entity.</param>
        /// <param name="targetNodeId">The target node to pathfind, or null to clear the current pathfinding node.</param>
        /// <param name="construction">The construction component of the target entity. Will be resolved if null.</param>
        /// <returns>Whether we could set/clear the pathfinding target node.</returns>
        public bool SetPathfindingTarget(EntityUid uid, string? targetNodeId, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction))
                return false;

            ClearPathfinding(uid, construction);

            if (targetNodeId == null)
                return true;

            if (GetCurrentGraph(uid, construction) is not { } graph)
                return false;

            if (GetNodeFromGraph(graph, construction.Node) is not { } node)
                return false;

            if (GetNodeFromGraph(graph, targetNodeId) is not { } targetNode)
                return false;

            return UpdatePathfinding(uid, graph, node, targetNode, GetCurrentEdge(uid, construction), construction);
        }

        /// <summary>
        ///     Updates the pathfinding state for the current construction state of an entity.
        /// </summary>
        /// <param name="uid">The target entity.</param>
        /// <param name="construction">The construction component of the target entity. Will be resolved if null.</param>
        /// <returns>Whether we could update the pathfinding state correctly.</returns>
        public bool UpdatePathfinding(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction))
                return false;

            if (construction.TargetNode is not { } targetNodeId)
                return false;

            if (GetCurrentGraph(uid, construction) is not { } graph
                || GetNodeFromGraph(graph, construction.Node) is not { } node
                || GetNodeFromGraph(graph, targetNodeId) is not { } targetNode)
                return false;

            return UpdatePathfinding(uid, graph, node, targetNode, GetCurrentEdge(uid, construction), construction);
        }

        /// <summary>
        ///     Internal version of <see cref="UpdatePathfinding(EntityUid, ConstructionComponent?)"/>, which expects
        ///     a valid construction state and actually performs the pathfinding update logic.
        /// </summary>
        /// <param name="uid">The target entity.</param>
        /// <param name="graph">The construction graph the entity is at.</param>
        /// <param name="currentNode">The current construction node the entity is at.</param>
        /// <param name="targetNode">The target node we are trying to reach on the graph.</param>
        /// <param name="currentEdge">The current edge the entity is at, or null if none.</param>
        /// <param name="construction">The construction component of the target entity. Will be resolved if null.</param>
        /// <returns>Whether we could update the pathfinding state correctly.</returns>
        protected bool UpdatePathfinding(EntityUid uid, ConstructionGraphPrototype graph,
            ConstructionGraphNode currentNode, ConstructionGraphNode targetNode,
            ConstructionGraphEdge? currentEdge,
            ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction))
                return false;

            construction.TargetNode = targetNode.Name;

            if (currentNode == targetNode)
            {
                ClearPathfinding(uid, construction);
                return true;
            }

            if (construction.NodePathfinding == null)
            {
                var path = graph.PathId(currentNode.Name, targetNode.Name);

                if (path == null || path.Length == 0)
                {
                    ClearPathfinding(uid, construction);
                    return false;
                }

                construction.NodePathfinding = new Queue<string>(path);
            }

            if (construction.NodePathfinding.Peek() == currentNode.Name)
                construction.NodePathfinding.Dequeue();

            if (currentEdge != null && construction.TargetEdgeIndex is { } targetEdgeIndex)
            {
                if (currentNode.Edges.Count >= targetEdgeIndex)
                {
                    construction.TargetEdgeIndex = null;
                }
                else if (currentNode.Edges[targetEdgeIndex] != currentEdge)
                {
                    ClearPathfinding(uid, construction);
                    return false;
                }
            }

            if (construction.EdgeIndex == null
                && construction.TargetEdgeIndex == null
                && construction.NodePathfinding != null)
                construction.TargetEdgeIndex = currentNode.GetEdgeIndex(construction.NodePathfinding.Peek());

            Dirty(uid, construction);
            return true;
        }

        /// <summary>
        ///     Clears the pathfinding targets on a construction entity.
        /// </summary>
        /// <param name="uid">The target entity.</param>
        /// <param name="construction">The construction component of the target entity. Will be resolved if null.</param>
        public void ClearPathfinding(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction))
                return;

            construction.TargetNode = null;
            construction.TargetEdgeIndex = null;
            construction.NodePathfinding = null;
            Dirty(uid, construction);
        }
    }
}
