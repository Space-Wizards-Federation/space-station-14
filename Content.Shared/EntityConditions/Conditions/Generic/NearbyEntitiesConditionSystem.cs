using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions.Generic;

/// <summary>
/// Checks for entities matching the whitelist in range.
/// </summary>
public sealed partial class NearbyEntitiesConditionSystem : EntityConditionSystem<TransformComponent, NearbyEntitiesCondition>
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    protected override void Condition(Entity<TransformComponent> entity, NearbyEntitiesCondition condition, EntityUid? sourceEnt, ref bool result)
    {
        if (entity.Comp.MapUid == null)
        {
            result = false;
            return;
        }

        var found = false;
        var worldPos = _transform.GetWorldPosition(entity.Comp);
        var count = 0;

        foreach (var ent in _lookup.GetEntitiesInRange(entity.Comp.MapID, worldPos, condition.Range))
        {
            if (_whitelist.IsWhitelistFail(condition.Whitelist, ent))
                continue;

            count++;

            if (count < condition.Count)
                continue;

            found = true;
            break;
        }

        if (!found)
        {
            result = false;
            return;
        }

        result = true;
    }
}


/// <inheritdoc cref="EntityCondition"/>
public sealed partial class NearbyEntitiesCondition : EntityCondition
{
    /// <summary>
    /// How many of the entity need to be nearby.
    /// </summary>
    [DataField]
    public int Count = 1;

    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public float Range = 10f;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => String.Empty;
}
