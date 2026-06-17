using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Rejuvenate;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Wires;

public abstract partial class SharedWiresSystem : EntitySystem
{
    [Dependency] private   IPrototypeManager _protoMan = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected ISharedAdminLogManager AdminLogger = default!;
    [Dependency] private   ActivatableUISystem _activatableUI = default!;
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] private   SharedDoAfterSystem _doAfter = default!;
    [Dependency] private   SharedHandsSystem _hands = default!;
    [Dependency] private   SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private   SharedPopupSystem _popupSystem = default!;
    [Dependency] protected SharedToolSystem Tool = default!;
    [Dependency] protected SharedUserInterfaceSystem UI = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private EntityQuery<AppearanceComponent> _appearanceQuery = default!;
    [Dependency] private EntityQuery<HandsComponent> _handsQuery = default!;
    [Dependency] private EntityQuery<ToolComponent> _toolQuery = default!;
    [Dependency] private EntityQuery<WiresComponent> _wiresQuery = default!;
    [Dependency] private EntityQuery<WiresPanelComponent> _wiresPanelQuery = default!;
    [Dependency] private EntityQuery<WiresPanelSecurityComponent> _wiresPanelSecurityQuery = default!;

    private readonly Dictionary<EntityUid, List<ActiveWireAction>> _activeWires = new();
    private readonly List<(EntityUid, ActiveWireAction)> _finishedWires = new();

    private static readonly ProtoId<ToolQualityPrototype> CuttingQuality = "Cutting";
    private static readonly ProtoId<ToolQualityPrototype> PulsingQuality = "Pulsing";

    private float _toolTime = 0f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WiresPanelComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WiresPanelComponent, WirePanelDoAfterEvent>(OnPanelDoAfter);
        SubscribeLocalEvent<WiresPanelComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<WiresPanelComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<WiresPanelComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);

        SubscribeLocalEvent<ActivatableUIRequiresPanelComponent, ActivatableUIOpenAttemptEvent>(OnAttemptOpenActivatableUI);
        SubscribeLocalEvent<ActivatableUIRequiresPanelComponent, PanelChangedEvent>(OnActivatableUIPanelChanged);

        SubscribeLocalEvent<WiresComponent, PanelChangedEvent>(OnWiresPanelChanged);
        SubscribeLocalEvent<WiresComponent, WiresActionMessage>(OnWiresActionMessage);
        SubscribeLocalEvent<WiresComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<WiresComponent, TimedWireEvent>(OnTimedWire);
        SubscribeLocalEvent<WiresComponent, PowerChangedEvent>(OnWiresPowered);
        SubscribeLocalEvent<WiresComponent, WireDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<WiresComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<WiresPanelSecurityComponent, WiresPanelSecurityEvent>(SetWiresPanelSecurity);
    }

    public virtual bool TryCancelWireAction(EntityUid owner, object key)
    {
        if (TryGetData<CancellationTokenSource?>(owner, key, out var token))
        {
            token.Cancel();
            return true;
        }

        return false;
    }

    public void StartWireAction(EntityUid owner, float delay, object key, TimedWireEvent onFinish)
    {
        if (!_wiresQuery.HasComp(owner))
            return;

        if (!_activeWires.ContainsKey(owner))
            _activeWires.Add(owner, new());

        CancellationTokenSource tokenSource = new();

        // Starting an already started action will do nothing.
        if (HasData(owner, key))
            return;

        SetData(owner, key, tokenSource);

        _activeWires[owner].Add(new ActiveWireAction
        (
            key,
            delay,
            tokenSource.Token,
            onFinish
        ));
    }

    public override void Update(float frameTime)
    {
        foreach (var (owner, activeWires) in _activeWires)
        {
            if (!_wiresQuery.HasComp(owner))
                _activeWires.Remove(owner);

            foreach (var wire in activeWires)
            {
                if (wire.CancelToken.IsCancellationRequested)
                {
                    RaiseLocalEvent(owner, wire.OnFinish, true);
                    _finishedWires.Add((owner, wire));
                }
                else
                {
                    wire.TimeLeft -= frameTime;
                    if (wire.TimeLeft <= 0)
                    {
                        RaiseLocalEvent(owner, wire.OnFinish, true);
                        _finishedWires.Add((owner, wire));
                    }
                }
            }
        }

        if (_finishedWires.Count == 0)
            return;

        foreach (var (owner, wireAction) in _finishedWires)
        {
            if (!_activeWires.TryGetValue(owner, out var activeWire))
                continue;

            activeWire.RemoveAll(action => action.CancelToken == wireAction.CancelToken);

            if (activeWire.Count == 0)
                _activeWires.Remove(owner);

            RemoveData(owner, wireAction.Id);
        }

        _finishedWires.Clear();
    }

    private sealed class ActiveWireAction
    {
        /// <summary>
        ///     The wire action's ID. This is so that once the action is finished,
        ///     any related data can be removed from the state dictionary.
        /// </summary>
        public object Id;

        /// <summary>
        ///     How much time is left in this action before it finishes.
        /// </summary>
        public float TimeLeft;

        /// <summary>
        ///     The token used to cancel the action.
        /// </summary>
        public CancellationToken CancelToken;

        /// <summary>
        ///     The event called once the action finishes.
        /// </summary>
        public TimedWireEvent OnFinish;

        public ActiveWireAction(object identifier, float time, CancellationToken cancelToken, TimedWireEvent onFinish)
        {
            Id = identifier;
            TimeLeft = time;
            CancelToken = cancelToken;
            OnFinish = onFinish;
        }
    }

    protected void SetOrCreateWireLayout(EntityUid uid, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return;

        List<Wire>? wireSet = null;

        List<IWireAction?> wireActions = new();
        var dummyWires = 0;

        if (!_protoMan.TryIndex(wires.LayoutId, out var layoutPrototype))
            return;

        dummyWires += layoutPrototype.DummyWires;

        if (layoutPrototype.Wires != null)
            wireActions.AddRange(layoutPrototype.Wires.Select(wire => wire.Action));

        foreach (var parentLayout in _protoMan.EnumerateParents<WireLayoutPrototype>(wires.LayoutId))
        {
            if (parentLayout.Wires != null)
                wireActions.AddRange(parentLayout.Wires.Select(wire => wire.Action));

            dummyWires += parentLayout.DummyWires;
        }

        if (wireActions.Count > 0)
        {
            foreach (var wire in wireActions)
            {
                wire?.Initialize();
            }

            wireSet = CreateWireSet(uid, wireActions, dummyWires, SeededRandom(wires.WireSeed));
        }

        if (wireSet == null || wireSet.Count == 0)
            return;

        wires.WiresList.Clear();

        var types = new Dictionary<object, int>();
        var enumeratedList = new List<(int, Wire)>();
        for (var i = 0; i < wireSet.Count; i++)
        {
            enumeratedList.Add((i, wireSet[i]));
        }

        SeededRandom(wires.WireSeed).Shuffle(enumeratedList);

        for (var i = 0; i < enumeratedList.Count; i++)
        {
            (int id, Wire d) = enumeratedList[i];
            d.Id = i;

            if (d.Action != null)
            {
                var actionType = d.Action.GetType();
                if (!types.TryAdd(actionType, 1))
                    types[actionType] += 1;

                if (!d.Action.AddWire(d, types[actionType]))
                    d.Action = null;
            }

            wires.WiresList.Add(wireSet[id]);
        }

        wires.BuiltWireSeed = wires.WireSeed;
    }

    private List<Wire>? CreateWireSet(EntityUid uid, List<IWireAction?> wires, int dummyWires, IRobustRandom random)
    {
        if (wires.Count == 0)
            return null;

        List<WireColor> colors = new((WireColor[]) Enum.GetValues(typeof(WireColor)));
        List<WireLetter> letters = new((WireLetter[]) Enum.GetValues(typeof(WireLetter)));

        var wireSet = new List<Wire>();
        for (var i = 0; i < wires.Count; i++)
        {
            wireSet.Add(CreateWire(uid, wires[i], i, colors, letters, random));
        }

        for (var i = 1; i <= dummyWires; i++)
        {
            wireSet.Add(CreateWire(uid, null, wires.Count + i, colors, letters, random));
        }

        return wireSet;
    }

    // TODO: Uahggrg engine PR
    private static IRobustRandom SeededRandom(int seed)
    {
        var random = new RobustRandom();
        random.SetSeed(seed);
        return random;
    }

    private Wire CreateWire(EntityUid uid, IWireAction? action, int position, List<WireColor> colors, List<WireLetter> letters, IRobustRandom random)
    {
        var color = colors.Count == 0 ? WireColor.Red : random.PickAndTake(colors);
        var letter = letters.Count == 0 ? default : random.PickAndTake(letters);

        return new Wire(uid, false, color, letter, position, action);
    }

    protected void ApplyClientWireState(EntityUid uid, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return;

        if (wires.WireSeed == 0 || string.IsNullOrEmpty(wires.LayoutId))
            return;

        if (wires.WiresList.Count == 0 || wires.BuiltWireSeed != wires.WireSeed)
            SetOrCreateWireLayout(uid, wires);

        foreach (var clientWire in wires.ClientWires)
        {
            var wire = TryGetWire(uid, clientWire.Id, wires);
            if (wire == null)
                continue;

            wire.IsCut = clientWire.IsCut;
        }
    }

    /// <summary>
    ///     Tries to get the stateful data stored in this entity's WiresComponent.
    /// </summary>
    /// <param name="identifier">The key that stores the data in the WiresComponent.</param>
    public virtual bool TryGetData<T>(EntityUid uid, object identifier, [NotNullWhen(true)] out T data, WiresComponent? wires = null)
    {
        data = default!;
        if (!Resolve(uid, ref wires))
            return false;

        if (!wires.StateData.TryGetValue(identifier, out var result) || result is not T typed)
            return false;

        data = typed;
        return true;
    }

    /// <summary>
    ///     Sets data in the entity's WiresComponent state dictionary by key.
    /// </summary>
    /// <param name="identifier">The key that stores the data in the WiresComponent.</param>
    /// <param name="data">The data to store using the given identifier.</param>
    public virtual void SetData(EntityUid uid, object identifier, object data, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return;

        if (wires.StateData.TryGetValue(identifier, out var storedMessage) && storedMessage == data)
            return;

        wires.StateData[identifier] = data;
        UpdateUserInterface(uid, wires);
    }

    /// <summary>
    ///     If this entity has data stored via this key in the WiresComponent it has
    /// </summary>
    public virtual bool HasData(EntityUid uid, object identifier, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return false;

        return wires.StateData.ContainsKey(identifier);
    }

    /// <summary>
    ///     Removes data from this entity stored in the given key from the entity's WiresComponent.
    /// </summary>
    /// <param name="identifier">The key that stores the data in the WiresComponent.</param>
    public virtual void RemoveData(EntityUid uid, object identifier, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return;

        wires.StateData.Remove(identifier);
        Dirty(uid, wires);
    }

    protected void UpdateUserInterface(EntityUid uid, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return;

        var clientList = new List<ClientWire>();
        var statuses = new List<(int position, object key, object value)>();
        foreach (var entry in wires.WiresList)
        {
            clientList.Add(new ClientWire(entry.Id, entry.IsCut, entry.Color, entry.Letter));

            var statusData = entry.Action?.GetStatusLightData(entry);
            if (statusData != null && entry.Action?.StatusKey != null)
                statuses.Add((entry.OriginalPosition, entry.Action.StatusKey, statusData));
        }

        // TODO: This server check is temporary while client ignores wire actions (see WireLayoutEntryListSerializer).
        // Otherwise it will ignore statuses until server state comes in and flicker for a single tick.
        if (_net.IsServer || statuses.Count > 0)
        {
            wires.Statuses.Clear();
            foreach (var (position, key, value) in statuses)
            {
                wires.Statuses[key] = (position, value);
            }

            statuses.Sort((a, b) => a.position.CompareTo(b.position));
            wires.StatusEntries = statuses.Select(p => new StatusEntry(p.key, p.value)).ToArray();
        }

        wires.ClientWires = clientList.ToArray();
        wires.LocalizedBoardName = Loc.GetString(wires.BoardName);
        Dirty(uid, wires);
    }

    public Wire? TryGetWire(EntityUid uid, int id, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return null;

        foreach (var wire in wires.WiresList)
        {
            if (wire.Id == id)
                return wire;
        }

        return null;
    }

    public IEnumerable<Wire> TryGetWires<T>(EntityUid uid, WiresComponent? wires = null) where T : IWireAction
    {
        if (!Resolve(uid, ref wires))
            return Enumerable.Empty<Wire>();

        return wires.WiresList.Where(wire => wire.Action is T);
    }

    public void SetWiresPanelSecurity(EntityUid uid, WiresPanelSecurityComponent component, WiresPanelSecurityEvent args)
    {
        component.WiresAccessible = args.WiresAccessible;
        component.Examine = args.Examine;

        if (!args.WiresAccessible)
            UI.CloseUi(uid, WiresUiKey.Key);

        Dirty(uid, component);
    }

    private void OnWiresPanelChanged(EntityUid uid, WiresComponent component, PanelChangedEvent args)
    {
        if (args.Open)
            return;

        UI.CloseUi(uid, WiresUiKey.Key);
    }

    private void OnTimedWire(EntityUid uid, WiresComponent component, TimedWireEvent args)
    {
        args.Delegate(args.Wire);
        UpdateUserInterface(uid);
    }

    private void OnWiresPowered(EntityUid uid, WiresComponent component, ref PowerChangedEvent args)
    {
        UpdateUserInterface(uid);
        foreach (var wire in component.WiresList)
        {
            wire.Action?.Update(wire);
        }
    }

    private void OnWiresActionMessage(EntityUid uid, WiresComponent component, WiresActionMessage args)
    {
        TryDoWireAction(uid, args.Actor, args.Id, args.Action, component);
    }

    private void OnDoAfter(EntityUid uid, WiresComponent component, WireDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            component.WiresQueue.Remove(args.Id);
            return;
        }

        if (args.Handled || args.Args.Target == null || args.Args.Used == null)
            return;

        UpdateWires(args.Args.Target.Value, args.Args.User, args.Args.Used.Value, args.Id, args.Action, component);

        args.Handled = true;
    }

    private void OnInteractUsing(EntityUid uid, WiresComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_toolQuery.TryComp(args.Used, out var tool))
            return;

        if (!IsPanelOpen(uid))
            return;

        if (Tool.HasQuality(args.Used, CuttingQuality, tool) ||
            Tool.HasQuality(args.Used, PulsingQuality, tool))
        {
            UI.OpenUi(uid, WiresUiKey.Key);
            args.Handled = true;
        }
    }

    private void OnRejuvenate(Entity<WiresComponent> ent, ref RejuvenateEvent args)
    {
        foreach (var wire in ent.Comp.WiresList)
        {
            // Rejuvenate has no user so we mend as the entity having the wire.
            if (wire.Action == null || wire.Action.Mend(ent, wire))
                wire.IsCut = false;
        }

        // If we don't update the interface wires will be desynced on client.
        UpdateUserInterface(ent.Owner, ent.Comp);
    }

    protected void GenerateSerialNumber(EntityUid uid, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires))
            return;

        var data = new char[9];
        data[4] = '-';

        if (Random.Prob(0.01f))
        {
            for (var i = 0; i < 4; i++)
            {
                // Cyrillic Letters
                data[i] = (char) Random.Next(0x0410, 0x0430);
            }
        }
        else
        {
            for (var i = 0; i < 4; i++)
            {
                // Letters
                data[i] = (char) Random.Next(0x41, 0x5B);
            }
        }

        for (var i = 5; i < 9; i++)
        {
            // Digits
            data[i] = (char) Random.Next(0x30, 0x3A);
        }

        wires.SerialNumber = new string(data);
        UpdateUserInterface(uid, wires);
    }

    public void TryDoWireAction(EntityUid target, EntityUid user, int id, WiresAction action, WiresComponent? wires = null)
    {
        if (!_handsQuery.TryComp(user, out var handsComponent))
        {
            _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-no-hands"), user);
            return;
        }

        if (!_interactionSystem.InRangeUnobstructed(user, target))
        {
            _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-reach"), user);
            return;
        }

        if (!_hands.TryGetActiveItem((user, handsComponent), out var heldEntity))
            return;

        if (!_toolQuery.TryComp(heldEntity, out var tool))
            return;

        TryDoWireAction(target, user, heldEntity.Value, id, action, wires, tool);
    }

    private void TryDoWireAction(EntityUid target, EntityUid user, EntityUid toolEntity, int id, WiresAction action, WiresComponent? wires = null, ToolComponent? tool = null)
    {
        if (!Resolve(target, ref wires)
            || !Resolve(toolEntity, ref tool))
            return;

        if (wires.WiresQueue.Contains(id))
            return;

        var wire = TryGetWire(target, id, wires);

        if (wire == null)
            return;

        switch (action)
        {
            case WiresAction.Cut:
                if (!Tool.HasQuality(toolEntity, CuttingQuality, tool))
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-need-wirecutters"), user);
                    return;
                }

                if (wire.IsCut)
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-cut-cut-wire"), user);
                    return;
                }

                break;
            case WiresAction.Mend:
                if (!Tool.HasQuality(toolEntity, CuttingQuality, tool))
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-need-wirecutters"), user);
                    return;
                }

                if (!wire.IsCut)
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-mend-uncut-wire"), user);
                    return;
                }

                break;
            case WiresAction.Pulse:
                if (!Tool.HasQuality(toolEntity, PulsingQuality, tool))
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-need-multitool"), user);
                    return;
                }

                if (wire.IsCut)
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-pulse-cut-wire"), user);
                    return;
                }

                break;
        }

        wires.WiresQueue.Add(id);

        if (_toolTime > 0f)
        {
            var args = new DoAfterArgs(EntityManager, user, _toolTime, new WireDoAfterEvent(action, id), target, target: target, used: toolEntity)
            {
                NeedHand = true,
                BreakOnDamage = true,
                BreakOnMove = true
            };

            _doAfter.TryStartDoAfter(args);
        }
        else
        {
            UpdateWires(target, user, toolEntity, id, action, wires);
        }
    }

    private void UpdateWires(EntityUid used, EntityUid user, EntityUid toolEntity, int id, WiresAction action, WiresComponent? wires = null, ToolComponent? tool = null)
    {
        if (!Resolve(used, ref wires))
            return;

        if (!wires.WiresQueue.Contains(id))
            return;

        if (!Resolve(toolEntity, ref tool))
        {
            wires.WiresQueue.Remove(id);
            return;
        }

        var wire = TryGetWire(used, id, wires);

        if (wire == null)
        {
            wires.WiresQueue.Remove(id);
            return;
        }

        switch (action)
        {
            case WiresAction.Cut:
                if (!Tool.HasQuality(toolEntity, CuttingQuality, tool))
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-need-wirecutters"), user);
                    break;
                }

                if (wire.IsCut)
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-cut-cut-wire"), user);
                    break;
                }

                Tool.PlayToolSound(toolEntity, tool, user);
                if (wire.Action == null || wire.Action.Cut(user, wire))
                    wire.IsCut = true;

                UpdateUserInterface(used);
                break;
            case WiresAction.Mend:
                if (!Tool.HasQuality(toolEntity, CuttingQuality, tool))
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-need-wirecutters"), user);
                    break;
                }

                if (!wire.IsCut)
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-mend-uncut-wire"), user);
                    break;
                }

                Tool.PlayToolSound(toolEntity, tool, user);
                if (wire.Action == null || wire.Action.Mend(user, wire))
                    wire.IsCut = false;

                UpdateUserInterface(used);
                break;
            case WiresAction.Pulse:
                if (!Tool.HasQuality(toolEntity, PulsingQuality, tool))
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-need-multitool"), user);
                    break;
                }

                if (wire.IsCut)
                {
                    _popupSystem.PopupPredictedCursor(Loc.GetString("wires-component-ui-on-receive-message-cannot-pulse-cut-wire"), user);
                    break;
                }

                wire.Action?.Pulse(user, wire);

                UpdateUserInterface(used);
                Audio.PlayPredicted(wires.PulseSound, used, user);
                break;
        }

        wire.Action?.Update(wire);
        wires.WiresQueue.Remove(id);
    }

    private void OnStartup(Entity<WiresPanelComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent, ent);
    }

    private void OnPanelDoAfter(EntityUid uid, WiresPanelComponent panel, WirePanelDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TogglePanel(uid, panel, !panel.Open, args.User))
            return;

        AdminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):user} screwed {ToPrettyString(uid):target}'s maintenance panel {(panel.Open ? "open" : "closed")}");

        var sound = panel.Open ? panel.ScrewdriverOpenSound : panel.ScrewdriverCloseSound;
        Audio.PlayPredicted(sound, uid, args.User);
        args.Handled = true;
    }

    private void OnInteractUsing(Entity<WiresPanelComponent> ent, ref InteractUsingEvent args)
    {
        if (!Tool.HasQuality(args.Used, ent.Comp.OpeningTool))
            return;

        if (!CanTogglePanel(ent, args.User))
            return;

        if (!Tool.UseTool(
                args.Used,
                args.User,
                ent,
                (float) ent.Comp.OpenDelay.TotalSeconds,
                ent.Comp.OpeningTool,
                new WirePanelDoAfterEvent()))
        {
            return;
        }

        AdminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.User):user} is screwing {ToPrettyString(ent):target}'s {(ent.Comp.Open ? "open" : "closed")} maintenance panel at {Transform(ent).Coordinates:targetlocation}");
        args.Handled = true;
    }

    private void OnExamine(EntityUid uid, WiresPanelComponent component, ExaminedEvent args)
    {
        using (args.PushGroup(nameof(WiresPanelComponent)))
        {
            if (!component.Open)
            {
                if (!string.IsNullOrEmpty(component.ExamineTextClosed))
                    args.PushMarkup(Loc.GetString(component.ExamineTextClosed));
            }
            else
            {
                if (!string.IsNullOrEmpty(component.ExamineTextOpen))
                    args.PushMarkup(Loc.GetString(component.ExamineTextOpen));

                if (_wiresPanelSecurityQuery.TryComp(uid, out var wiresPanelSecurity) &&
                    wiresPanelSecurity.Examine != null)
                {
                    args.PushMarkup(Loc.GetString(wiresPanelSecurity.Examine));
                }
            }
        }
    }

    private void OnGetVerbs(Entity<WiresPanelComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!IsPanelOpen(ent.Owner))
            return;

        var actor = args.User;
        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("wires-panel-verb-view-panel"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/screwdriver.png")),
            Act = () => OpenUserInterface(ent, actor),
        };

        args.Verbs.Add(verb);
    }

    public void OpenUserInterface(EntityUid uid, EntityUid actor)
    {
        UI.TryOpenUi(uid, WiresUiKey.Key, actor);
    }

    public void OpenUserInterface(EntityUid uid, ICommonSession player)
    {
        UI.OpenUi(uid, WiresUiKey.Key, player);
    }

    public void ChangePanelVisibility(EntityUid uid, WiresPanelComponent component, bool visible)
    {
        component.Visible = visible;
        UpdateAppearance(uid, component);
        Dirty(uid, component);
    }

    protected void UpdateAppearance(EntityUid uid, WiresPanelComponent panel)
    {
        if (_appearanceQuery.TryComp(uid, out var appearance))
            Appearance.SetData(uid, WiresVisuals.MaintenancePanelState, panel.Open && panel.Visible, appearance);
    }

    public bool TogglePanel(EntityUid uid, WiresPanelComponent component, bool open, EntityUid? user = null)
    {
        if (!CanTogglePanel((uid, component), user))
            return false;

        component.Open = open;
        UpdateAppearance(uid, component);
        Dirty(uid, component);

        var ev = new PanelChangedEvent(component.Open);
        RaiseLocalEvent(uid, ref ev);
        return true;
    }

    public bool CanTogglePanel(Entity<WiresPanelComponent> ent, EntityUid? user)
    {
        var attempt = new AttemptChangePanelEvent(ent.Comp.Open, user);
        RaiseLocalEvent(ent, ref attempt);
        return !attempt.Cancelled;
    }

    public bool IsPanelOpen(Entity<WiresPanelComponent?> entity, EntityUid? tool = null)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return true;

        if (tool != null)
        {
            var ev = new PanelOverrideEvent();
            RaiseLocalEvent(tool.Value, ref ev);

            if (ev.Allowed)
                return true;
        }

        // Listen, i don't know what the fuck this component does. it's stapled on shit for airlocks
        // but it looks like an almost direct duplication of WiresPanelComponent except with a shittier API.
        if (_wiresPanelSecurityQuery.TryComp(entity, out var wiresPanelSecurity) &&
            !wiresPanelSecurity.WiresAccessible)
            return false;

        return entity.Comp.Open;
    }

    private void OnAttemptOpenActivatableUI(EntityUid uid, ActivatableUIRequiresPanelComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || !_wiresPanelQuery.TryComp(uid, out var wires))
            return;

        if (component.RequireOpen != wires.Open)
            args.Cancel();
    }

    private void OnActivatableUIPanelChanged(EntityUid uid, ActivatableUIRequiresPanelComponent component, ref PanelChangedEvent args)
    {
        if (args.Open == component.RequireOpen)
            return;

        _activatableUI.CloseAll(uid);
    }
}

/// <summary>
/// Raised directed on a tool to try and override panel visibility.
/// </summary>
[ByRefEvent]
public record struct PanelOverrideEvent()
{
    public bool Allowed = true;
}
