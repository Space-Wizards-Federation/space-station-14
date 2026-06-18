using Content.Server.Afk.Events;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Shared.Afk;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Afk;

/// <summary>
/// Once a player is AFK handles confirmation and subsequent disconnection.
/// </summary>
public sealed partial class AfkConfirmSystem : EntitySystem
{
    [Dependency] private IAfkManager _afkManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;

    private readonly Dictionary<ICommonSession, AfkConfirmation> _confirmations = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AFKEvent>(OnAfk);
        SubscribeLocalEvent<UnAFKEvent>(OnUnAfk);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var confirmation in _confirmations.Values)
            confirmation.Eui.Close();

        _confirmations.Clear();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnAfk(ref AFKEvent ev)
    {
        var timeout = _cfg.GetCVar(CCVars.AfkConfirmTimeout);
        if (timeout <= 0 || ev.Session.Status is not (SessionStatus.Connected or SessionStatus.InGame) || _confirmations.ContainsKey(ev.Session))
            return;

        var deadline = _timing.RealTime + TimeSpan.FromSeconds(timeout);
        var eui = new AfkConfirmEui(this, deadline);
        _confirmations[ev.Session] = new AfkConfirmation(eui, deadline);
        _eui.OpenEui(eui, ev.Session);
        _adminLogger.Add(LogType.Connection, LogImpact.Low,
            $"{ev.Session.Name} ({ev.Session.UserId}) was shown the AFK confirmation window with {timeout} seconds to respond.");

        var message = Loc.GetString("afk-system-afk-warning", ("seconds", MathF.Ceiling(timeout)));
        _chat.ChatMessageToOne(ChatChannel.Server, message, message, EntityUid.Invalid, false, ev.Session.Channel);
    }

    private void OnUnAfk(ref UnAFKEvent ev)
    {
        ClearConfirmation(ev.Session);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _confirmations.Remove(args.Session);
    }

    public void Confirm(ICommonSession session)
    {
        if (!_confirmations.ContainsKey(session))
            return;

        _afkManager.PlayerDidAction(session);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_confirmations.Count == 0)
            return;

        foreach (var (session, confirmation) in new Dictionary<ICommonSession, AfkConfirmation>(_confirmations))
        {
            if (session.Status == SessionStatus.Disconnected)
            {
                _confirmations.Remove(session);
                continue;
            }

            if (_timing.RealTime < confirmation.Deadline)
                continue;

            _confirmations.Remove(session);
            confirmation.Eui.Close();
            _adminLogger.Add(LogType.Connection, LogImpact.Medium,
                $"{session.Name} ({session.UserId}) timed out on the AFK confirmation window and was disconnected.");
            session.Channel.Disconnect(Loc.GetString("afk-system-kick-reason"));
        }
    }

    private void ClearConfirmation(ICommonSession session)
    {
        if (!_confirmations.Remove(session, out var confirmation))
            return;

        confirmation.Eui.Close();
    }

    private sealed record AfkConfirmation(AfkConfirmEui Eui, TimeSpan Deadline);
}
