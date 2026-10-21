using System;
using System.Diagnostics;
using System.Threading;
using MazeGame.Core.Game;
using MazeGame.UI.Input;
using MazeGame.UI.Rendering;

namespace MazeGame.UI.Frontends;

/// <summary>
/// Plays a whole campaign in the terminal: each floor in ASCII, with a floor heading, a
/// floor-cleared beat and the run summary.
/// </summary>
/// <remarks>
/// <para>
/// This is the fallback frontend — used with <c>--console</c>, and automatically when no window
/// can be opened. It plays by exactly the same rules as the window, including hearts and the
/// multi-floor run; only the drawing differs.
/// </para>
/// <para>
/// The console cannot read keys when its input is redirected, so a piped run skips the title and
/// the "press any key" beats, watches one campaign to its end and exits. That keeps smoke tests
/// and logging possible without a keyboard.
/// </para>
/// </remarks>
internal static class ConsoleFrontend
{
    /// <summary>
    /// Runs campaigns until the player quits, printing the outcome of each run.
    /// </summary>
    /// <param name="createCampaign">
    /// Builds a fresh, playable run from floor 1. Called once per run, so a replay starts over.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="createCampaign"/> is null.
    /// </exception>
    public static void Run(Func<Campaign> createCampaign)
    {
        ArgumentNullException.ThrowIfNull(createCampaign);

        // The title needs a keypress to leave, which a redirected run can never provide.
        if (!Console.IsInputRedirected)
        {
            DrawTitle();
        }

        while (true)
        {
            var campaign = createCampaign();
            var quit = PlayCampaign(campaign);

            if (quit)
            {
                return; // the player quit mid-run, so there is no outcome to report
            }

            if (campaign.Status == CampaignStatus.Won)
            {
                Console.WriteLine(
                    $"You escaped the depths: {campaign.FloorCount} floors, {campaign.Hearts} heart(s) left.");
            }
            else
            {
                Console.WriteLine(DescribeEnding(GameStatus.Caught));
                Console.WriteLine(
                    $"Floor {campaign.FloorNumber} of {campaign.FloorCount}, " +
                    $"{campaign.FloorsCleared} cleared.");
            }

            if (Console.IsInputRedirected || !WantsAnotherDescent())
            {
                return;
            }
        }
    }

    /// <summary>
    /// Plays every floor of one run until it ends or the player quits.
    /// </summary>
    /// <param name="campaign">The run to play.</param>
    /// <returns>
    /// <see langword="true"/> when the player quit mid-run; <see langword="false"/> when the run
    /// ended by winning or losing.
    /// </returns>
    private static bool PlayCampaign(Campaign campaign)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"FLOOR {campaign.FloorNumber}/{campaign.FloorCount} — " +
                $"{campaign.CurrentFloor.Name.ToUpperInvariant()}");

            PauseForDescent();

            Play(campaign.Floor);

            if (campaign.Floor.Status == GameStatus.InProgress)
            {
                return true;
            }

            if (campaign.Status == CampaignStatus.Lost)
            {
                return false;
            }

            // The floor is cleared: either the run continues or this was the last one.
            var cleared = campaign.FloorNumber;

            if (campaign.Draft is not null)
            {
                Console.WriteLine();

                if (PromptForDraft(campaign))
                {
                    return true;
                }

                Console.WriteLine($"run: {campaign.Modifiers.Describe()}");
            }

            if (!campaign.TryAdvanceFloor())
            {
                return false;
            }

            Console.WriteLine(
                $"Floor {cleared} cleared — carrying {campaign.Hearts} heart(s) to floor " +
                $"{campaign.FloorNumber}.");
        }
    }

    /// <summary>
    /// Offers the cards between two floors and applies the player's choice.
    /// </summary>
    /// <param name="campaign">The run whose draft is open.</param>
    /// <returns>
    /// <see langword="true"/> when the player asked to leave; <see langword="false"/> when the
    /// draft was resolved and the run can continue.
    /// </returns>
    /// <remarks>
    /// The offers are printed even when the input is a pipe, so a log shows the run's choices;
    /// a piped run cannot press a key, so it takes the heart. Taking a hit point is the safe
    /// default: it never spends a card the player did not choose.
    /// </remarks>
    private static bool PromptForDraft(Campaign campaign)
    {
        var draft = campaign.Draft;
        if (draft is null)
        {
            return false;
        }

        Console.WriteLine("DRAFT — pick one:");

        for (var index = 0; index < draft.Offers.Count; index++)
        {
            var card = draft.Offers[index];
            Console.WriteLine($"  [{index + 1}] {card.Name} ({card.Tier}): {card.FaceText}");
        }

        Console.WriteLine("  [S] skip: heal one heart");

        if (Console.IsInputRedirected)
        {
            campaign.SkipDraft();
            Console.WriteLine("  (no input: skipped for a heart)");
            return false;
        }

        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;

            if (key is ConsoleKey.Q or ConsoleKey.Escape)
            {
                return true;
            }

            if (key == ConsoleKey.S)
            {
                campaign.SkipDraft();
                Console.WriteLine("  skipped: one heart restored.");
                return false;
            }

            var index = key - ConsoleKey.D1;
            if (index >= 0 && index < draft.Offers.Count && campaign.TakeCard(draft.Offers[index]))
            {
                Console.WriteLine($"  took {draft.Offers[index].Name}.");
                return false;
            }
        }
    }

    /// <summary>
    /// Waits for a key before a floor starts, unless the input is a pipe.
    /// </summary>
    /// <remarks>
    /// The line is printed even when input is redirected, so a log still shows the floor order
    /// and the run's pacing.
    /// </remarks>
    private static void PauseForDescent()
    {
        if (Console.IsInputRedirected)
        {
            return;
        }

        Console.WriteLine("press any key to descend...");
        Console.ReadKey(intercept: true);
    }

    /// <summary>
    /// Plays one floor until it ends or the player quits, drawing a frame per iteration.
    /// </summary>
    /// <param name="state">The floor to play. It is left in whatever state the run ended in.</param>
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
        ConsoleKey.Spacebar => GameCommand.Blink,
        ConsoleKey.V => GameCommand.Veil,
        ConsoleKey.T => GameCommand.Drag,
        ConsoleKey.L => GameCommand.Lantern,
        ConsoleKey.F => GameCommand.Shove,
        ConsoleKey.Enter => aiming ? GameCommand.ConfirmAim : null,
        ConsoleKey.Escape => aiming ? GameCommand.CancelAim : GameCommand.Quit,
        ConsoleKey.Q => GameCommand.Quit,
        _ => null,
    };

    /// <summary>Returns the sentence printed when a run is lost.</summary>
    internal static string DescribeEnding(GameStatus status) => status switch
    {
        GameStatus.Won => "You escaped. For now.",
        GameStatus.Caught => "The killer took your last heart.",
        _ => "You slip back into the dark.",
    };
}
