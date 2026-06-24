using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions.Generic;

/// <summary>
/// Checks if a percentage of the tiles we are nearby match
/// </summary>
public sealed partial class NearbyTilesPercentConditionSystem : EntityConditionSystem<TransformComponent, NearbyTilesPercentCondition>
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;


    protected override void Condition(Entity<TransformComponent> entity, NearbyTilesPercentCondition condition, EntityUid? sourceEnt, ref bool result)
    {
        if (!TryComp<MapGridComponent>(entity.Comp.GridUid, out var grid))
        {
            result = false;
            return;
        }

        var tileCount = 0;
        var matchingTileCount = 0;

        foreach (var tile in _map.GetTilesIntersecting(entity.Comp.GridUid.Value,
                     grid,
                     new Circle(_transform.GetWorldPosition(entity.Comp), condition.Range)))
        {
            // Only consider collidable anchored (for reasons some subfloor stuff has physics but non-collidable)
            if (condition.IgnoreAnchored)
            {
                var gridEnum = _map.GetAnchoredEntitiesEnumerator(entity.Comp.GridUid.Value, grid, tile.GridIndices);
                var found = false;

                while (gridEnum.MoveNext(out var ancUid))
                {
                    if (!_physicsQuery.TryGetComponent(ancUid, out var physics) ||
                        !physics.CanCollide)
                    {
                        continue;
                    }

                    found = true;
                    break;
                }

                if (found)
                    continue;
            }

            tileCount++;

            if (!condition.Tiles.Contains(_tileDef[tile.Tile.TypeId].ID))
                continue;

            matchingTileCount++;
        }

        if (tileCount == 0 || matchingTileCount / (float) tileCount < condition.Percent)
        {
            result = false;
            return;
        }

        result = true;
    }
}


/// <inheritdoc cref="EntityCondition"/>
public sealed partial class NearbyTilesPercentCondition : EntityCondition
{
    [DataField]
    public bool IgnoreAnchored;

    [DataField(required: true)]
    public float Percent;

    [DataField(required: true)]
    public List<ProtoId<ContentTileDefinition>> Tiles = new();

    [DataField]
    public float Range = 10f;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => String.Empty;
}
