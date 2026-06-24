using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions.Generic;

/// <summary>
/// Returns true if griduid and mapuid match (AKA on 'planet').
/// </summary>
public sealed class OnMapGridConditionSystem : EntityConditionSystem<TransformComponent, OnMapGridCondition>
{
    protected override void Condition(Entity<TransformComponent> entity, OnMapGridCondition condition, EntityUid? sourceEnt, ref bool result)
    {
        if (entity.Comp.GridUid != entity.Comp.MapUid || entity.Comp.MapUid == null)
        {
            result = false;
            return;
        }

        result = true;
    }
}


/// <inheritdoc cref="EntityCondition"/>
public sealed partial class OnMapGridCondition : EntityCondition
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => String.Empty;
}
