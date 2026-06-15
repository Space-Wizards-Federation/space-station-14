using Content.Shared.Changeling.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared.Changeling.Systems;

/// <summary>
/// Handles transforming to / from the horror form.
/// </summary>
public abstract partial class SharedChangelingHorrorSystem : EntitySystem
{
    [Dependency] private SharedChangelingIdentitySystem _identitySystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChangelingHorrorActionSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChangelingIdentityComponent, ChangelingUnlockHorrorEvent>(OnUnlock);
        SubscribeLocalEvent<ChangelingHorrorComponent, AfterChangelingTransformEvent>(OnAfterTransform);
        SubscribeLocalEvent<ChangelingHorrorComponent, BeforeChangelingTransformEvent>(OnBeforeTransform);
    }

    private void OnBeforeTransform(Entity<ChangelingHorrorComponent> ent, ref BeforeChangelingTransformEvent args)
    {
        // this event fires before the transformation (but after the doafter)
        if (HasComp<ChangelingHorrorComponent>(args.StoredIdentity))
            return;

        // Turn actions on/off
        _actions.TransformFromHorror(ent.Owner);
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

    protected virtual void OnAfterTransform(Entity<ChangelingHorrorComponent> ent, ref AfterChangelingTransformEvent ev)
    {
        // fires after the transformation
        // transformed into a changeling horror, spawn VFX station-wide, toggle actions, etc
        if (!HasComp<ChangelingHorrorComponent>(ev.StoredIdentity))
            return; // this shouldn't happen...

        // spawn an evil-ass screech VFX
        Spawn("EffectScreech", Transform(ent.Owner).Coordinates);

        // play a spawn sound
        _audio.PlayPredicted(ent.Comp.SpawnSound, ent.Owner, null);

        // Turn actions on/off
        _actions.TransformToHorror(ent.Owner);
    }

}

/// <summary>
/// Unlocks an entity's horror form
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ChangelingUnlockHorrorEvent : EntityEventArgs;
