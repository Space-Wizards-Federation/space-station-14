using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Construction.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ComputerComponent : Component
{
    [DataField("board")]
    public EntProtoId? BoardPrototype;
}
