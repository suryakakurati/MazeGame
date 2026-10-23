using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MazeGame.Core.Game;
using MazeGame.UI.Input;
using MazeGame.UI.Persistence;
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
    /// <param name="restoreCampaign">Rebuilds the saved run for CONTINUE.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="createCampaign"/> or <paramref name="restoreCampaign"/> is null.
    /// </exception>
    public static void Run(
        Func<Campaign> createCampaign,
        Func<RunSnapshot, Campaign> restoreCampaign)
    {
        ArgumentNullException.ThrowIfNull(createCampaign);
        ArgumentNullException.ThrowIfNull(restoreCampaign);

        var saves = new SaveStore();
        var garden = saves.LoadGarden();

        // The title needs a keypress to leave, which a redirected run can never provide.
        var action = Console.IsInputRedirected
            ? TitleAction.Descend
            : ShowTitleMenu(saves.HasRun, garden.Blooms);

        while (true)
        {
            if (action == TitleAction.Leave)
            {
                return;
            }

            var campaign = action == TitleAction.Resume ? Resume() : createCampaign();

            if (action == TitleAction.Resume)
            {
                Console.WriteLine($"  resumed on floor {campaign.FloorNumber}, {campaign.Hearts} heart(s).");
            }

            var exit = PlayCampaign(campaign, saves);

            if (exit != RunExit.Ended)
            {
                if (exit == RunExit.Saved)
                {
                    // Save and quit keeps the run: the title's CONTINUE rebuilds this floor.
                    Console.WriteLine("  the run is saved — CONTINUE on the title picks it up.");
                }
                else
                {
                    // Abandoning the run throws its save away: the title is where the player
                    // chooses what to do next.
                    Console.WriteLine("  the run is abandoned.");
                    saves.ClearRun();
                }

                if (Console.IsInputRedirected)
                {
                    return;
                }

                action = ShowTitleMenu(saves.HasRun, garden.Blooms);
                continue;
            }

            // The run is over: its blooms join the garden, and there is nothing left to resume.
            garden = saves.RecordRun(campaign.GardenCount, campaign.FloorNumber);
            saves.ClearRun();

            if (campaign.Status == CampaignStatus.Won)
            {
                Console.WriteLine(
                    $"You escaped the depths: {campaign.FloorCount} floors, {campaign.Hearts} heart(s) " +
                    $"left, {campaign.GardenCount} bloom(s) this run; the garden holds {garden.Blooms}.");
            }
            else
            {
                Console.WriteLine(DescribeEnding(GameStatus.Caught));
                Console.WriteLine(
                    $"Floor {campaign.FloorNumber} of {campaign.FloorCount}, " +
                    $"{campaign.FloorsCleared} cleared; the garden holds {garden.Blooms} bloom(s).");
            }

            if (Console.IsInputRedirected || !WantsAnotherDescent())
            {
                return;
            }

            action = ShowTitleMenu(saves.HasRun, garden.Blooms);
        }

        Campaign Resume()
        {
            var snapshot = saves.LoadRun();

            if (snapshot is not null)
            {
                try
                {
                    return restoreCampaign(snapshot);
                }
                catch (ArgumentException)
                {
                    // A save from another build: refuse it and start over rather than refusing to
                    // start at all.
                }
            }

            saves.ClearRun();
            return createCampaign();
        }
    }

    /// <summary>
    /// Plays every floor of one run until it ends or the player quits.
    /// </summary>
    /// <param name="campaign">The run to play.</param>
    /// <param name="saves">The store that remembers the run between floors.</param>
    /// <returns>
    /// <see cref="RunExit.Saved"/> when the player left through SAVE &amp; QUIT,
    /// <see cref="RunExit.Abandoned"/> when they left through ABANDON RUN, and
    /// <see cref="RunExit.Ended"/> when the run ended by winning or losing.
    /// </returns>
    /// <remarks>
    /// The save is written at the top of every floor and again when the player saves from the
    /// pause card, so a crash costs at most the floor being played: CONTINUE rebuilds that floor
    /// from the run's seed.
    /// </remarks>
    private static RunExit PlayCampaign(Campaign campaign, SaveStore saves)
    {
        while (true)
        {
            saves.SaveRun(campaign.Snapshot());

            Console.WriteLine();
            Console.WriteLine(
                $"FLOOR {campaign.FloorNumber}/{campaign.FloorCount} — " +
                $"{campaign.CurrentFloor.Name.ToUpperInvariant()}");

            PauseForDescent();

            var exit = Play(campaign);

            if (exit == RunExit.Saved)
            {
                // Written here rather than in Play: the pause card asks, and the run is what
                // knows its hearts, blooms and cards.
                saves.SaveRun(campaign.Snapshot());
                return RunExit.Saved;
            }

            if (exit == RunExit.Abandoned)
            {
                return RunExit.Abandoned;
            }

            if (campaign.Status == CampaignStatus.Lost)
            {
                return RunExit.Ended;
            }

            // The floor is cleared: either the run continues or this was the last one. The page
            // is read here, on the same beat the window puts it on the cleared card.
            var cleared = campaign.FloorNumber;

            PrintPage(campaign);

            if (campaign.Draft is not null)
            {
                Console.WriteLine();

                PromptForDraft(campaign);
                Console.WriteLine($"run: {campaign.Modifiers.Describe()}");
            }

            if (!campaign.TryAdvanceFloor())
            {
                return RunExit.Ended;
            }

            Console.WriteLine(
                $"Floor {cleared} cleared — carrying {campaign.Hearts} heart(s) to floor " +
                $"{campaign.FloorNumber}.");
        }
    }

    /// <summary>
    /// Reads the Echo aloud: the gardener's lines, then a keypress to let the floor go.
    /// </summary>
    /// <param name="campaign">The run whose floor holds the Echo.</param>
    /// <remarks>
    /// The last line always teaches the floor's rule, which is why the Echo is also the tutorial:
    /// story and instruction never compete for the player's attention.
    /// </remarks>
    private static void ReadEcho(Campaign campaign)
    {
        var floor = campaign.Floor;

        Console.WriteLine();
        Console.WriteLine(new string('=', 64));
        Console.WriteLine($"  THE ECHO — {campaign.CurrentPage.Title}");
        Console.WriteLine(new string('-', 64));

        foreach (var line in floor.EchoLines)
        {
            foreach (var row in Wrap(line, 58))
            {
                Console.WriteLine($"    {row}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("  IT WARMS YOU: ONE HEART");
        Console.WriteLine(new string('=', 64));

        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("  press any key to let go...");
            Console.ReadKey(intercept: true);
        }

        floor.CloseEcho();
    }

    /// <summary>
    /// Prints the page of the gardener's book that the cleared floor turned, in a box that owns the
    /// screen the way the window's page does, and waits to be read.
    /// </summary>
    /// <param name="campaign">The run that just cleared a floor.</param>
    /// <remarks>
    /// A terminal cannot grow its type, so the page is made big the only way it can be: a full-width
    /// frame, air around the prose, and a keypress before the draft replaces it. Before this the
    /// page and the draft printed back to back, so in an interactive run the story scrolled past
    /// before it could be read.
    /// </remarks>
    private static void PrintPage(Campaign campaign)
    {
        var page = campaign.CurrentPage;
        const int Width = 64;
        var rule = new string('=', Width);
        var thin = new string('-', Width);

        Console.WriteLine();
        Console.WriteLine(rule);
        Console.WriteLine($"  PAGE {page.FloorNumber} — {page.Title}");
        Console.WriteLine($"  FLOOR {campaign.FloorNumber} CLEARED");
        Console.WriteLine(thin);
        Console.WriteLine();

        foreach (var line in page.Lines)
        {
            foreach (var row in Wrap(line, Width - 6))
            {
                Console.WriteLine($"    {row}");
            }

            Console.WriteLine();
        }

        if (campaign.CurrentMoodLine is { } mood)
        {
            foreach (var row in Wrap(mood, Width - 6))
            {
                Console.WriteLine($"    {row}");
            }

            Console.WriteLine();
        }

        Console.WriteLine(thin);
        Console.WriteLine(
            $"  garden: {campaign.GardenCount} bloom(s)    hearts: {campaign.Hearts}");
        Console.WriteLine(rule);

        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("  press any key to turn the page...");
            Console.ReadKey(intercept: true);
        }
    }

    /// <summary>Splits <paramref name="text"/> into lines that fit <paramref name="width"/> columns.</summary>
    /// <param name="text">The text to wrap.</param>
    /// <param name="width">The widest a line may be.</param>
    /// <returns>The lines, in order.</returns>
    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = string.Empty;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";

            if (candidate.Length <= width)
            {
                line = candidate;
                continue;
            }

            if (line.Length > 0)
            {
                yield return line;
            }

            line = word;
        }

        if (line.Length > 0)
        {
            yield return line;
        }
    }

    /// <summary>
    /// Offers the cards between two floors and applies the player's choice.
    /// </summary>
    /// <param name="campaign">The run whose draft is open.</param>
    /// <remarks>
    /// A draft has no way out: every run takes a card or pays a heart, so Escape does nothing
    /// here rather than ending a run the player only paused to think in. The offers are printed
    /// even when the input is a pipe, so a log shows the run's choices; a piped run cannot press
    /// a key, so it takes the heart. Taking a hit point is the safe default: it never spends a
    /// card the player did not choose.
    /// </remarks>
    private static void PromptForDraft(Campaign campaign)
    {
        var draft = campaign.Draft;
        if (draft is null)
        {
            return;
        }

        if (Console.IsInputRedirected)
        {
            PrintDraft(campaign);
            campaign.SkipDraft();
            Console.WriteLine("  (no input: skipped for a heart)");
            return;
        }

        while (true)
        {
            // Redrawn after a reroll so the log always shows the table that is actually on offer.
            PrintDraft(campaign);

            var key = Console.ReadKey(intercept: true).Key;

            if (key == ConsoleKey.S)
            {
                campaign.SkipDraft();
                Console.WriteLine("  skipped: one heart restored.");
                return;
            }

            if (key == ConsoleKey.R)
            {
                if (campaign.TryRerollDraft())
                {
                    Console.WriteLine(
                        $"  rerolled for {campaign.Hearts} heart(s) left - the price is now " +
                        $"{campaign.DraftRerollCost} next time.");
                }
                else
                {
                    Console.WriteLine("  not enough hearts to reroll: the last heart is never a price.");
                }

                continue;
            }

            var index = key - ConsoleKey.D1;
            if (index >= 0 && index < draft.Offers.Count && campaign.TakeCard(draft.Offers[index]))
            {
                Console.WriteLine($"  took {draft.Offers[index].Name}.");
                return;
            }
        }
    }

    /// <summary>Prints the current draft's table and the reroll price.</summary>
    /// <param name="campaign">The run whose draft is open.</param>
    private static void PrintDraft(Campaign campaign)
    {
        var draft = campaign.Draft;
        if (draft is null)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("DRAFT — pick one:");

        for (var index = 0; index < draft.Offers.Count; index++)
        {
            var card = draft.Offers[index];
            Console.WriteLine($"  [{index + 1}] {card.Name} ({card.Tier}): {card.FaceText}");
        }

        Console.WriteLine("  [S] skip: heal one heart");
        Console.WriteLine(
            campaign.CanRerollDraft
                ? $"  [R] reroll: spend {campaign.DraftRerollCost} heart(s)"
                : $"  [R] reroll: needs {campaign.DraftRerollCost} heart(s), not enough left");
    }

    /// <summary>
    /// Shows the safe room's card as text and applies the option the player picks.
    /// </summary>
    /// <param name="campaign">The run whose room is open.</param>
    /// <remarks>
    /// The console has no pointer and no layout, so the card is a numbered list with its price on
    /// its own line. A piped run cannot choose, so it closes the card and walks on: taking a stake
    /// on the player's behalf would be spending their hearts for them.
    /// </remarks>
    private static void PromptForRoom(Campaign campaign)
    {
        if (!campaign.OpenRoom())
        {
            return;
        }

        var room = campaign.Room!;

        PrintRoom(room);

        if (Console.IsInputRedirected)
        {
            campaign.CloseRoom();
            return;
        }

        while (!room.IsResolved)
        {
            var key = Console.ReadKey(intercept: true).Key;

            if (key == ConsoleKey.Escape)
            {
                campaign.CloseRoom();
                return;
            }

            var index = key - ConsoleKey.D1;

            if (index >= 0 && index < room.Options.Count &&
                campaign.ResolveRoom(room.Options[index]))
            {
                // Leaving clears the campaign's card without resolving the detached offer, so
                // waiting on IsResolved would read keys forever. Both outcomes end the visit.
                if (room.IsResolved)
                {
                    PrintRoom(room);
                }
                else
                {
                    Console.WriteLine("  you step away.");
                }

                break;
            }
        }

        Console.WriteLine("  press any key to walk on...");
        Console.ReadKey(intercept: true);
        campaign.CloseRoom();
    }

    /// <summary>Prints one room card, or its outcome once it is resolved.</summary>
    private static void PrintRoom(RoomOffer room)
    {
        Console.WriteLine();
        Console.WriteLine($"SAFE ROOM — {room.Name.ToUpperInvariant()}");
        Console.WriteLine($"  {room.Prompt}");

        if (room.IsResolved)
        {
            Console.WriteLine($"  {room.Outcome}");
            return;
        }

        Console.WriteLine($"  price: {room.Price}");

        for (var index = 0; index < room.Options.Count; index++)
        {
            var option = room.Options[index];
            var note = option.Note is { } text ? $" ({text})" : string.Empty;
            Console.WriteLine($"  [{index + 1}] {option.Label}{note}");

            // A card for sale prints its face under the price, so buying it does not depend on
            // remembering what the name did.
            if (option.Preview is { Length: > 0 } preview)
            {
                foreach (var line in Wrap(preview, 58))
                {
                    Console.WriteLine($"      {line}");
                }
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
    /// Plays one floor until it ends or the player leaves from the pause card, drawing a frame per
    /// iteration.
    /// </summary>
    /// <param name="campaign">The run to play. Its floor is left in whatever state the run ended in.</param>
    /// <returns>
    /// <see cref="RunExit.Ended"/> when the floor ended on its own, otherwise the way the player
    /// chose to leave it from the pause card.
    /// </returns>
    private static RunExit Play(Campaign campaign)
    {
        var state = campaign.Floor;
        var renderer = new ConsoleRenderer();
        var clock = Stopwatch.StartNew();

        // The relic card is read once per floor, at the moment the altar is touched. The floor
        // number is enough here: this loop only ever plays one campaign.
        var relicShownFor = -1;

        while (state.Status == GameStatus.InProgress)
        {
            renderer.Draw(state);

            // KeyAvailable throws when input is redirected, so a piped run simply watches.
            if (!Console.IsInputRedirected && Console.KeyAvailable)
            {
                var pressed = Console.ReadKey(intercept: true).Key;

                // Escape pauses instead of ending the run. While aiming it still cancels the aim,
                // so the map key is asked first and only a non-aiming Escape pauses.
                if (pressed == ConsoleKey.Escape)
                {
                    var choice = PauseMenu(campaign);

                    if (choice != PauseChoice.Resume)
                    {
                        // The floor stays standing while the player leaves it. SAVE & QUIT keeps
                        // the run for CONTINUE; ABANDON throws it away. Whether to write the
                        // save is PlayCampaign's call, so the choice is handed up as it is.
                        return choice == PauseChoice.Save ? RunExit.Saved : RunExit.Abandoned;
                    }

                    // The pause lasted real seconds while the floor stood still: the clock has to
                    // start again, or the next tick would hand the hunters the whole pause as
                    // walking time and catch the player the moment they resumed.
                    clock.Restart();
                    continue;
                }

                var command = MapKey(pressed);

                if (command == GameCommand.Interact)
                {
                    if (campaign.CanOpenRoom)
                    {
                        PromptForRoom(campaign);
                    }
                }
                else if (command is { } chosen)
                {
                    GameCommands.Apply(state, chosen);
                }
            }

            // Measured elapsed time, not an assumed frame length: the killer's pace must not
            // depend on how fast the machine draws.
            state.Tick(clock.Elapsed.TotalSeconds);
            clock.Restart();

            // A relic touched during that tick joins the run now, so the next floor is built with
            // it and a save taken here keeps it.
            campaign.Sync();

            // The altar's card freezes the floor the way the Echo does: the relic is read before
            // the hunt resumes, and only once per floor.
            if (state.IsRelicTaken && state.Relic is { } found && relicShownFor != campaign.FloorNumber)
            {
                relicShownFor = campaign.FloorNumber;
                ReadRelic(found);
                clock.Restart();
            }

            // The Echo freezes the floor, so reading it is the only thing that can happen until
            // the player lets go. It is checked after the tick as well as after a keypress: a
            // buffered step can carry the player onto the Echo while no key is being held, and a
            // piped run must close the card rather than freeze on it.
            if (state.IsEchoOpen)
            {
                ReadEcho(campaign);
            }

            Thread.Sleep(8);
        }

        // The last frame stays on screen so the ending can be read next to it.
        renderer.Draw(state);

        return RunExit.Ended;
    }

    /// <summary>
    /// Draws the terminal title screen and reads the menu, so the two frontends open the same way.
    /// </summary>
    /// <param name="hasSave">Whether a saved run is on offer.</param>
    /// <param name="gardenBlooms">The garden grown across finished runs.</param>
    /// <returns>What the player chose.</returns>
    private static TitleAction ShowTitleMenu(bool hasSave, int gardenBlooms)
    {
        while (true)
        {
            DrawTitle(hasSave, gardenBlooms);

            var key = Console.ReadKey(intercept: true).Key;

            switch (key)
            {
                case ConsoleKey.D:
                case ConsoleKey.Enter:
                    return TitleAction.Descend;

                case ConsoleKey.R:
                    if (hasSave)
                    {
                        return TitleAction.Resume;
                    }

                    break;

                case ConsoleKey.T:
                    PrintTutorial();
                    break;

                case ConsoleKey.P:
                    // The practice floor ends by offering the real descent, so the title's answer
                    // is whatever the lesson's last card says.
                    if (Practice())
                    {
                        return TitleAction.Descend;
                    }

                    break;

                case ConsoleKey.C:
                    PrintCodex();
                    break;

                case ConsoleKey.Q:
                case ConsoleKey.Escape:
                    return TitleAction.Leave;
            }
        }
    }

    /// <summary>
    /// Draws the terminal title screen: the wordmark, the garden and the menu entries.
    /// </summary>
    /// <param name="hasSave">Whether CONTINUE is on the menu.</param>
    /// <param name="gardenBlooms">The garden grown across finished runs.</param>
    private static void DrawTitle(bool hasSave, int gardenBlooms)
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
        Console.WriteLine("   pick the blooms. open the arch. do not be caught.");

        if (gardenBlooms > 0)
        {
            Console.WriteLine($"   the garden holds {gardenBlooms} bloom(s).");
        }

        Console.WriteLine();
        Console.WriteLine(
            hasSave
                ? "   [D] descend    [R] continue    [T] tutorial    [P] practice    [C] cards    [Q] leave"
                : "   [D] descend    [T] tutorial    [P] practice    [C] cards    [Q] leave");
    }

    /// <summary>What the title screen was asked to do.</summary>
    private enum TitleAction
    {
        /// <summary>Start a fresh run.</summary>
        Descend,

        /// <summary>Rebuild the saved run.</summary>
        Resume,

        /// <summary>Close the program.</summary>
        Leave,
    }

    /// <summary>
    /// Prints the whole catalog, grouped by rarity, and waits for a key.
    /// </summary>
    /// <remarks>
    /// The same data the window's codex draws: a run's choices are only as readable as the cards
    /// the player has seen, and a rare card offered in a draft is not the moment to learn it.
    /// </remarks>
    private static void PrintCodex()
    {
        if (!Console.IsOutputRedirected)
        {
            Console.Clear();
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 64));
        Console.WriteLine($"  THE CATALOG — {CardCatalog.All.Count} CARDS, RAREST LAST");
        Console.WriteLine(new string('=', 64));

        foreach (var tier in new[] { CardTier.Common, CardTier.Uncommon, CardTier.Rare, CardTier.SuperRare })
        {
            Console.WriteLine();
            Console.WriteLine($"  {TierLabel(tier)}");

            foreach (var card in CardCatalog.All)
            {
                if (card.Tier != tier)
                {
                    continue;
                }

                Console.WriteLine($"    {card.Name}");

                foreach (var row in Wrap(card.FaceText, 58))
                {
                    Console.WriteLine($"      {row}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("  press any key to go back...");
        Console.ReadKey(intercept: true);
    }

    /// <summary>Returns a card tier's printed name, matching the window's labels.</summary>
    /// <param name="tier">The tier.</param>
    private static string TierLabel(CardTier tier) => tier switch
    {
        CardTier.Common => "COMMON",
        CardTier.Uncommon => "UNCOMMON",
        CardTier.Rare => "RARE",
        _ => "SUPER RARE",
    };

    /// <summary>
    /// Plays the tutorial's practice floor in the terminal: walk, pick the blooms, open the arch.
    /// </summary>
    /// <returns><see langword="true"/> when the player asked to begin the real descent.</returns>
    /// <remarks>
    /// The floor is Core's (<see cref="PracticeTutorial"/>), not a script printed here: the walk
    /// is a real timed step, so the terminal and the window teach the same movement rule.
    /// </remarks>
    private static bool Practice()
    {
        var practice = new PracticeTutorial();
        var renderer = new ConsoleRenderer();

        while (true)
        {
            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }

            renderer.Draw(practice.Floor);

            var step = Math.Min(practice.CompletedSteps + 1, PracticeTutorial.StepCount);

            Console.WriteLine();
            Console.WriteLine($"  PRACTICE {step}/{PracticeTutorial.StepCount}");

            foreach (var row in Wrap(practice.Prompt, 58))
            {
                Console.WriteLine($"    {row}");
            }

            Console.WriteLine();

            if (practice.IsComplete)
            {
                Console.WriteLine("  [ENTER] begin the descent    [ESC] back to the title");

                var done = Console.ReadKey(intercept: true).Key;

                if (done is ConsoleKey.Enter or ConsoleKey.Spacebar)
                {
                    return true;
                }

                if (done == ConsoleKey.Escape)
                {
                    return false;
                }

                continue;
            }

            Console.WriteLine("  WASD / arrows: walk    ESC: back to the title");

            var key = Console.ReadKey(intercept: true).Key;

            if (key == ConsoleKey.Escape)
            {
                return false;
            }

            var move = key switch
            {
                ConsoleKey.W or ConsoleKey.UpArrow => GameCommand.MoveNorth,
                ConsoleKey.S or ConsoleKey.DownArrow => GameCommand.MoveSouth,
                ConsoleKey.A or ConsoleKey.LeftArrow => GameCommand.MoveWest,
                ConsoleKey.D or ConsoleKey.RightArrow => GameCommand.MoveEast,
                _ => (GameCommand?)null,
            };

            if (move is { } command)
            {
                GameCommands.Apply(practice.Floor, command);
            }

            // The floor is timed even with nothing hunting: the tick spends the step, so the
            // next tap can move again.
            practice.Floor.Tick(0.3);
            practice.Update();
        }
    }

    /// <summary>
    /// Reads the guided tutorial one page at a time, exactly as the window does.
    /// </summary>
    /// <remarks>
    /// The text comes from <see cref="Tutorial"/> in Core, so the terminal cannot teach a key the
    /// window does not bind. Escape leaves early; the last page returns to the title.
    /// </remarks>
    private static void PrintTutorial()
    {
        for (var index = 0; index < Tutorial.PageCount; index++)
        {
            var page = Tutorial.Page(index);

            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }

            Console.WriteLine();
            Console.WriteLine(new string('=', 64));
            Console.WriteLine($"  TUTORIAL — {page.Caption}  ({index + 1}/{Tutorial.PageCount})");
            Console.WriteLine(new string('-', 64));
            Console.WriteLine();
            Console.WriteLine($"  {page.Title}");
            Console.WriteLine();

            foreach (var line in page.Lines)
            {
                foreach (var row in Wrap(line, 58))
                {
                    Console.WriteLine($"    {row}");
                }

                Console.WriteLine();
            }

            if (page.Keys.Length > 0)
            {
                Console.WriteLine($"  {page.Keys}");
                Console.WriteLine();
            }

            Console.WriteLine(new string('=', 64));
            Console.WriteLine("  ENTER: next    ESC: back to the title");

            while (true)
            {
                var key = Console.ReadKey(intercept: true).Key;

                if (key == ConsoleKey.Escape)
                {
                    return;
                }

                if (key is ConsoleKey.Enter or ConsoleKey.Spacebar)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Shows the pause menu and waits for the player's choice.
    /// </summary>
    /// <returns>The entry the player took.</returns>
    /// <remarks>
    /// The floor is not ticked while this reads, which is what makes it a pause rather than a
    /// menu drawn over a hunt that keeps moving. Escape resumes, matching the window. SAVE &amp;
    /// QUIT and ABANDON RUN both go back to the title; they differ only in whether the run is
    /// there when the player comes back.
    /// </remarks>
    private static PauseChoice PauseMenu(Campaign campaign)
    {
        DrawPauseChoices();

        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;

            if (key is ConsoleKey.C or ConsoleKey.Escape)
            {
                Console.WriteLine("  you walk on.");
                return PauseChoice.Resume;
            }

            if (key == ConsoleKey.S)
            {
                return PauseChoice.Save;
            }

            if (key == ConsoleKey.Q)
            {
                return PauseChoice.Abandon;
            }

            if (key == ConsoleKey.L)
            {
                PrintCodex();
                DrawPauseChoices();
            }

            if (key == ConsoleKey.V)
            {
                PrintRunDetails(campaign);
                DrawPauseChoices();
            }
        }
    }

    /// <summary>
    /// Prints the altar's card: the relic, its family and the promise it keeps for the rest of the
    /// run.
    /// </summary>
    /// <param name="kind">The relic that was taken.</param>
    /// <remarks>
    /// A terminal cannot draw a gem, so the card is a frame and the family is a word. The wait is
    /// skipped when input is redirected, exactly like the Echo, so a piped log still shows what the
    /// altar paid.
    /// </remarks>
    private static void ReadRelic(RelicKind kind)
    {
        var card = RelicCatalog.CardFor(kind);

        Console.WriteLine();
        Console.WriteLine(new string('=', 64));
        Console.WriteLine($"  A RELIC — {card.Name.ToUpperInvariant()}  ({RelicCatalog.FamilyOf(kind).ToString().ToUpperInvariant()})");
        Console.WriteLine(new string('-', 64));

        foreach (var line in Wrap(card.FaceText, 56))
        {
            Console.WriteLine($"    {line}");
        }

        Console.WriteLine();
        Console.WriteLine("  KEPT FOR THE REST OF THE RUN");
        Console.WriteLine(new string('=', 64));

        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("  press any key to walk on...");
            Console.ReadKey(intercept: true);
        }
    }

    /// <summary>
    /// Prints the run details: where the run is, what it has earned, and every card and relic it
    /// is carrying.
    /// </summary>
    /// <param name="campaign">The run to describe.</param>
    private static void PrintRunDetails(Campaign campaign)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 64));
        Console.WriteLine($"  THE RUN — FLOOR {campaign.FloorNumber} OF {campaign.FloorCount}");
        Console.WriteLine(new string('-', 64));
        Console.WriteLine($"  hearts {campaign.Hearts}   blooms {campaign.BloomsPicked}   garden {campaign.GardenCount}");
        Console.WriteLine($"  {campaign.Modifiers.Describe()}");

        var conditions = DescentConditionCatalog.Line(campaign.ActiveConditions);
        if (conditions.Length > 0)
        {
            Console.WriteLine($"  {conditions}");
        }

        if (campaign.CurrentFloor.Gimmick is { Length: > 0 } gimmick)
        {
            Console.WriteLine($"  {gimmick.ToUpperInvariant()}");
        }

        Console.WriteLine(new string('-', 64));

        if (campaign.Modifiers.Cards.Count == 0)
        {
            Console.WriteLine("  nothing yet — the first draft is still ahead");
        }

        for (var index = 0; index < campaign.Modifiers.Cards.Count; index++)
        {
            var held = campaign.Modifiers.Cards[index];
            var relic = RelicCatalog.KindOf(held.Id);
            var tag = relic is { } kind
                ? $"RELIC — {RelicCatalog.FamilyOf(kind).ToString().ToUpperInvariant()}"
                : held.Tier.ToString().ToUpperInvariant();

            Console.WriteLine($"  {index + 1,2}. {held.Name}  [{tag}]");

            foreach (var line in Wrap(held.FaceText, 56))
            {
                Console.WriteLine($"      {line}");
            }
        }

        Console.WriteLine(new string('=', 64));

        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("  press any key to go back...");
            Console.ReadKey(intercept: true);
        }
    }

    /// <summary>
    /// Prints the pause card's heading and keys. Drawn again after the cards, so the player always
    /// comes back to a menu rather than to a wall of card text.
    /// </summary>
    private static void DrawPauseChoices()
    {
        Console.WriteLine();
        Console.WriteLine("PAUSED — the dark waits");
        Console.WriteLine("  [C] continue    [V] run details    [L] cards    [S] save & quit    [Q] abandon run");
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
    /// Translates one key press into a command. Escape is the pause menu and is read by the
    /// caller, so it is not a command here.
    /// </summary>
    private static GameCommand? MapKey(ConsoleKey key) => key switch
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
        ConsoleKey.E => GameCommand.Interact,
        _ => null,
    };

    /// <summary>Returns the sentence printed when a run is lost.</summary>
    internal static string DescribeEnding(GameStatus status) => status switch
    {
        GameStatus.Won => "You escaped. For now.",
        GameStatus.Caught => "The killer took your last heart.",
        _ => "You slip back into the dark.",
    };

    /// <summary>How the player left a floor or a run.</summary>
    private enum RunExit
    {
        /// <summary>The floor or run ended on its own.</summary>
        Ended,

        /// <summary>The player saved the run and went back to the title, where CONTINUE offers it.</summary>
        Saved,

        /// <summary>The player walked away from the run, and its save went with it.</summary>
        Abandoned,
    }

    /// <summary>The pause card's entries.</summary>
    private enum PauseChoice
    {
        /// <summary>Back to the frozen floor, which starts moving again.</summary>
        Resume,

        /// <summary>Write the run and return to the title, keeping it for CONTINUE.</summary>
        Save,

        /// <summary>Return to the title and throw the run away.</summary>
        Abandon,
    }
}
