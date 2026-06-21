using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Popups;

namespace Content.Shared.Actions;

/// <summary>
/// Just the system to realize Pacifism Disabled Component.
/// <seealso cref="PacifismDisabledComponent" />
/// </summary>
public sealed partial class PacifismDisabledSystem : EntitySystem
{
    [Dependency] private PacificationSystem _pacification = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PacifismDisabledComponent, ActionAttemptEvent>(OnAttempt);
    }

    private void OnAttempt(Entity<PacifismDisabledComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;
        //query for pacification
        if (!_pacification.IsPacified(args.User))
            return;
        //if found popup message and cancel
        _popup.PopupClient(Loc.GetString(ent.Comp.PacificationMessage), args.User, args.User);
        args.Cancelled = true;
    }
}
