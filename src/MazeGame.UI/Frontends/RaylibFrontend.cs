using System;
using MazeGame.Core.Game;
using MazeGame.UI.Input;
using MazeGame.UI.Rendering;
using Raylib_cs;

namespace MazeGame.UI.Frontends;

/// <summary>
/// Plays the game in a Raylib window: owns the window, the title screen, the key mapping and the
/// frame loop, and starts a fresh game whenever the player asks for another descent.
/// </summary>
/// <remarks>
/// <para>
/// The window has two screens: the title (a menu over the cave) and the game itself. There is no
/// third screen for the ending — the game over card sits on top of the last frame, so the player
/// can still see where they were caught and press R straight into a new run.
/// </para>
/// <para>
/// The frontend, not the renderer, brackets each frame with <c>BeginDrawing</c> /
/// <c>EndDrawing</c>, so the renderer stays a plain "fill in this frame" component. Starting a
/// game is a call to the injected factory: no maze generation or placement rule lives here, and a
/// replay is the same call as the first run.
/// </para>
/// </remarks>
internal static class RaylibFrontend
{
    /// <summary>Menu entries on the title screen.</summary>
    private const int MenuItemCount = 2;

    private const int DescendItem = 0;

    /// <summary>
    /// Opens the window and plays games until the player leaves.
    /// </summary>
    /// <param name="config">The settings the window geometry and the games are built from.</param>
    /// <param name="createGame">
    /// Builds a fresh, playable game. Called once for the first descent and again for every
    /// replay, so a restart is always a new maze with everything restored.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a window was opened and the session ran;
    /// <see langword="false"/> when no window is available (for example on a headless
    /// machine), so the caller can fall back to the console frontend.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="config"/> or <paramref name="createGame"/> is null.
    /// </exception>
    public static bool TryRun(GameConfig config, Func<GameState> createGame)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(createGame);

        var (width, height) = RaylibLayout.WindowSize(config);

        // Warnings only: Raylib's startup banner would otherwise be printed over the ending.
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.InitWindow(width, height, RaylibLayout.Title);

        if (!Raylib.IsWindowReady())
        {
            Console.WriteLine("No game window available; falling back to the console version.");
            Raylib.CloseWindow();
            return false;
        }

        Raylib.SetTargetFPS(RaylibLayout.TargetFps);

        try
        {
            var lastGame = RunLoop(createGame);

            // Only report an outcome if there is one: leaving from the title, or mid-run after a
            // replay, should not print the previous game's ending.
            if (lastGame is { } game && game.Status != GameStatus.InProgress)
            {
                Console.WriteLine(ConsoleFrontend.DescribeEnding(game.Status));
            }
        }
        finally
        {
            Raylib.CloseWindow();
        }

        return true;
    }

    /// <summary>
    /// Runs frames until the window closes or the player leaves, switching between the title
    /// screen and the game.
    /// </summary>
    /// <param name="createGame">Builds each new game.</param>
    /// <returns>
    /// The last game played, or <see langword="null"/> when the player left from the title
    /// screen without ever starting a run.
    /// </returns>
    private static GameState? RunLoop(Func<GameState> createGame)
    {
        var renderer = new RaylibRenderer();
        GameState? state = null;
        var onTitle = true;
        var selected = DescendItem;

        while (!Raylib.WindowShouldClose())
        {
            if (onTitle)
            {
                switch (ReadTitleChoice(ref selected))
                {
                    case TitleChoice.Quit:
                        return null;

                    case TitleChoice.Descend:
                        state = createGame();

                        // A new maze must not inherit the previous cave's explored cells.
                        renderer.Reset();
                        onTitle = false;
                        break;
                }
            }
            else if (state!.Status == GameStatus.InProgress)
            {
                if (TryReadCommand(state.IsAiming, out var command) && !GameCommands.Apply(state, command))
                {
                    return state;
                }

                // Measured frame time: the killer's pace must not depend on the frame rate.
                state.Tick(Raylib.GetFrameTime());
            }
            else if (Pressed(KeyboardKey.R) || Pressed(KeyboardKey.Enter) || Pressed(KeyboardKey.KpEnter))
            {
                // Replay is the same call as the first descent; the renderer just has to forget
                // the previous cave.
                state = createGame();
                renderer.Reset();
            }
            else if (Pressed(KeyboardKey.Q) || Pressed(KeyboardKey.Escape))
            {
                return state;
            }

            Raylib.BeginDrawing();
            if (onTitle)
            {
                renderer.DrawTitle(selected);
            }
            else
            {
                renderer.Draw(state!);
            }

            Raylib.EndDrawing();
        }

        return onTitle ? null : state;
    }

    /// <summary>
    /// Reads one command for this frame. Escape means "cancel" while aiming and "quit"
    /// otherwise, mirroring the console frontend.
    /// </summary>
    private static bool TryReadCommand(bool aiming, out GameCommand command)
    {
        if (Pressed(KeyboardKey.Q) || (!aiming && Pressed(KeyboardKey.Escape)))
        {
            command = GameCommand.Quit;
            return true;
        }

        if (Pressed(KeyboardKey.W) || Pressed(KeyboardKey.Up))
        {
            command = GameCommand.MoveNorth;
            return true;
        }

        if (Pressed(KeyboardKey.S) || Pressed(KeyboardKey.Down))
        {
            command = GameCommand.MoveSouth;
            return true;
        }

        if (Pressed(KeyboardKey.A) || Pressed(KeyboardKey.Left))
        {
            command = GameCommand.MoveWest;
            return true;
        }

        if (Pressed(KeyboardKey.D) || Pressed(KeyboardKey.Right))
        {
            command = GameCommand.MoveEast;
            return true;
        }

        if (Pressed(KeyboardKey.B) || Pressed(KeyboardKey.Space))
        {
            command = aiming ? GameCommand.ConfirmAim : GameCommand.BeginAim;
            return true;
        }

        if (Pressed(KeyboardKey.R))
        {
            command = aiming ? GameCommand.ConfirmAim : GameCommand.BeginLureAim;
            return true;
        }

        if (aiming && (Pressed(KeyboardKey.Enter) || Pressed(KeyboardKey.KpEnter)))
        {
            command = GameCommand.ConfirmAim;
            return true;
        }

        if (aiming && Pressed(KeyboardKey.Escape))
        {
            command = GameCommand.CancelAim;
            return true;
        }

        command = default;
        return false;
    }

    /// <summary>
    /// Reads one title-screen command: move the highlight, start a descent, or leave.
    /// </summary>
    /// <param name="selected">The highlighted entry, updated in place by W/S or the arrows.</param>
    /// <returns>What the player chose this frame.</returns>
    private static TitleChoice ReadTitleChoice(ref int selected)
    {
        if (Pressed(KeyboardKey.Q) || Pressed(KeyboardKey.Escape))
        {
            return TitleChoice.Quit;
        }

        // Two entries, so either direction simply wraps; the arithmetic keeps working if a third
        // entry is ever added between them.
        if (Pressed(KeyboardKey.W) || Pressed(KeyboardKey.Up))
        {
            selected = (selected + MenuItemCount - 1) % MenuItemCount;
            return TitleChoice.None;
        }

        if (Pressed(KeyboardKey.S) || Pressed(KeyboardKey.Down))
        {
            selected = (selected + 1) % MenuItemCount;
            return TitleChoice.None;
        }

        if (Pressed(KeyboardKey.Enter) || Pressed(KeyboardKey.KpEnter) || Pressed(KeyboardKey.Space))
        {
            return selected == DescendItem ? TitleChoice.Descend : TitleChoice.Quit;
        }

        return TitleChoice.None;
    }

    /// <summary>
    /// Tests a key press including the operating system's auto-repeat, so holding a direction
    /// key keeps walking instead of requiring one tap per cell.
    /// </summary>
    private static bool Pressed(KeyboardKey key) =>
        Raylib.IsKeyPressed(key) || Raylib.IsKeyPressedRepeat(key);

    /// <summary>What the player asked for on the title screen.</summary>
    private enum TitleChoice
    {
        /// <summary>Nothing yet: keep showing the title.</summary>
        None,

        /// <summary>Start a game.</summary>
        Descend,

        /// <summary>Close the window.</summary>
        Quit,
    }
}
