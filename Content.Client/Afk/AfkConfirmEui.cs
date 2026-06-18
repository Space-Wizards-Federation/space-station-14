using Content.Client.Eui;
using Content.Shared.Afk;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Client.Afk;

[UsedImplicitly]
public sealed partial class AfkConfirmEui : BaseEui
{
    private static readonly SoundSpecifier BwoinkSound = new SoundPathSpecifier("/Audio/Effects/adminhelp.ogg");

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entManager = default!;
    private AudioSystem _audio = default!;

    private readonly AfkConfirmWindow _window = new();

    public AfkConfirmEui()
    {
        _audio = _entManager.System<AudioSystem>();

        _window.OnConfirm += () =>
        {
            SendMessage(new AfkConfirmMessage());
            _window.Close();
        };

        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _clyde.RequestWindowAttention();
        _audio.PlayGlobal(BwoinkSound, Filter.Local(), false);
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not AfkConfirmEuiState afkState)
            return;

        _window.SetTimeRemaining(afkState.TimeRemaining);
    }
}
