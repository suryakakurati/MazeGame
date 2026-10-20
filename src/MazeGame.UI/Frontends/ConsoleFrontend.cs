using System;
using System.Diagnostics;
using System.Threading;
using MazeGame.Core.Game;
using MazeGame.UI.Input;
using MazeGame.UI.Rendering;

namespace MazeGame.UI.Frontends;

/// <summary>
/// Plays the game in the terminal: reads keys, forwards measured frame time and draws the
/// ASCII view.
/// </summary>
/// <remarks>
/// This is the fallback frontend — used with <c>--console</c>, and automatically when no
/// window can be opened. It plays by exactly the same rules as the window, including fog of
/// war; only the drawing differs.
/// </remarks>
internal static class ConsoleFrontend
{
    /// <summary>
    /// Runs the game to completion (or until the player quits), then prints the outcome.
    /// </summary>
    /// <param name="state">The game to play.</param>
    public static void Run(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var renderer = new ConsoleRenderer();
        var clock = Stopwatch.StartNew();

        while (state.Status == GameStatus.InProgress)
        {
            renderer.Draw(state);

            // KeyAvailable throws when input is redirected, so a piped run simply watches.
            if (!Console.IsInputRedirected && Console.KeyAvailable)
            {
                var command = MapKey(Console.ReadKey(intercept: true).Key, state.IsPlacingBarrier);

                if (command is { } chosen && !GameCommands.Apply(state, chosen))
                {
                    Console.WriteLine("You slip back into the dark.");
                    return;
                }
            }

            // Measured elapsed time, not an assumed frame length: the killer's pace must not
            // depend on how fast the machine draws.
            state.Tick(clock.Elapsed.TotalSeconds);
            clock.Restart();

            Thread.Sleep(8);
        }

        renderer.Draw(state);
        Console.WriteLine(DescribeEnding(state.Status));
    }

    /// <summary>
    /// Translates one key press into a command. Escape means "cancel" while aiming and "quit"
    /// otherwise, and Enter only places a barrier while aiming.
    /// </summary>
    private static GameCommand? MapKey(ConsoleKey key, bool aiming) => key switch
    {
        ConsoleKey.W or ConsoleKey.UpArrow => GameCommand.MoveNorth,
        ConsoleKey.S or ConsoleKey.DownArrow => GameCommand.MoveSouth,
        ConsoleKey.A or ConsoleKey.LeftArrow => GameCommand.MoveWest,
        ConsoleKey.D or ConsoleKey.RightArrow => GameCommand.MoveEast,
        ConsoleKey.B or ConsoleKey.Spacebar => aiming ? GameCommand.ConfirmAim : GameCommand.BeginAim,
        ConsoleKey.Enter => aiming ? GameCommand.ConfirmAim : null,
        ConsoleKey.Escape => aiming ? GameCommand.CancelAim : GameCommand.Quit,
        ConsoleKey.Q => GameCommand.Quit,
        _ => null,
    };

    /// <summary>Returns the sentence printed when a game ends.</summary>
    internal static string DescribeEnding(GameStatus status) => status switch
    {
        GameStatus.Won => "You escaped. For now.",
        GameStatus.Caught => "The killer found you in the dark.",
        _ => "You slip back into the dark.",
    };
}
