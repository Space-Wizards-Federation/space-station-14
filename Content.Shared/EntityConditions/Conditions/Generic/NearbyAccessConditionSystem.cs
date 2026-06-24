using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions.Generic;

/// <summary>
/// Checks for an entity nearby with the specified access.
/// </summary>
public sealed partial class NearbyAccessConditionSystem : EntityConditionSystem<TransformComponent, NearbyAccessCondition>
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private AccessReaderSystem _reader = default!;

    protected override void Condition(Entity<TransformComponent> entity, NearbyAccessCondition condition, EntityUid? sourceEnt, ref bool result)
    {
        if (entity.Comp.MapUid == null)
        {
            result = false;
            return;
        }

        var found = false;
        var count = 0;

        foreach (var (ent, comp) in _lookup.GetEntitiesInRange<AccessReaderComponent>(entity.Comp.Coordinates, condition.Range))
        {
            if (!_reader.AreAccessTagsAllowed(condition.Access, comp) ||
                condition.Anchored && Transform(ent).Anchored)
            {
                continue;
            }

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
public sealed partial class NearbyAccessCondition : EntityCondition
{
    // This exists because of door electronics contained inside doors.
    /// <summary>
    /// Does the access entity need to be anchored.
    /// </summary>
    [DataField]
    public bool Anchored = true;

    /// <summary>
    /// Count of entities that need to be nearby.
    /// </summary>
    [DataField]
    public int Count = 1;

    [DataField(required: true)]
    public List<ProtoId<AccessLevelPrototype>> Access = new();

    [DataField]
    public float Range = 10f;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => String.Empty;
}
