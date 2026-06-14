using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Changeling.Components;

/// <summary>
/// Like an ActionGrant but puts the action in either the changeling horror action storage (if not in horror form) or in the current action pool (if in horror form)
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ChangelingHorrorActionComponent : Component
{
    /// <summary>
    /// The actions that will be added on MapInit by this component, for the horror form.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> HorrorActions = new();

    /// <summary>
    /// The actions that will be added on MapInit by this component, for the regular form.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> NormalActions = new();
}
