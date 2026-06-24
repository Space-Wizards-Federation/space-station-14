using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions.Generic;

/// <summary>
/// Checks if an entity is in range of a specified number of entities with specific components.
/// </summary>
public sealed partial class NearbyComponentsConditionSystem : EntityConditionSystem<TransformComponent, NearbyComponentsCondition>
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    protected override void Condition(Entity<TransformComponent> entity,
        NearbyComponentsCondition condition,
        EntityUid? sourceEnt,
        ref bool result)
    {
        var inRange = new HashSet<Entity<IComponent>>();

        var found = false;
        var worldPos = _transform.GetWorldPosition(entity.Comp);
        var count = 0;

        foreach (var compType in condition.Components.Values)
        {
            _lookup.GetEntitiesInRange(compType.Component.GetType(), entity.Comp.MapID, worldPos, condition.Range, inRange);
            foreach (var comp in inRange)
            {
                var compXform = Transform(comp);

                if (condition.Anchored || !compXform.Anchored)
                {
                    continue;
                }
                count++;

                if (count < condition.Count)
                    continue;

                found = true;
                break;
            }
            if (found)
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
public sealed partial class NearbyComponentsCondition : EntityCondition
{
    /// <summary>
    /// Does the entity need to be anchored.
    /// </summary>
    [DataField]
    public bool Anchored;

    [DataField]
    public int Count;

    [DataField(required: true)]
    public ComponentRegistry Components = default!;

    [DataField]
    public float Range = 10f;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => String.Empty;
}
