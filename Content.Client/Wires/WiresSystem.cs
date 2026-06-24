using Content.Shared.Wires;
using Content.Client.Wires.UI;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Wires;

public sealed partial class WiresSystem : SharedWiresSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityQuery<HandsComponent> _handsQuery = default!;
    [Dependency] private EntityQuery<ToolComponent> _toolQuery = default!;

    private static readonly ProtoId<ToolQualityPrototype> CuttingQuality = "Cutting";
    private static readonly ProtoId<ToolQualityPrototype> PulsingQuality = "Pulsing";

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

        if (_player.LocalEntity is not { } user)
            return;

        foreach (var message in messages)
        {
            // Ignore pulses because they don't affect UI state (at least without wire actions being predicted)
            if (message is not WiresActionMessage { Action: not WiresAction.Pulse } action)
                continue;

            ClientWire? wire = null;
            foreach (var clientWire in component.ClientWires)
            {
                if (clientWire.Id != action.Id)
                    continue;

                wire = clientWire;
                break;
            }

            if (wire == null)
                continue;

            if (!CanReplayWireAction(user, wire, action.Action))
                continue;

            var cut = action.Action == WiresAction.Cut;
            if (wire.IsCut == cut)
                continue;

            wire.IsCut = cut;
            dirty = true;
        }

        if (!dirty)
            return;

        foreach (var wire in component.WiresList)
        {
            foreach (var clientWire in component.ClientWires)
            {
                if (clientWire.Id != wire.Id)
                    continue;

                wire.IsCut = clientWire.IsCut;
                break;
            }
        }
    }

    private bool CanReplayWireAction(EntityUid user, ClientWire wire, WiresAction action)
    {
        if (!_handsQuery.TryComp(user, out var handsComponent))
            return false;

        if (!_hands.TryGetActiveItem((user, handsComponent), out var heldEntity))
            return false;

        if (!_toolQuery.TryComp(heldEntity, out var tool))
            return false;

        switch (action)
        {
            case WiresAction.Cut:
                return !wire.IsCut && Tool.HasQuality(heldEntity.Value, CuttingQuality, tool);
            case WiresAction.Mend:
                return wire.IsCut && Tool.HasQuality(heldEntity.Value, CuttingQuality, tool);
            case WiresAction.Pulse:
                return !wire.IsCut && Tool.HasQuality(heldEntity.Value, PulsingQuality, tool);
            default:
                return false;
        }
    }
}
