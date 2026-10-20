using System;
using MazeGame.Core.Game;
using MazeGame.UI.Input;
using MazeGame.UI.Rendering;
using Raylib_cs;

namespace MazeGame.UI.Frontends;

/// <summary>
/// Plays the game in a Raylib window: owns the window, maps keys to commands and runs the
/// frame loop.
/// </summary>
/// <remarks>
/// The frontend, not the renderer, brackets each frame with <c>BeginDrawing</c> /
/// <c>EndDrawing</c>, so the renderer stays a plain "fill in this frame" component. When the
/// game ends the last frame stays on screen until the player presses Q or Esc or closes the
/// window, instead of the window vanishing before the outcome can be read.
/// </remarks>
internal static class RaylibFrontend
{
    /// <summary>
    /// Opens the window and plays <paramref name="state"/> until it ends or the player quits.
    /// </summary>
    /// <param name="state">The game to play.</param>
    /// <returns>
    /// <see langword="true"/> when a window was opened and the game ran;
    /// <see langword="false"/> when no window is available (for example on a headless
    /// machine), so the caller can fall back to the console frontend.
    /// </returns>
    public static bool TryRun(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var (width, height) = RaylibLayout.WindowSize(state.Config);

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
            RunLoop(state);
        }
        finally
        {
            Raylib.CloseWindow();
        }

        Console.WriteLine(ConsoleFrontend.DescribeEnding(state.Status));
        return true;
    }

    /// <summary>
    /// Runs frames until the game ends and the player dismisses the ending, or the window is
    /// closed.
    /// </summary>
    private static void RunLoop(GameState state)
    {
        var renderer = new RaylibRenderer();

        while (!Raylib.WindowShouldClose())
        {
            if (state.Status == GameStatus.InProgress)
            {
                if (TryReadCommand(state.IsPlacingBarrier, out var command) && !GameCommands.Apply(state, command))
                {
                    return;
                }

                // Measured frame time: the killer's pace must not depend on the frame rate.
                state.Tick(Raylib.GetFrameTime());
            }
            else if (Pressed(KeyboardKey.Q) || Pressed(KeyboardKey.Escape))
            {
                return;
            }

            Raylib.BeginDrawing();
            renderer.Draw(state);
            Raylib.EndDrawing();
        }
    }

    /// <summary>
    /// Reads at most one command for this frame. Escape means "cancel" while aiming and "quit"
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
    /// Tests a key press including the operating system's auto-repeat, so holding a direction
    /// key keeps walking instead of requiring one tap per cell.
    /// </summary>
    private static bool Pressed(KeyboardKey key) =>
        Raylib.IsKeyPressed(key) || Raylib.IsKeyPressedRepeat(key);
}
