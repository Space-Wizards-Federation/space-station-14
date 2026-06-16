using Content.Client.Chemistry.Visualizers;
using Content.Shared.Chemistry.Components;

namespace Content.Client.Storage.Components;

/// <summary>
///     Essentially a version of <see cref="SolutionContainerVisualsComponent"/> fill level handling but for item storage.
///     Depending on the fraction of storage that's filled, will change the sprite at <see cref="FillLayer"/> to the nearest
///     fill level, up to <see cref="MaxFillLevels"/>. Does the same for the Equipped/InHands versions but in their respective
///     code, OnGetHeldVisuals for InHands & OnGetClothingVisuals for Equipped.
/// </summary>
[RegisterComponent]
public sealed partial class StorageContainerVisualsComponent : Component
{
    [DataField]
    public int MaxFillLevels;

    [DataField]
    public int InHandsMaxFillLevels;

    [DataField]
    public int EquippedMaxFillLevels;

    /// <summary>
    ///     A prefix to use for the fill states., i.e. {FillBaseName}{fill level} for the state
    /// </summary>
    [DataField]
    public string? FillBaseName;

    [DataField]
    public string? InHandsFillBaseName;

    [DataField]
    public string? EquippedFillBaseName;

    [DataField("layer")]
    public StorageContainerVisualLayers FillLayer = StorageContainerVisualLayers.Fill;
}

public enum StorageContainerVisualLayers : byte
{
    Fill
}
