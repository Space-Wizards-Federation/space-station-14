using Content.Shared.Arcade.Components;
using Content.Shared.Arcade.Systems;
using JetBrains.Annotations;

namespace Content.Server.Arcade.Systems;

public sealed partial class ArcadeSystem
{
    private void InitializeRewards()
    {
        SubscribeLocalEvent<ArcadeRewardComponent, ComponentInit>(OnArcadeRewardComponentInit);
        SubscribeLocalEvent<ArcadeRewardComponent, ArcadeGameEndedEvent>(OnArcadeRewardGameEnded);
    }

    private void OnArcadeRewardComponentInit(Entity<ArcadeRewardComponent> machine, ref ComponentInit args)
    {
        var comp = machine.Comp;
        var min = Math.Min(comp.RewardMinAmount, comp.RewardMaxAmount);
        var max = Math.Max(comp.RewardMinAmount, comp.RewardMaxAmount);

        comp.RewardAmount = _random.Next(min, max + 1);
    }

    private void OnArcadeRewardGameEnded(Entity<ArcadeRewardComponent> machine, ref ArcadeGameEndedEvent args)
    {
        if (args.Result == ArcadeGameResult.Win)
            SpawnReward(machine);
    }

    /// <summary>
    ///     Produce a reward from an arcade machine, if the machine is capable of doing so.
    /// </summary>
    /// <param name="machine">The machine to spawn rewards from.</param>
    [PublicAPI]
    public void SpawnReward(Entity<ArcadeRewardComponent> machine)
    {
        if (machine.Comp.RewardAmount <= 0)
            return;

        var coords = Transform(machine).Coordinates;
        var spawns = _entityTable.GetSpawns(machine.Comp.PossibleRewards);

        foreach (var proto in spawns)
            Spawn(proto, coords);

        machine.Comp.RewardAmount--;
    }
}
