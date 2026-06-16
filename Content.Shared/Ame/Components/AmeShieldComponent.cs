namespace Content.Shared.Ame.Components;

/// <summary>
/// The component used to make an entity part of the bulk machinery of an AntiMatter Engine.
/// Connects to adjacent entities with this component or the AME controller to make an AME.
/// </summary>
[RegisterComponent]
public sealed partial class AmeShieldComponent : SharedAmeShieldComponent
{
    /// <summary>
    /// Whether or not this AME shield counts as a core for the AME or not.
    /// </summary>
    [ViewVariables]
    public bool IsCore = false;

    /// <summary>
    /// The current integrity of the AME shield.
    /// </summary>
    [DataField("integrity")]
    [ViewVariables]
    public int CoreIntegrity = 100;
}
