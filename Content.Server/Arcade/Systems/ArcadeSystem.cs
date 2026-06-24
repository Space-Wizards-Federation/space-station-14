using Content.Shared.Arcade.Prototypes;
using Content.Shared.Arcade.Systems;
using Content.Shared.EntityTable;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Arcade.Systems;

public sealed partial class ArcadeSystem : SharedArcadeSystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeScoreboards();
        InitializeRewards();

        _prototypeManager.PrototypesReloaded += OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ArcadeScoreboardPrototype>())
            FillMissingScoreboards();
    }
}
