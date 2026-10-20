using System;
using MazeGame.Core.Game;
using MazeGame.UI.Input;
using MazeGame.UI.Rendering;
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
            var lastRun = RunLoop(createCampaign);

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
    /// <param name="createCampaign">Builds each new run.</param>
    /// <returns>
    /// The run being played, or <see langword="null"/> when the player left from the title screen
    /// without ever descending.
    /// </returns>
    private static Campaign? RunLoop(Func<Campaign> createCampaign)
    {
        var renderer = new RaylibRenderer();
        Campaign? campaign = null;
        var screen = Screen.Title;
        var selected = DescendItem;
        var introEndsAt = 0.0;

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
                    if (TryReadCommand(campaign!.Floor.IsAiming, out var command) &&
                        !GameCommands.Apply(campaign.Floor, command))
                    {
                        return campaign;
                    }

                    // Measured frame time: the killer's pace must not depend on the frame rate.
                    campaign.Floor.Tick(Raylib.GetFrameTime());

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
                        campaign!.TryAdvanceFloor();
                        renderer.Reset();
                        StartFloorIntro();
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

            DrawFrame(screen, selected, campaign, renderer);
        }

        return screen == Screen.Title ? null : campaign;

        void StartRun()
        {
            campaign = createCampaign();
            renderer.Reset();
            StartFloorIntro();
        }

        void StartFloorIntro()
        {
            screen = Screen.FloorIntro;
            introEndsAt = Raylib.GetTime() + IntroSeconds;
            Raylib.SetWindowTitle(
                $"{RaylibLayout.Title} — Floor {campaign!.FloorNumber}/{campaign.FloorCount}");
        }
    }

    /// <summary>
    /// Draws one frame for the current screen. The game frame is always drawn underneath the
    /// campaign cards, so a cleared floor still shows where the player was standing.
    /// </summary>
    private static void DrawFrame(Screen screen, int selected, Campaign? campaign, RaylibRenderer renderer)
    {
        Raylib.BeginDrawing();

        if (screen == Screen.Title)
        {
            renderer.DrawTitle(selected);
        }
        else
        {
            renderer.Draw(campaign!.Floor);

            switch (screen)
            {
                case Screen.FloorIntro:
                    renderer.DrawFloorIntro(campaign.CurrentFloor, campaign.FloorCount);
                    break;

                case Screen.FloorCleared:
                    renderer.DrawFloorCleared(campaign.CurrentFloor, campaign.Hearts);
                    break;

                case Screen.RunComplete:
                    renderer.DrawRunComplete(campaign.FloorCount, campaign.Hearts);
                    break;
            }
        }

        Raylib.EndDrawing();
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
