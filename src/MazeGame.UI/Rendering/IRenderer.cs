using MazeGame.Core.Game;

namespace MazeGame.UI.Rendering;

/// <summary>
/// Draws one frame of the game.
/// </summary>
/// <remarks>
/// The renderer only reads <see cref="GameState"/>; it is not allowed to change it. Keeping
/// this to a single method means the console debug view and a future Raylib window are
/// interchangeable, and the game rules stay in Core.
/// </remarks>
public interface IRenderer
{
    /// <summary>
    /// Draws the current state.
    /// </summary>
    /// <param name="state">The game to draw. It is not modified.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="state"/> is <see langword="null"/>.
    /// </exception>
    void Draw(GameState state);
}
