using Robust.Shared.GameStates;

namespace Content.Shared.Changeling.Components;

/// <summary>
/// Component used to store regular changeling actions when in horror form, and horror actions when in regular changeling form.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ChangelingHorrorActionStorageComponent : Component
{
    /// <summary>
    /// The action entities stored within this component
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntityUid> ActionEntities = new();

    /// <summary>
    /// If set to true, the stored actions are the horror form's
    /// </summary>
    [DataField]
    public bool StoredIsHorror = false;
}
