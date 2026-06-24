using Content.Shared.Construction.Components;

namespace Content.Shared.Construction;

public abstract partial class SharedMachineFrameSystem : EntitySystem
{
    public bool IsComplete(MachineFrameComponent component)
    {
        if (!component.HasBoard)
            return false;

        foreach (var (type, amount) in component.MaterialRequirements)
        {
            if (component.MaterialProgress[type] < amount)
                return false;
        }

        foreach (var (compName, info) in component.ComponentRequirements)
        {
            if (component.ComponentProgress[compName] < info.Amount)
                return false;
        }

        foreach (var (tagName, info) in component.TagRequirements)
        {
            if (component.TagProgress[tagName] < info.Amount)
                return false;
        }

        return true;
    }
}
