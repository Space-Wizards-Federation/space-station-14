namespace Content.Shared.Wires;

public sealed class Wire
{
    /// <summary>
    /// The entity that registered the wire.
    /// </summary>
    public EntityUid Owner { get; }

    /// <summary>
    /// Whether the wire is cut.
    /// </summary>
    public bool IsCut { get; set; }

    /// <summary>
    /// Used in client-server communication to identify a wire without telling the client what the wire does.
    /// </summary>
    [ViewVariables]
    public int Id { get; set; }

    /// <summary>
    /// The original position of this wire in the prototype.
    /// </summary>
    [ViewVariables]
    public int OriginalPosition { get; set; }

    /// <summary>
    /// The color of the wire.
    /// </summary>
    [ViewVariables]
    public WireColor Color { get; }

    /// <summary>
    /// The greek letter shown below the wire.
    /// </summary>
    [ViewVariables]
    public WireLetter Letter { get; }

    /// <summary>
    ///     The action that this wire performs when mended, cut or pulsed.
    /// </summary>
    public IWireAction? Action { get; set; }

    public Wire(EntityUid owner, bool isCut, WireColor color, WireLetter letter, int position, IWireAction? action)
    {
        Owner = owner;
        IsCut = isCut;
        Color = color;
        OriginalPosition = position;
        Letter = letter;
        Action = action;
    }
}

// This is here so that when a DoAfter event is called, WiresSystem can call the action in question
// after the doafter is finished.
public delegate void WireActionDelegate(Wire wire);

public sealed class TimedWireEvent : EntityEventArgs
{
    /// <summary>
    ///     The function to be called once the timed event is complete.
    /// </summary>
    public WireActionDelegate Delegate { get; }

    /// <summary>
    ///     The wire tied to this timed wire event.
    /// </summary>
    public Wire Wire { get; }

    public TimedWireEvent(WireActionDelegate @delegate, Wire wire)
    {
        Delegate = @delegate;
        Wire = wire;
    }
}

public sealed class WireLayout
{
    // why is this an <int, WireData>?
    // List<T>.Insert panics, and this gives wires a stable identifier.
    [ViewVariables]
    public IReadOnlyDictionary<int, WireData> Specifications { get; }

    public WireLayout(IReadOnlyDictionary<int, WireData> specifications)
    {
        Specifications = specifications;
    }

    public sealed class WireData
    {
        public WireLetter Letter { get; }
        public WireColor Color { get; }
        public int Position { get; }

        public WireData(WireLetter letter, WireColor color, int position)
        {
            Letter = letter;
            Color = color;
            Position = position;
        }
    }
}
