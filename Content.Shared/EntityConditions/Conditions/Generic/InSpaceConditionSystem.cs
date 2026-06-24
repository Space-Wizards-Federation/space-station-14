using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions.Generic;

/// <summary>
/// Returns true if the entity is in space.
/// </summary>
public sealed class InSpaceConditionSystem : EntityConditionSystem<TransformComponent, InSpaceCondition>
{
    protected override void Condition(Entity<TransformComponent> entity, InSpaceCondition condition, EntityUid? sourceEnt, ref bool result)
    {
        if (entity.Comp.GridUid != null)
        {
            result = false;
            return;
        }

        result = true;
    }
}


/// <inheritdoc cref="EntityCondition"/>
public sealed partial class InSpaceCondition : EntityCondition
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => String.Empty;
}
