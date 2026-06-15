using Robust.Shared.Timing;

namespace Content.Shared.Effects;

public sealed partial class ScreechShockWaveSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
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
}
