using System;
using System.Diagnostics;
using System.Threading;
using MazeGame.Core.Game;
using MazeGame.UI.Input;
using MazeGame.UI.Rendering;

namespace MazeGame.UI.Frontends;

/// <summary>
/// Plays the game in the terminal: reads keys, forwards measured frame time, draws the ASCII view
/// and offers another descent once a run ends.
/// </summary>
/// <remarks>
/// <para>
/// This is the fallback frontend — used with <c>--console</c>, and automatically when no window
/// can be opened. It plays by exactly the same rules as the window, including fog of war and
/// replay; only the drawing differs.
/// </para>
/// <para>
/// The console cannot read keys when its input is redirected, so a piped run skips the title
/// prompt, watches one game to its end and exits. That keeps smoke tests and logging possible
/// without a keyboard.
/// </para>
/// </remarks>
internal static class ConsoleFrontend
{
    /// <summary>
    /// Runs games until the player quits, printing the outcome of each run.
    /// </summary>
    /// <param name="createGame">
    /// Builds a fresh, playable game. Called once per run, so a replay starts from scratch.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="createGame"/> is null.</exception>
    public static void Run(Func<GameState> createGame)
    {
        ArgumentNullException.ThrowIfNull(createGame);

        // The title needs a keypress to leave, which a redirected run can never provide.
        if (!Console.IsInputRedirected)
        {
            DrawTitle();
        }

        while (true)
        {
            var state = createGame();
            Play(state);

            if (state.Status == GameStatus.InProgress)
            {
                return; // the player quit mid-run, so there is no outcome to report
            }

            Console.WriteLine(DescribeEnding(state.Status));

            if (Console.IsInputRedirected || !WantsAnotherDescent())
            {
                return;
            }
        }
    }

    /// <summary>
    /// Plays one game until it ends or the player quits, drawing a frame per iteration.
    /// </summary>
    /// <param name="state">The game to play. It is left in whatever state the run ended in.</param>
    private static void Play(GameState state)
    {
        var renderer = new ConsoleRenderer();
        var clock = Stopwatch.StartNew();

        while (state.Status == GameStatus.InProgress)
        {
            renderer.Draw(state);

            // KeyAvailable throws when input is redirected, so a piped run simply watches.
            if (!Console.IsInputRedirected && Console.KeyAvailable)
            {
                var command = MapKey(Console.ReadKey(intercept: true).Key, state.IsAiming);

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

        // The last frame stays on screen so the ending can be read next to it.
        renderer.Draw(state);
    }

    /// <summary>
    /// Draws the terminal title screen and waits for a key, so the two frontends open the same way.
    /// </summary>
    private static void DrawTitle()
    {
        if (!Console.IsOutputRedirected)
        {
            Console.Clear();
        }

        // ASCII only, and spaced by hand: the console has no letter-spacing to lean on.
        Console.WriteLine();
        Console.WriteLine("      A   D E S C E N T   I N   T H E   D A R K");
        Console.WriteLine();
        Console.WriteLine("      B I O L U M I N E S C E N T");
        Console.WriteLine("                       D E P T H S");
        Console.WriteLine();
        Console.WriteLine("   collect the keys. open the arch. do not be caught.");
        Console.WriteLine();
        Console.WriteLine("   press any key to descend...");
        Console.ReadKey(intercept: true);
    }

    /// <summary>
    /// Asks whether the player wants another run, and waits for an answer.
    /// </summary>
    /// <returns><see langword="true"/> for another descent, <see langword="false"/> to leave.</returns>
    /// <remarks>Only called when input is interactive; a redirected run cannot answer.</remarks>
    private static bool WantsAnotherDescent()
    {
        Console.WriteLine();
        Console.WriteLine("[R] descend again    [Q] leave");

        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;

            if (key == ConsoleKey.R)
            {
                return true;
            }

            if (key is ConsoleKey.Q or ConsoleKey.Escape)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Translates one key press into a command. Escape means "cancel" while aiming and "quit"
    /// otherwise, B aims a barrier, R aims a wisp, and Enter only places while aiming.
    /// </summary>
    private static GameCommand? MapKey(ConsoleKey key, bool aiming) => key switch
    {
        ConsoleKey.W or ConsoleKey.UpArrow => GameCommand.MoveNorth,
        ConsoleKey.S or ConsoleKey.DownArrow => GameCommand.MoveSouth,
        ConsoleKey.A or ConsoleKey.LeftArrow => GameCommand.MoveWest,
        ConsoleKey.D or ConsoleKey.RightArrow => GameCommand.MoveEast,
        ConsoleKey.B or ConsoleKey.Spacebar => aiming ? GameCommand.ConfirmAim : GameCommand.BeginAim,
        ConsoleKey.R => aiming ? GameCommand.ConfirmAim : GameCommand.BeginLureAim,
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
