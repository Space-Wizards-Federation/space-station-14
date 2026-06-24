namespace Content.Shared.Stacks;

[ByRefEvent]
public readonly record struct MergeEvent(EntityUid Merger, EntityUid Mergee, int Delta);

/// <summary>
///     Raised on the original stack entity when it is split to create another.
/// </summary>
/// <param name="NewId">The entity id of the new stack.</param>
[ByRefEvent]
public readonly record struct StackSplitEvent(EntityUid NewId);
