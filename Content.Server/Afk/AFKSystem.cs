using Content.Server.Afk.Events;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Afk;

/// <summary>
/// Actively checks for AFK players regularly and issues an event whenever they go afk.
/// </summary>
public sealed partial class AFKSystem : EntitySystem
{
    [Dependency] private IAfkManager _afkManager = default!;
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _ticker = default!;

    private float _checkDelay;
    private TimeSpan _checkTime;

    private readonly HashSet<ICommonSession> _afkPlayers = new();

    public override void Initialize()
    {
        base.Initialize();
        _playerManager.PlayerStatusChanged += OnPlayerChange;
        Subs.CVar(_configManager, CCVars.AfkTime, SetAfkDelay, true);
        _afkManager.PlayerDidActionEvent += OnPlayerAction;

        SubscribeNetworkEvent<FullInputCmdMessage>(HandleInputCmd);
        SubscribeLocalEvent<BoundUserInterfaceMessageReceivedEvent>(OnBoundUiMessageReceived);
    }

    private void HandleInputCmd(FullInputCmdMessage msg, EntitySessionEventArgs args)
    {
        if (_checkDelay <= 0)
            return;

        if (!_playerManager.KeyMap.TryGetKeyFunction(msg.InputFunctionId, out _))
            return;

        if (!Enum.IsDefined(msg.State))
            return;

        _afkManager.PlayerDidAction(args.SenderSession);
    }

    private void OnBoundUiMessageReceived(ref BoundUserInterfaceMessageReceivedEvent args)
    {
        if (_checkDelay <= 0)
            return;

        if (!TryComp<ActorComponent>(args.Actor, out var actor))
            return;

        _afkManager.PlayerDidAction(actor.PlayerSession);
    }

    private void SetAfkDelay(float obj)
    {
        _checkDelay = obj;
        _checkTime = _timing.CurTime;
    }

    private void OnPlayerChange(object? sender, SessionStatusEventArgs e)
    {
        switch (e.NewStatus)
        {
            case SessionStatus.Disconnected:
                _afkPlayers.Remove(e.Session);
                break;
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _afkPlayers.Clear();
        _playerManager.PlayerStatusChanged -= OnPlayerChange;
        _afkManager.PlayerDidActionEvent -= OnPlayerAction;
    }

    private void OnPlayerAction(ICommonSession session)
    {
        if (_checkDelay <= 0)
            return;

        if (!_afkPlayers.Remove(session))
            return;

        var ev = new UnAFKEvent(session);
        RaiseLocalEvent(ref ev);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_ticker.RunLevel is not (GameRunLevel.InRound or GameRunLevel.PreRoundLobby) || _checkDelay <= 0f)
        {
            _afkPlayers.Clear();
            _checkTime = TimeSpan.Zero;
            return;
        }

        if (_timing.CurTime < _checkTime)
            return;

        _checkTime = _timing.CurTime + TimeSpan.FromSeconds(_checkDelay);

        foreach (var pSession in _playerManager.Sessions)
        {
            if (!CanCheckSession(pSession))
                continue;

            var isAfk = _afkManager.IsAfk(pSession);

            if (isAfk && _afkPlayers.Add(pSession))
            {
                var ev = new AFKEvent(pSession);
                RaiseLocalEvent(ref ev);
                continue;
            }

            if (!isAfk && _afkPlayers.Remove(pSession))
            {
                var ev = new UnAFKEvent(pSession);
                RaiseLocalEvent(ref ev);
            }
        }
    }

    private bool CanCheckSession(ICommonSession session)
    {
        return _ticker.RunLevel switch
        {
            GameRunLevel.InRound => session.Status is SessionStatus.Connected or SessionStatus.InGame,
            GameRunLevel.PreRoundLobby => session.Status is SessionStatus.Connected or SessionStatus.InGame,
            _ => false,
        };
    }
}
