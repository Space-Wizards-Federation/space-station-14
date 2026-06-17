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

        public ConstructionGraphPrototype? GetCurrentGraph(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction, false))
                return null;

            return PrototypeManager.TryIndex(construction.Graph, out ConstructionGraphPrototype? graph) ? graph : null;
        }

        public ConstructionGraphNode? GetCurrentNode(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction, false))
                return null;

            if (construction.Node is not { } nodeIdentifier)
                return null;

            return GetCurrentGraph(uid, construction) is not { } graph ? null : GetNodeFromGraph(graph, nodeIdentifier);
        }

        public ConstructionGraphEdge? GetCurrentEdge(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction, false))
                return null;

            if (construction.EdgeIndex is not { } edgeIndex)
                return null;

            return GetCurrentNode(uid, construction) is not { } node ? null : GetEdgeFromNode(node, edgeIndex);
        }

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

        public ConstructionGraphNode? GetNodeFromGraph(ConstructionGraphPrototype graph, string id)
        {
            return graph.Nodes.TryGetValue(id, out var node) ? node : null;
        }

        public ConstructionGraphEdge? GetEdgeFromNode(ConstructionGraphNode node, int index)
        {
            return node.Edges.Count > index ? node.Edges[index] : null;
        }

        public ConstructionGraphStep? GetStepFromEdge(ConstructionGraphEdge edge, int index)
        {
            return edge.Steps.Count > index ? edge.Steps[index] : null;
        }

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
