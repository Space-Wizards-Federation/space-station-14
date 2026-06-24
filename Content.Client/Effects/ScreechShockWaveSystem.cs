using Content.Client.Overlays;
using Content.Shared.Effects;
using Robust.Client.Graphics;

namespace Content.Client.Effects;

/// <summary>
/// This system exists only to add the overlay
/// </summary>
public sealed partial class ScreechShockWaveSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;

    public override void Initialize()
    {
        _overlayMan.AddOverlay(new ScreechShockWaveOverlay());
    }
}
