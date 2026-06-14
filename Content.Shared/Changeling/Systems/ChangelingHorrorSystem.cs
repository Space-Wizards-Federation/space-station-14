using Content.Shared.Changeling.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Changeling.Systems;

/// <summary>
/// Handles transforming to / from the horror form.
/// </summary>
public sealed partial class ChangelingHorrorSystem : EntitySystem
{
    [Dependency] private SharedChangelingIdentitySystem _identitySystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChangelingIdentityComponent, ChangelingUnlockHorrorEvent>(OnUnlock);
        SubscribeLocalEvent<ChangelingUnlockHorrorEvent>(OnDoThing);
    }

    private void OnDoThing(ChangelingUnlockHorrorEvent ev)
    {
        // do nuffin
    }

    private void OnUnlock(Entity<ChangelingIdentityComponent> ent, ref ChangelingUnlockHorrorEvent ev)
    {
        var idEnt = Spawn("MobHorror"); // todo: make this into a generic system that unlocks identities (can be used for the lesser form etc.)
        var identity = _identitySystem.GrantIdentity(ent, idEnt);
        if (identity.HasValue)
        {
            AddComp(identity.Value, new UncountedIdentityComponent());
            AddComp(identity.Value, new UnremovableIdentityComponent());
        }

        QueueDel(idEnt); // we dont need to keep this entity any longer
    }
}

/// <summary>
/// Unlocks an entity's horror form
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ChangelingUnlockHorrorEvent : EntityEventArgs;
