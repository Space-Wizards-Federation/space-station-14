using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Changeling.Components;

/// <summary>
/// Marks an entity as a changeling horror & stores horror-related datafiels.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ChangelingHorrorComponent : Component
{
    /// <summary>
    /// Station-wide announcement sound that is played when the changeling enters its horror form.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? SpawnAnnouncementSound;

    /// <summary>
    /// Local sound that is played when a changeling enters its horror form.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? SpawnSound;
}
