using Content.Shared.Actions;
using Content.Shared.Changeling.Components;
using Content.Shared.Effects;
using Content.Shared.Rejuvenate;
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
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ScreechShockWaveSystem _screech = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChangelingIdentityComponent, ChangelingUnlockHorrorEvent>(OnUnlock);
        SubscribeLocalEvent<ChangelingHorrorComponent, AfterChangelingTransformEvent>(OnAfterTransform);
        SubscribeLocalEvent<ChangelingHorrorComponent, BeforeChangelingTransformEvent>(OnBeforeTransform);
    }

    /// <summary>
    /// This function will only be executed when transforming to changeling horror to a "regular" person.
    /// </summary>
    private void OnBeforeTransform(Entity<ChangelingHorrorComponent> ent, ref BeforeChangelingTransformEvent args)
    {
        // this event fires before the transformation (but after the doafter)
        if (HasComp<ChangelingHorrorComponent>(args.StoredIdentity))
            return;

        // enable actions again
        foreach (var action in _actions.GetActions(ent.Owner))
        {
            if (TryComp<ChangelingHorrorDisableComponent>(action.Owner, out var comp))
            {
                if (comp.ToggleOff)
                {
                    _actions.SetToggled((action.Owner, action.Comp), comp.OldToggleStatus);
                }

                _actions.SetEnabled((action.Owner, action.Comp), true);
            }
        }

        // remove horror actions
        if (TryComp<ChangelingHorrorActionStorageComponent>(ent.Owner, out var lingActions))
        {
            foreach (var action in lingActions.CreatedActions)
            {
                _actions.RemoveAction(ent.Owner, action);
            }

            lingActions.CreatedActions.Clear();
        }

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

    /// <summary>
    /// This fonction should only be executed when the changeling transforms into its horror form
    /// </summary>
    protected virtual void OnAfterTransform(Entity<ChangelingHorrorComponent> ent, ref AfterChangelingTransformEvent ev)
    {
        // fires after the transformation
        // transformed into a changeling horror, spawn VFX station-wide, toggle actions, etc
        if (!HasComp<ChangelingHorrorComponent>(ev.StoredIdentity))
            return; // this shouldn't happen...

        // full heal
        RaiseLocalEvent(ent.Owner, new RejuvenateEvent());

        // spawn an evil-ass screech VFX
        _screech.EntityScreech(ent.Owner, ent.Comp.SpawnScreech);

        // play a spawn sound
        _audio.PlayPredicted(ent.Comp.SpawnSound, ent.Owner, null);

        // Turn actions on/off
        foreach (var action in _actions.GetActions(ent.Owner))
        {
            if (TryComp<ChangelingHorrorDisableComponent>(action.Owner, out var comp))
            {
                if (comp.ToggleOff)
                {
                    comp.OldToggleStatus = action.Comp.Toggled;
                    _actions.SetToggled((action.Owner, action.Comp), false);
                }

                _actions.SetEnabled((action.Owner, action.Comp), false);
            }
        }

        // give horror actions
        if (TryComp<ChangelingHorrorActionStorageComponent>(ent.Owner, out var lingActions))
        {
            // this shouldn't be needed, but just in case...
            lingActions.CreatedActions.Clear();

            foreach (var action in lingActions.Actions)
            {
                var k = _actions.AddAction(ent.Owner, action);
                if (k.HasValue)
                {
                    // we keep track of them to delete them later when turning back
                    lingActions.CreatedActions.Add(k.Value);
                }
            }
        }
    }

}

/// <summary>
/// Unlocks an entity's horror form
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ChangelingUnlockHorrorEvent : EntityEventArgs;
