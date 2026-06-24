using Content.Client.UserInterface;
using Content.Shared.Wires;
using Robust.Client.Timing;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.Wires.UI
{
    public sealed partial class WiresBoundUserInterface : BoundUserInterface
    {
        [Dependency] private IClientGameTiming _gameTiming = default!;

        [ViewVariables]
        private WiresMenu? _menu;

        private readonly WiresSystem _wires;
        private BuiPredictionState? _prediction;

        public WiresBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            IoCManager.InjectDependencies(this);
            _wires = EntMan.System<WiresSystem>();
        }

        protected override void Open()
        {
            base.Open();
            _prediction = new BuiPredictionState(this, _gameTiming);
            _menu = this.CreateWindow<WiresMenu>();
            _menu.OnAction += PerformAction;
            Refresh();
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);
            Refresh();
        }

        public void PerformAction(int id, WiresAction action)
        {
            _prediction?.SendMessage(new WiresActionMessage(id, action));
            Refresh();
        }

        public void Refresh()
        {
            if (_menu == null || !EntMan.TryGetComponent<WiresComponent>(Owner, out var wires))
                return;

            if (_prediction != null)
                _wires.ReplayPredictedWireActions(Owner, wires, _prediction.MessagesToReplay());

            _menu.Populate(wires);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            _menu?.Dispose();
        }
    }
}
