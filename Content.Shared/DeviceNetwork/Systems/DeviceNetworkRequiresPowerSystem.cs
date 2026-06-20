using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Power.Components;

namespace Content.Shared.DeviceNetwork.Systems;

public sealed partial class DeviceNetworkRequiresPowerSystem : EntitySystem
{
    [Dependency] private EntityQuery<SharedApcPowerReceiverComponent> _power = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeviceNetworkRequiresPowerComponent, BeforePacketSentEvent>(OnBeforePacketSent);
    }

    private void OnBeforePacketSent(Entity<DeviceNetworkRequiresPowerComponent> ent, ref BeforePacketSentEvent args)
    {
        if (_power.TryComp(ent, out var receiver) && !receiver.Powered)
        {
            args.Cancelled = true;
        }
    }
}
