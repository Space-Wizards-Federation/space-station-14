using Content.Shared.Construction.Prototypes;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Construction.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedConstructionSystem))]
public sealed partial class ConstructionComponent : Component
{
    [DataField("graph", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<ConstructionGraphPrototype>))]
    [AutoNetworkedField]
    public string Graph { get; set; } = string.Empty;

    [DataField("node", required: true)]
    [AutoNetworkedField]
    public string Node { get; set; } = default!;

    [DataField("edge")]
    [AutoNetworkedField]
    public int? EdgeIndex { get; set; } = null;

    [DataField("step")]
    [AutoNetworkedField]
    public int StepIndex { get; set; } = 0;

    [DataField("containers")]
    public HashSet<string> Containers { get; set; } = new();

    [DataField("defaultTarget")]
    [AutoNetworkedField]
    public string? TargetNode { get; set; } = null;

    [ViewVariables]
    [AutoNetworkedField]
    public int? TargetEdgeIndex { get; set; } = null;

    [ViewVariables]
    public Queue<string>? NodePathfinding { get; set; } = null;

    [DataField("deconstructionTarget")]
    [AutoNetworkedField]
    public string? DeconstructionNode { get; set; } = "start";

    [ViewVariables]
    // TODO Force flush interaction queue before serializing to YAML.
    // Otherwise you can end up with entities stuck in invalid states (e.g., waiting for DoAfters).
    public readonly Queue<object> InteractionQueue = new();
}
