using Robust.Shared.GameStates;

namespace Content.Shared.Actions.Components;

/// <summary>
/// If the user attempts an action with this component,
/// it is aborted with a custom message while the user is pacified.
/// </summary>
[RegisterComponent] [NetworkedComponent] [Access(typeof(PacifismDisabledSystem))]
public sealed partial class PacifismDisabledComponent : Component
{
    /// <summary>
    /// The message pop up message when the action is attempted.
    /// </summary>
    [DataField]
    public string PacificationMessage { get; set; } = "dangerous-action-popup";
}
