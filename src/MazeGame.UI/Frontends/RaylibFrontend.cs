using System;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.UI.Audio;
using MazeGame.UI.Input;
using MazeGame.UI.Rendering;
using MazeGame.UI.Rendering.Pixels;
using Raylib_cs;

namespace MazeGame.UI.Frontends;

/// <summary>
/// Plays a whole campaign in a Raylib window: the title, a floor intro, the floor itself, the
/// floor-cleared card and the run summary.
/// </summary>
/// <remarks>
/// <para>
/// The frontend owns the screen state and the timers; the rules stay in Core. A floor is cleared
/// when its <see cref="GameState.Status"/> becomes <see cref="GameStatus.Won"/>, which is progress
/// rather than victory — <see cref="Campaign.TryAdvanceFloor"/> builds the next floor with the
/// hearts the player has left.
/// </para>
/// <para>
/// Starting a run is a call to the injected factory, so a replay is a fresh campaign with full
/// hearts and fresh floors. The renderer only remembers explored cells, which are cleared on every
/// new floor.
/// </para>
/// </remarks>
internal static class RaylibFrontend
{
    /// <summary>Menu entries on the title screen.</summary>
    private const int MenuItemCount = 2;

    private const int DescendItem = 0;

    /// <summary>How long a floor intro card stays up before play begins.</summary>
    private const double IntroSeconds = 1.6;

    /// <summary>
    /// Opens the window and plays campaigns until the player leaves.
    /// </summary>
    /// <param name="config">The settings the window geometry is built from.</param>
    /// <param name="createCampaign">
    /// Builds a fresh run. Called on the first descent and on every new run, so a restart always
    /// starts on floor 1 with full hearts.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a window was opened and the session ran;
    /// <see langword="false"/> when no window is available (for example on a headless
    /// machine), so the caller can fall back to the console frontend.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="config"/> or <paramref name="createCampaign"/> is null.
    /// </exception>
    public static bool TryRun(GameConfig config, Func<Campaign> createCampaign)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(createCampaign);

        var (width, height) = RaylibLayout.WindowSize(config);

        // Warnings only: Raylib's startup banner would otherwise be printed over the ending.
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);

        // Draggable and maximisable: the canvas scales by a whole number to whatever room the frame
        // gives it, so a bigger window is a bigger view rather than a blurrier one.
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(width, height, RaylibLayout.Title);
        Raylib.SetWindowMinSize(RaylibLayout.MinWindowWidth, RaylibLayout.MinWindowHeight);

        if (!Raylib.IsWindowReady())
        {
            Console.WriteLine("No game window available; falling back to the console version.");
            Raylib.CloseWindow();
            return false;
        }

        Raylib.SetTargetFPS(RaylibLayout.TargetFps);

        try
        {
            var lastRun = RunLoop(config, createCampaign);

            // Only report an outcome if there is one: leaving from the title, or mid-run, should
            // not print a summary for a run the player walked away from.
            if (lastRun is not null)
            {
                if (lastRun.Status == CampaignStatus.Lost)
                {
                    Console.WriteLine(ConsoleFrontend.DescribeEnding(GameStatus.Caught));
                }
                else if (lastRun.Status == CampaignStatus.Won)
                {
                    Console.WriteLine(
                        $"You escaped the depths: {lastRun.FloorCount} floors, {lastRun.Hearts} hearts left.");
                }
            }
        }
        finally
        {
            Raylib.CloseWindow();
        }

        return true;
    }

    /// <summary>
    /// Runs frames until the window closes or the player leaves, switching between the title, the
    /// floor intro, play and the outcome cards.
    /// </summary>
    /// <param name="config">The settings the pixel canvas is sized from.</param>
    /// <param name="createCampaign">Builds each new run.</param>
    /// <returns>
    /// The run being played, or <see langword="null"/> when the player left from the title screen
    /// without ever descending.
    /// </returns>
    private static Campaign? RunLoop(GameConfig config, Func<Campaign> createCampaign)
    {
        var renderer = new RaylibRenderer();

        // The floors are drawn from the committed atlases; the vector renderer keeps the title, the
        // campaign cards and the HUD chrome around the canvas.
        using var pixels = new PixelRenderer(config);

        // No device is a silent game, not a broken one: the director is null on a machine with no
        // sound card, and every use of it is a no-op.
        using var audio = AudioDirector.TryCreate();
        Campaign? campaign = null;
        var screen = Screen.Title;
        var selected = DescendItem;
        var introEndsAt = 0.0;

        // Walking is a tap or a hold, and the difference is what keeps one press worth one tile.
        // The rule itself is Core's, so it is tested without a window.
        var walk = new StepRepeat();

        while (!Raylib.WindowShouldClose())
        {
            switch (screen)
            {
                case Screen.Title:
                    switch (ReadTitleChoice(ref selected))
                    {
                        case TitleChoice.Quit:
                            return null;

                        case TitleChoice.Descend:
                            StartRun();
                            break;
                    }

                    break;

                case Screen.FloorIntro:
                    if (Raylib.GetTime() >= introEndsAt || Pressed(KeyboardKey.Enter) ||
                        Pressed(KeyboardKey.KpEnter) || Pressed(KeyboardKey.Space))
                    {
                        screen = Screen.Play;
                    }

                    break;

                case Screen.Play:
                    // Sprint is a hold, so it is sampled every frame; the state decides whether it
                    // is granted (stamina, aiming, a timed floor) rather than the input layer.
                    campaign!.Floor.SetSprinting(
                        Held(KeyboardKey.LeftShift) || Held(KeyboardKey.RightShift));

                    if (TryReadCommand(campaign.Floor, walk, Raylib.GetTime(), out var command) &&
                        !GameCommands.Apply(campaign.Floor, command))
                    {
                        return campaign;
                    }

                    // Measured frame time: the killer's pace must not depend on the frame rate.
                    campaign.Floor.Tick(Raylib.GetFrameTime());

                    // The floor's events are the only thing the sound layer listens to: it never
                    // diffs two frames to guess that a key was lifted.
                    audio?.Handle(campaign.Floor.DrainEvents());
                    audio?.Update(campaign.Floor, Raylib.GetFrameTime());

                    if (campaign.Floor.Status == GameStatus.Caught)
                    {
                        screen = Screen.RunLost;
                    }
                    else if (campaign.IsFloorCleared)
                    {
                        screen = campaign.IsOnLastFloor ? Screen.RunComplete : Screen.FloorCleared;
                    }

                    break;

                case Screen.FloorCleared:
                    if (Pressed(KeyboardKey.Enter) || Pressed(KeyboardKey.KpEnter) || Pressed(KeyboardKey.Space))
                    {
                        // The cleared card is the results beat; the choice itself is the next
                        // screen, so the player reads what the floor cost before picking.
                        screen = Screen.Draft;
                        selected = 0;

                        // The killer is not on this screen; drop to the bed while the cards are up.
                        audio?.SetIntensity(MusicIntensity.Calm);
                    }
                    else if (Pressed(KeyboardKey.Q) || Pressed(KeyboardKey.Escape))
                    {
                        return campaign;
                    }

                    break;

                case Screen.Draft:
                    var previousSelection = selected;

                    if (ReadDraftChoice(campaign!, ref selected, out var take, out var skip))
                    {
                        if (take is not null && !campaign!.TakeCard(take))
                        {
                            break;
                        }

                        if (skip)
                        {
                            campaign!.SkipDraft();
                        }

                        audio?.PlayCardResolved(take is not null);

                        // Only move on if the floor was actually built: a refused advance must
                        // leave the draft on screen rather than loop back to a floor intro for a
                        // floor that is already cleared.
                        if (!campaign!.TryAdvanceFloor())
                        {
                            break;
                        }

                        renderer.Reset();
                        pixels.Reset();
                        StartFloorIntro();
                    }
                    else if (selected != previousSelection)
                    {
                        audio?.PlayCardHover();
                    }
                    else if (Pressed(KeyboardKey.Q) || Pressed(KeyboardKey.Escape))
                    {
                        return campaign;
                    }

                    break;

                case Screen.RunComplete:
                case Screen.RunLost:
                    if (Pressed(KeyboardKey.R) || Pressed(KeyboardKey.Enter) || Pressed(KeyboardKey.KpEnter))
                    {
                        StartRun();
                    }
                    else if (Pressed(KeyboardKey.Q) || Pressed(KeyboardKey.Escape))
                    {
                        return campaign;
                    }

                    break;
            }

            DrawFrame(screen, selected, campaign, renderer, pixels);
        }

        return screen == Screen.Title ? null : campaign;

        void StartRun()
        {
            campaign = createCampaign();
            renderer.Reset();
            pixels.Reset();
            StartFloorIntro();
        }

        void StartFloorIntro()
        {
            screen = Screen.FloorIntro;

            // Every floor is drawn in its own theme's palette, so the atlas is chosen here — the
            // one place both "a run started" and "the next floor was built" pass through. The
            // score is arranged here too, while the intro card holds the screen.
            pixels.SetFloor(campaign!.CurrentFloor);
            pixels.SetTheme(campaign.CurrentFloor.ThemeId);
            audio?.PlayFloor(
                campaign.CurrentFloor.ThemeId,
                campaign.FloorNumber,
                campaign.RunSeed);
            introEndsAt = Raylib.GetTime() + IntroSeconds;
            Raylib.SetWindowTitle(
                $"{RaylibLayout.Title} — Floor {campaign!.FloorNumber}/{campaign.FloorCount}");
        }
    }

    /// <summary>
    /// Draws one frame for the current screen. The game frame is always drawn underneath the
    /// campaign cards, so a cleared floor still shows where the player was standing.
    /// </summary>
    private static void DrawFrame(
        Screen screen,
        int selected,
        Campaign? campaign,
        RaylibRenderer renderer,
        PixelRenderer pixels)
    {
        Raylib.BeginDrawing();

        if (screen == Screen.Title)
        {
            RaylibRenderer.DrawTitle(pixels.Font, selected);
        }
        else
        {
            pixels.Draw(campaign!.Floor);

            switch (screen)
            {
                case Screen.FloorIntro:
                    PixelCards.FloorIntro(
                        pixels.Font,
                        campaign.CurrentFloor,
                        campaign.FloorCount,
                        RaylibRenderer.AccentFor(campaign.CurrentFloor.ThemeId));
                    break;

                case Screen.FloorCleared:
                    PixelCards.FloorCleared(
                        pixels.Font,
                        campaign.CurrentFloor,
                        campaign.Hearts,
                        RaylibRenderer.AccentFor(campaign.CurrentFloor.ThemeId));
                    break;

                case Screen.Draft:
                    PixelCards.Draft(
                        pixels.Font,
                        campaign,
                        selected,
                        RaylibRenderer.AccentFor(campaign.CurrentFloor.ThemeId));
                    break;

                case Screen.RunComplete:
                    PixelCards.RunComplete(
                        pixels.Font,
                        campaign.FloorCount,
                        campaign.Hearts,
                        RaylibRenderer.AccentFor(campaign.CurrentFloor.ThemeId),
                        campaign.CurrentFloor.ThemeId);
                    break;
            }
        }

        Raylib.EndDrawing();
    }

    /// <summary>
    /// Reads one command for this frame. Escape means "cancel" while aiming and "quit"
    /// otherwise, mirroring the console frontend.
    /// </summary>
    /// <param name="state">The floor being played.</param>
    /// <param name="walk">The walking gate's memory across frames.</param>
    /// <param name="now">Seconds since the window opened.</param>
    /// <param name="command">The command to apply, when one was read.</param>
    /// <returns><see langword="true"/> when a command is waiting.</returns>
    private static bool TryReadCommand(GameState state, StepRepeat walk, double now, out GameCommand command)
    {
        var aiming = state.IsAiming;

        if (Pressed(KeyboardKey.Q) || (!aiming && Pressed(KeyboardKey.Escape)))
        {
            command = GameCommand.Quit;
            return true;
        }

        // Walking is a tap or a hold (see ReadWalk). The aim cursor stays press-based: holding a
        // direction there would fly across the map.
        if (ReadDirection(state, walk, now) is { } direction)
        {
            command = direction;
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
    /// Returns the direction the player is asking for this frame, or null when nothing should be
    /// sent.
    /// </summary>
    /// <param name="state">The floor being played, for aiming and its step cooldown.</param>
    /// <param name="walk">The walking gate's memory across frames.</param>
    /// <param name="now">Seconds since the window opened.</param>
    /// <returns>A direction command, or null when the frame sends none.</returns>
    /// <remarks>
    /// While aiming the cursor moves on key presses, not holds, so its step stays one cell per tap.
    /// Walking goes through <see cref="ReadWalk"/>, which is where tap-versus-hold is decided.
    /// </remarks>
    private static GameCommand? ReadDirection(GameState state, StepRepeat walk, double now)
    {
        if (state.IsAiming)
        {
            walk.Clear();

            return DirectionOf(Pressed, KeyboardKey.W, KeyboardKey.Up) ??
                   DirectionOf(Pressed, KeyboardKey.S, KeyboardKey.Down) ??
                   DirectionOf(Pressed, KeyboardKey.A, KeyboardKey.Left) ??
                   DirectionOf(Pressed, KeyboardKey.D, KeyboardKey.Right);
        }

        return ReadWalk(state, walk, now);
    }

    /// <summary>
    /// Reads walking: a tap is exactly one tile, a hold repeats, and sprint only makes the repeat
    /// faster.
    /// </summary>
    /// <param name="state">The floor being played, for its step cooldown.</param>
    /// <param name="walk">The gate's memory: which direction is down and for how long.</param>
    /// <param name="now">Seconds since the window opened.</param>
    /// <returns>A direction to send this frame, or null when nothing should be sent.</returns>
    /// <remarks>
    /// <para>
    /// The old version sent the held direction every frame, so a tap that lasted a couple of
    /// frames left a press in the buffer and could move a second tile after the player had let go.
    /// A fresh press now sends exactly one command — which the state buffers if the last step is
    /// still cooling down, so a tap is worth one tile even when pressed early.
    /// </para>
    /// <para>
    /// Repeats only start after <see cref="StepRepeat.HoldDelaySeconds"/> and only once the previous step has
    /// finished, so holding walks at the game's own cadence rather than at the frame rate, and a
    /// deliberately slow tap is still a tap. Sprint is not read here: holding Shift shortens the
    /// step in Core, so the same repeat simply runs faster until the stamina bar is empty.
    /// </para>
    /// </remarks>
    private static GameCommand? ReadWalk(GameState state, StepRepeat walk, double now)
    {
        var held = HeldDirection();

        if (walk.Next(held, state.IsPlayerStepping, now) is not { } direction)
        {
            return null;
        }

        return ToCommand(direction);
    }

    /// <summary>
    /// Returns the direction whose key is down, or null when none is.
    /// </summary>
    private static Direction? HeldDirection()
    {
        if (Held(KeyboardKey.W) || Held(KeyboardKey.Up))
        {
            return Direction.North;
        }

        if (Held(KeyboardKey.S) || Held(KeyboardKey.Down))
        {
            return Direction.South;
        }

        if (Held(KeyboardKey.A) || Held(KeyboardKey.Left))
        {
            return Direction.West;
        }

        return Held(KeyboardKey.D) || Held(KeyboardKey.Right) ? Direction.East : null;
    }

    /// <summary>
    /// Returns the movement command for a direction.
    /// </summary>
    private static GameCommand ToCommand(Direction direction) => direction switch
    {
        Direction.North => GameCommand.MoveNorth,
        Direction.South => GameCommand.MoveSouth,
        Direction.West => GameCommand.MoveWest,
        _ => GameCommand.MoveEast,
    };

    /// <summary>
    /// Returns the movement command for a key pair, or null when neither key is down.
    /// </summary>
    private static GameCommand? DirectionOf(
        Func<KeyboardKey, bool> isDown,
        KeyboardKey primary,
        KeyboardKey alternate)
    {
        if (!isDown(primary) && !isDown(alternate))
        {
            return null;
        }

        return primary switch
        {
            KeyboardKey.W => GameCommand.MoveNorth,
            KeyboardKey.S => GameCommand.MoveSouth,
            KeyboardKey.A => GameCommand.MoveWest,
            _ => GameCommand.MoveEast,
        };
    }

    /// <summary>
    /// Reads one draft command for this frame: move the highlight, take a card, or skip.
    /// </summary>
    /// <param name="campaign">The run, whose open draft is being chosen from.</param>
    /// <param name="selected">The highlighted card, moved in place.</param>
    /// <param name="take">The card to take this frame, or null when none was chosen.</param>
    /// <param name="skip">Whether the player chose to skip for a heart.</param>
    /// <returns><see langword="true"/> when the draft is resolved by this frame's input.</returns>
    private static bool ReadDraftChoice(Campaign campaign, ref int selected, out Card? take, out bool skip)
    {
        take = null;
        skip = false;

        var offers = campaign.Draft?.Offers ?? Array.Empty<Card>();
        if (offers.Count == 0)
        {
            if (Pressed(KeyboardKey.S) || Pressed(KeyboardKey.Enter) || Pressed(KeyboardKey.Space))
            {
                skip = true;
                return true;
            }

            return false;
        }

        // Four or more cards are laid out in two columns, so down has to move by a row.
        var columns = offers.Count <= 3 ? offers.Count : 2;

        if (Pressed(KeyboardKey.A) || Pressed(KeyboardKey.Left))
        {
            selected = (selected + offers.Count - 1) % offers.Count;
        }
        else if (Pressed(KeyboardKey.D) || Pressed(KeyboardKey.Right))
        {
            selected = (selected + 1) % offers.Count;
        }
        else if (Pressed(KeyboardKey.W) || Pressed(KeyboardKey.Up))
        {
            selected = Math.Max(0, selected - columns);
        }
        else if (Pressed(KeyboardKey.S) || Pressed(KeyboardKey.Down))
        {
            // S is the skip key, so it must not also move the highlight; the arrows and A/D do
            // the moving, while S always means "take the heart".
            if (Pressed(KeyboardKey.S) && !Pressed(KeyboardKey.Down))
            {
                skip = true;
                return true;
            }

            selected = Math.Min(offers.Count - 1, selected + columns);
        }

        if (Pressed(KeyboardKey.Enter) || Pressed(KeyboardKey.KpEnter) || Pressed(KeyboardKey.Space))
        {
            take = offers[Math.Clamp(selected, 0, offers.Count - 1)];
            return true;
        }

        // The number keys take a card directly, which is faster than moving the highlight.
        for (var index = 0; index < offers.Count && index < 9; index++)
        {
            if (Pressed((KeyboardKey)((int)KeyboardKey.One + index)))
            {
                take = offers[index];
                return true;
            }
        }

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
    /// Tests a key press including the operating system's auto-repeat. Used for the aim cursor,
    /// the menus and the draft, where one tap is one action.
    /// </summary>
    private static bool Pressed(KeyboardKey key) =>
        Raylib.IsKeyPressed(key) || Raylib.IsKeyPressedRepeat(key);

    /// <summary>Tests whether a key is being held, which is how walking and sprint are read.</summary>
    private static bool Held(KeyboardKey key) => Raylib.IsKeyDown(key);

    /// <summary>Which screen the window is showing.</summary>
    private enum Screen
    {
        /// <summary>The title menu.</summary>
        Title,

        /// <summary>A floor card, before its first frame of play.</summary>
        FloorIntro,

        /// <summary>A floor being played.</summary>
        Play,

        /// <summary>The card shown after a floor that is not the last one.</summary>
        FloorCleared,

        /// <summary>The choice between two floors.</summary>
        Draft,

        /// <summary>The run summary after the final floor.</summary>
        RunComplete,

        /// <summary>The run summary after the last heart was spent.</summary>
        RunLost,
    }

    /// <summary>What the player asked for on the title screen.</summary>
    private enum TitleChoice
    {
        /// <summary>Nothing yet: keep showing the title.</summary>
        None,

        /// <summary>Start a run.</summary>
        Descend,

        /// <summary>Close the window.</summary>
        Quit,
    }
}
