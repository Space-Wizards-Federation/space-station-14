using Content.Server.Construction;
using Content.Shared.Construction.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Wires;

namespace Content.Server.Wires;

public sealed partial class WiresSystem : SharedWiresSystem
{
    [Dependency] private ConstructionSystem _construction = default!;
    [Dependency] private EntityQuery<ConstructionComponent> _constructionQuery = default!;
    [Dependency] private EntityQuery<WiresPanelSecurityComponent> _wiresPanelSecurityQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WiresComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, WiresComponent component, MapInitEvent args)
    {
        if (component.SerialNumber == null)
            GenerateSerialNumber(uid, component);

        if (component.WireSeed == 0)
            component.WireSeed = Random.Next(1, int.MaxValue);

        if (!string.IsNullOrEmpty(component.LayoutId))
            SetOrCreateWireLayout(uid, component);

        OnWiresMapInit(uid, component);
        UpdateUserInterface(uid);
    }

    private void OnWiresMapInit(EntityUid uid, WiresComponent component)
    {
        // Update the construction graph to make sure that it starts on the node specified by WiresPanelSecurityComponent.
        if (_wiresPanelSecurityQuery.TryComp(uid, out var wiresPanelSecurity) &&
            !string.IsNullOrEmpty(wiresPanelSecurity.SecurityLevel) &&
            _constructionQuery.TryComp(uid, out var construction))
        {
            _construction.ChangeNode(uid, null, wiresPanelSecurity.SecurityLevel, true, construction);
        }
    }
}
