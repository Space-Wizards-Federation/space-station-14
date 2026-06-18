using Content.Server.EUI;
using Content.Shared.Afk;
using Content.Shared.Eui;
using Robust.Shared.Timing;

namespace Content.Server.Afk;

public sealed partial class AfkConfirmEui(AfkConfirmSystem system, TimeSpan deadline) : BaseEui
{
    [Dependency] private IGameTiming _timing = null!;

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new AfkConfirmEuiState(deadline - _timing.RealTime);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is not (AfkConfirmMessage or CloseEuiMessage))
        {
            base.HandleMessage(msg);
            return;
        }

        system.Confirm(Player);
        Close();
    }
}
