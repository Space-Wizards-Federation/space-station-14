using JetBrains.Annotations;

namespace Content.Shared.Arcade.Systems;

public abstract partial class SharedArcadeSystem
{
    /// <summary>
    ///     Ends the arcade game on a win.
    /// </summary>
    /// <param name="player">The entity playing the game.</param>
    /// <param name="machine">The arcade machine entity.</param>
    /// <param name="score">The (optional) final score associated with this game session.</param>
    [PublicAPI]
    public void WinGame(EntityUid? player, EntityUid machine, int? score = null)
    {
        FinishGame(player, machine, ArcadeGameResult.Win, score);
    }

    /// <summary>
    ///     Ends the arcade game on a loss.
    /// </summary>
    /// <param name="player">The entity playing the game.</param>
    /// <param name="machine">The arcade machine entity.</param>
    /// <param name="score">The (optional) final score associated with this game session.</param>
    [PublicAPI]
    public void LoseGame(EntityUid? player, EntityUid machine, int? score = null)
    {
        FinishGame(player, machine, ArcadeGameResult.Loss, score);
    }

    /// <summary>
    ///     Ends the arcade game without finishing it (i.e. quitting early).
    /// </summary>
    /// <param name="player">The entity playing the game.</param>
    /// <param name="machine">The arcade machine entity.</param>
    /// <param name="score">The (optional) final score associated with this game session.</param>
    [PublicAPI]
    public void LeaveGame(EntityUid? player, EntityUid machine, int? score = null)
    {
        FinishGame(player, machine, ArcadeGameResult.Forfeit, score);
    }

    /// <summary>
    ///     Ends the arcade game on a draw (game finished, neither win nor lose).
    /// </summary>
    /// <param name="player">The entity playing the game.</param>
    /// <param name="machine">The arcade machine entity.</param>
    /// <param name="score">The (optional) final score associated with this game session.</param>
    [PublicAPI]
    public void DrawGame(EntityUid? player, EntityUid machine, int? score = null)
    {
        FinishGame(player, machine, ArcadeGameResult.Draw, score);
    }
}
