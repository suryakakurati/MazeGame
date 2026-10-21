using MazeGame.Core.Game;
using MazeGame.Core.Maze;

namespace MazeGame.UI.Input;

/// <summary>
/// Applies a <see cref="GameCommand"/> to the game.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place that knows the direction keys move the aiming cursor rather than the
/// player while a barrier is being placed. The keyboard layouts themselves live in the
/// frontends, so adding a second way to play (gamepad, replay file) means translating into
/// these commands, not copying the rules.
/// </para>
/// <para>
/// <see cref="GameState"/> still has the final say: every method here may silently refuse
/// (no barrier budget, aimed at an occupied cell, game already over), which is why nothing
/// here checks the rules itself.
/// </para>
/// </remarks>
public static class GameCommands
{
    /// <summary>
    /// Applies <paramref name="command"/> to <paramref name="state"/>.
    /// </summary>
    /// <param name="state">The game to act on.</param>
    /// <param name="command">The intention to carry out.</param>
    /// <returns>
    /// <see langword="true"/> when play should continue, <see langword="false"/> when the
    /// command was <see cref="GameCommand.Quit"/>.
    /// </returns>
    public static bool Apply(GameState state, GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);

        switch (command)
        {
            case GameCommand.MoveNorth:
                Move(state, Direction.North);
                break;
            case GameCommand.MoveSouth:
                Move(state, Direction.South);
                break;
            case GameCommand.MoveWest:
                Move(state, Direction.West);
                break;
            case GameCommand.MoveEast:
                Move(state, Direction.East);
                break;
            case GameCommand.BeginAim:
                state.BeginBarrierPlacement();
                break;
            case GameCommand.BeginLureAim:
                state.BeginLurePlacement();
                break;
            case GameCommand.ConfirmAim:
                state.ConfirmPlacement();
                break;
            case GameCommand.CancelAim:
                state.CancelPlacement();
                break;
            case GameCommand.Blink:
                state.TryBlink();
                break;
            case GameCommand.Veil:
                state.TryVeil();
                break;
            case GameCommand.Drag:
                state.TryDrag();
                break;
            case GameCommand.Lantern:
                state.TryThrowLantern();
                break;
            case GameCommand.Shove:
                state.TryShove();
                break;
            case GameCommand.Quit:
                return false;
        }

        return true;
    }

    /// <summary>
    /// Sends a direction to the aiming cursor while aiming, and to the player otherwise.
    /// </summary>
    private static void Move(GameState state, Direction direction)
    {
        if (state.IsAiming)
        {
            state.MoveAimCursor(direction);
        }
        else
        {
            state.TryMove(direction);
        }
    }
}
