using Content.Shared.Wires;
using Content.Client.Wires.UI;
using Robust.Shared.GameStates;

namespace Content.Client.Wires;

public sealed class WiresSystem : SharedWiresSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WiresComponent, AfterAutoHandleStateEvent>(OnWiresHandleState);
    }

    private void OnWiresHandleState(EntityUid uid, WiresComponent component, ref AfterAutoHandleStateEvent args)
    {
        ApplyClientWireState(uid, component);

        if (UI.TryGetOpenUi<WiresBoundUserInterface>(uid, WiresUiKey.Key, out var bui))
            bui.Refresh();
    }

    public void ReplayPredictedWireActions(EntityUid uid, WiresComponent component, IEnumerable<BoundUserInterfaceMessage> messages)
    {
        var dirty = false;

        foreach (var message in messages)
        {
            if (message is not WiresActionMessage { Action: not WiresAction.Pulse } action)
                continue;

            var wire = TryGetWire(uid, action.Id, component);
            if (wire == null)
                continue;

            var cut = action.Action == WiresAction.Cut;
            if (wire.IsCut == cut)
                continue;

            wire.IsCut = cut;
            dirty = true;
        }

        if (dirty)
            UpdateUserInterface(uid, component);
    }
}
