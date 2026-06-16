using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Wires;

namespace Content.Shared.Doors;

public sealed partial class DoorBoltWireAction : ComponentWireAction<DoorBoltComponent>
{
    public override Color Color { get; set; } = Color.Red;
    public override string Name { get; set; } = "wire-name-door-bolt";

    public override StatusLightState? GetLightState(Wire wire, DoorBoltComponent comp)
        => comp.BoltsDown ? StatusLightState.On : StatusLightState.Off;

    public override object StatusKey { get; } = AirlockWireStatus.BoltIndicator;

    public override bool Cut(EntityUid user, Wire wire, DoorBoltComponent airlock)
    {
        EntityManager.System<SharedDoorSystem>().SetBoltWireCut((wire.Owner, airlock), true);
        if (!airlock.BoltsDown && IsPowered(wire.Owner))
            EntityManager.System<SharedDoorSystem>().SetBoltsDown((wire.Owner, airlock), true, user);

        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, DoorBoltComponent door)
    {
        EntityManager.System<SharedDoorSystem>().SetBoltWireCut((wire.Owner, door), false);
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, DoorBoltComponent door)
    {
        if (IsPowered(wire.Owner))
            EntityManager.System<SharedDoorSystem>().SetBoltsDown((wire.Owner, door), !door.BoltsDown);
        else if (!door.BoltsDown)
            EntityManager.System<SharedDoorSystem>().SetBoltsDown((wire.Owner, door), true);
    }
}
