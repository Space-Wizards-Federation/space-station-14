using Robust.Shared.Serialization;

namespace Content.Shared.Arcade.Systems;

/// <summary>
///     A system that handles logic common to arcade game machines, such as game completion,
///     prize rewards, and scoreboards.
/// </summary>
public abstract partial class SharedArcadeSystem : EntitySystem
{
    private void FinishGame(EntityUid? player, EntityUid machine, ArcadeGameResult result, int? score = null)
    {
        var endedEvent = new ArcadeGameEndedEvent(player, result, score);
        RaiseLocalEvent(machine, ref endedEvent);

        if (player != null)
        {
            var finishEvent = new FinishedArcadeGameEvent(result, score);
            RaiseLocalEvent(player.Value, ref finishEvent);
        }
    }
}

/// <summary>
/// Represents a single entry on the scoreboard of an arcade game.
/// </summary>
[Serializable, NetSerializable]
public sealed class ArcadeHighScoreEntry(string name, int score) : IComparable
{
    /// <summary>
    /// The name of the player associated with this high score entry.
    /// </summary>
    [ViewVariables]
    public string Name = name;

    /// <summary>
    /// The score associated with this high score entry.
    /// </summary>
    [ViewVariables]
    public int Score = score;

    public int CompareTo(object? obj)
    {
        if (obj is not ArcadeHighScoreEntry entry) return 0;
        return Score.CompareTo(entry.Score);
    }
}

/// <summary>
///     Called on the arcade machine entity when a game ends for any reason.
/// </summary>
/// <param name="Player">The entity playing the arcade game.</param>
/// <param name="Result">The result of the game.</param>
/// <param name="Score">The (optional) final score associated with this game session.</param>
[ByRefEvent]
public record struct ArcadeGameEndedEvent(EntityUid? Player,
    ArcadeGameResult Result = ArcadeGameResult.Forfeit,
    int? Score = null)
{
    public EntityUid? Player = Player;
    public ArcadeGameResult Result = Result;
    public int? Score = Score;
}

/// <summary>
///     Called on the arcade game player entity when they finish an arcade game for any reason.
/// </summary>
/// <param name="Result">The result of the game.</param>
/// <param name="Score">The (optional) final score associated with this game session.</param>
[ByRefEvent]
public record struct FinishedArcadeGameEvent(ArcadeGameResult Result, int? Score = null)
{
    public ArcadeGameResult Result = Result;
    public int? Score = Score;
}

/// <summary>
/// The outcome of a completed arcade game session.
/// </summary>
/// <remarks>
/// Entity systems may use this result to perform certain logic - for example, only dispensing a prize if the game is won.
/// </remarks>
public enum ArcadeGameResult
{
    Win,
    Draw,
    Forfeit,
    Loss,
}
