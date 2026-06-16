using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Effects;

public sealed partial class ScreechShockWaveSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScreechShockWaveComponent, MapInitEvent>(OnInit);
    }

    private void OnInit(Entity<ScreechShockWaveComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.InitTime = _timing.CurTime;
        Dirty(ent);
    }

    /// <summary>
    /// Makes the target entity screech using the given screech
    /// </summary>
    public void EntityScreech(EntityUid target, EntProtoId screechPrototype)
    {
        // TODO: handle stunning etc. here
        var ett = Spawn(screechPrototype);
        var container = _containers.EnsureContainer<ContainerSlot>(target, "screechHolder");
        _containers.Insert(ett, container);
    }
}
