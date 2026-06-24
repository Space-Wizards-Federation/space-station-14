using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions.Generic;

/// <summary>
/// Returns true if entity is on a grid or in range of one.
/// </summary>
public sealed partial class GridInRangeConditionSystem : EntityConditionSystem<TransformComponent, GridInRangeCondition>
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IMapManager _mapManager = default!;

    protected override void Condition(Entity<TransformComponent> entity, GridInRangeCondition condition, EntityUid? sourceEnt, ref bool result)
    {
        if (entity.Comp.GridUid != null)
        {
            result = true;
            return;
        }

        var worldPos = _transform.GetWorldPosition(entity.Comp);
        var gridRange = new Vector2(condition.Range, condition.Range);

        List<Entity<MapGridComponent>> grids = [];

        _mapManager.FindGridsIntersecting(entity.Comp.MapID, new Box2(worldPos - gridRange, worldPos + gridRange), ref grids);

        if (grids.Count > 0)
        {
            result = true;
            return;
        }

        result = false;
    }
}


/// <inheritdoc cref="EntityCondition"/>
public sealed partial class GridInRangeCondition : EntityCondition
{
    [DataField]
    public float Range = 10f;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => String.Empty;
}
