using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared.Wires;

/// <summary>
///     WireLayout prototype.
///
///     This is meant for ease of organizing wire sets on entities that use
///     wires. Once one of these is initialized, it should be stored in the
///     WiresSystem as a functional wire set.
/// </summary>
[Prototype]
public sealed partial class WireLayoutPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<WireLayoutPrototype>))]
    public string[]? Parents { get; private set; }

    [AbstractDataField]
    public bool Abstract { get; private set; }

    /// <summary>
    ///     How many wires in this layout will do
    ///     nothing (these are added upon layout
    ///     initialization)
    /// </summary>
    [DataField]
    [NeverPushInheritance]
    public int DummyWires { get; private set; } = default!;

    /// <summary>
    ///     All the valid IWireActions currently in this layout.
    /// </summary>
    [DataField(customTypeSerializer: typeof(WireLayoutEntryListSerializer))]
    [NeverPushInheritance]
    public List<WireLayoutEntry>? Wires { get; private set; }
}

public sealed class WireLayoutEntry
{
    public IWireAction? Action { get; }

    public WireLayoutEntry(IWireAction? action)
    {
        Action = action;
    }
}

public sealed class WireLayoutEntryListSerializer :
    ITypeSerializer<List<WireLayoutEntry>, SequenceDataNode>
{
    // This scrunkly is to make the actual action outputs null on client while they're not predicted.

    public List<WireLayoutEntry> Read(
        ISerializationManager serializationManager,
        SequenceDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<List<WireLayoutEntry>>? instanceProvider = null)
    {
        var list = instanceProvider != null ? instanceProvider() : new List<WireLayoutEntry>();

        foreach (var dataNode in node.Sequence)
        {
            if (IsClient(dependencies))
            {
                list.Add(new WireLayoutEntry(null));
                continue;
            }

            list.Add(new WireLayoutEntry(serializationManager.Read<IWireAction>(dataNode, hookCtx, context, notNullableOverride: true)));
        }

        return list;
    }

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        SequenceDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context)
    {
        if (IsClient(dependencies))
            return new ValidatedSequenceNode(node.Sequence.Select(dataNode => new ValidatedValueNode(dataNode)).Cast<ValidationNode>().ToList());

        return new ValidatedSequenceNode(node.Sequence.Select(dataNode => serializationManager.ValidateNode<IWireAction>(dataNode, context)).ToList());
    }

    public DataNode Write(
        ISerializationManager serializationManager,
        List<WireLayoutEntry> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        var sequence = new SequenceDataNode();

        foreach (var entry in value)
        {
            if (entry.Action == null)
                continue;

            sequence.Add(serializationManager.WriteValue(entry.Action, alwaysWrite, context, notNullableOverride: true));
        }

        return sequence;
    }

    private static bool IsClient(IDependencyCollection dependencies)
    {
        return dependencies.Resolve<INetManager>().IsClient;
    }
}
