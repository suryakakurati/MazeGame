using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.UI.Rendering.Pixels;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the interactive tutorial's floor: it asks for one thing at a time — walk, blink, the
/// blooms, sprint, the arch — the arch stays shut until both blooms are picked, and nothing on the
/// floor can catch the player while they learn.
/// </summary>
/// <remarks>
/// The steps are derived from the floor rather than reported by it, so these tests walk the
/// chamber the way the tutorial asks and assert the prompt at each turn.
/// </remarks>
public sealed class PracticeTutorialTests
{
    [Fact]
    public void ThePracticeFloor_StartsByAskingForAWalk()
    {
        var practice = new PracticeTutorial();

        Assert.Equal(0, practice.CompletedSteps);
        Assert.False(practice.IsComplete);
        Assert.Equal(PracticeTutorial.Steps[0], practice.Prompt);
        Assert.Equal(2, practice.Floor.Keys.Count);
        Assert.Equal(GameStatus.InProgress, practice.Floor.Status);
    }

    [Fact]
    public void Walking_ThenBlinking_AdvancesOntoTheFirstBloom()
    {
        var practice = new PracticeTutorial();

        Move(practice, Direction.East);   // onto the arch, facing the wall

        Assert.Equal(1, practice.CompletedSteps);
        Assert.Equal(PracticeTutorial.Steps[1], practice.Prompt);

        // The lesson's wall: the step east is refused until the player blinks through it.
        Assert.False(practice.Floor.TryMove(Direction.East));

        Blink(practice);

        // The blink lands on the first bloom, so it passes the blink step and the first bloom.
        Assert.Equal(3, practice.CompletedSteps);
        Assert.Equal(PracticeTutorial.Steps[3], practice.Prompt);
        Assert.Equal(1, practice.Floor.KeysCollected);
    }

    [Fact]
    public void TheArch_StaysShutUntilBothBloomsArePicked()
    {
        var practice = new PracticeTutorial();

        // The arch is the cell next to the start: walking onto it early is allowed and does
        // nothing, which is the lesson the first page states.
        Move(practice, Direction.East);

        Assert.Equal(GameStatus.InProgress, practice.Floor.Status);
        Assert.False(practice.IsComplete);
    }

    [Fact]
    public void TheSprintLesson_WaitsForSprintingSteps()
    {
        var practice = new PracticeTutorial();

        Move(practice, Direction.East);   // the arch, locked
        Blink(practice);                  // through the wall onto the first bloom
        Move(practice, Direction.East);   // second bloom: the arch unlocks

        Assert.Equal(4, practice.CompletedSteps);
        Assert.Equal(PracticeTutorial.Steps[4], practice.Prompt);

        // Plain walking does not count as sprinting.
        Move(practice, Direction.South);
        Move(practice, Direction.North);

        Assert.Equal(4, practice.CompletedSteps);

        for (var step = 0; step < PracticeTutorial.SprintStepsNeeded; step++)
        {
            Sprint(practice, step % 2 == 0 ? Direction.South : Direction.North);
        }

        Assert.Equal(5, practice.CompletedSteps);
        Assert.Equal(PracticeTutorial.Steps[5], practice.Prompt);
    }

    [Fact]
    public void EveryLesson_ThenTheArch_FinishesThePractice()
    {
        var practice = new PracticeTutorial();

        Move(practice, Direction.East);   // the arch, locked
        Blink(practice);                  // first bloom
        Move(practice, Direction.East);   // second bloom: the arch unlocks

        Assert.Equal(0, practice.Floor.KeysRemaining);
        Assert.Equal(GameStatus.InProgress, practice.Floor.Status);

        Sprint(practice, Direction.South);
        Sprint(practice, Direction.North);
        Sprint(practice, Direction.South);
        Move(practice, Direction.North);  // back to the top row
        Move(practice, Direction.West);   // back along the corridor
        Move(practice, Direction.West);   // through the breach and onto the arch

        Assert.True(practice.IsComplete);
        Assert.Equal(PracticeTutorial.StepCount, practice.CompletedSteps);
        Assert.Equal(PracticeTutorial.FinishPrompt, practice.Prompt);
        Assert.Equal(GameStatus.Won, practice.Floor.Status);
    }

    [Fact]
    public void ReachingTheOpenArch_FinishesThePractice_EvenWithALessonSkipped()
    {
        var practice = new PracticeTutorial();

        Move(practice, Direction.East);   // the arch
        Blink(practice);                  // first bloom
        Move(practice, Direction.East);   // second bloom
        Move(practice, Direction.West);   // no sprint: straight back
        Move(practice, Direction.West);   // onto the open arch

        // The floor is won, so the lesson cannot keep asking for a sprint it can no longer take.
        Assert.Equal(GameStatus.Won, practice.Floor.Status);
        Assert.True(practice.IsComplete);
        Assert.Equal(PracticeTutorial.FinishPrompt, practice.Prompt);
    }

    [Fact]
    public void ThePracticeFloor_OpensOnALitRoomRatherThanABlankCanvas()
    {
        var practice = new PracticeTutorial();
        var floor = practice.Floor;

        // The window draws a floor through a viewport, so the lesson has to be a room: the old
        // one-row corridor was a thin line in a dark canvas and read as a black screen. Nine cells
        // across at lantern radius six fills the frame the way a real floor does, and the first
        // frame is already lit.
        Assert.Equal(PracticeTutorial.ChamberSize, floor.Maze.Rows);
        Assert.Equal(PracticeTutorial.ChamberSize, floor.Maze.Columns);

        // The chamber has to fill most of the viewport the window draws it through, or the lesson
        // is a small lit island in a dark canvas however many cells are lit.
        var viewport = PixelLayout.ViewCells(floor.Config.VisionRadius);
        Assert.True(
            floor.Maze.Columns * 2 > viewport,
            $"The practice chamber is {floor.Maze.Columns} cells wide in a {viewport}-cell viewport.");

        var lit = 0;

        for (var row = 0; row < floor.Maze.Rows; row++)
        {
            for (var column = 0; column < floor.Maze.Columns; column++)
            {
                if (floor.IsVisible(floor.Maze.GetCell(row, column)))
                {
                    lit++;
                }
            }
        }

        Assert.True(lit >= 20, $"Only {lit} cells were lit on the practice floor's first frame.");
    }

    [Fact]
    public void ThePracticeHunter_NeverTakesAStep()
    {
        var practice = new PracticeTutorial();
        var hunter = practice.Floor.Enemies[0];
        var cell = hunter.Cell;

        practice.Floor.Tick(30.0);

        Assert.Equal(cell, practice.Floor.Enemies[0].Cell);
        Assert.Equal(GameStatus.InProgress, practice.Floor.Status);
    }

    /// <summary>Walks one tile in <paramref name="direction"/> and lets the tutorial catch up.</summary>
    private static void Move(PracticeTutorial practice, Direction direction)
    {
        Assert.True(practice.Floor.TryMove(direction));

        // A step is timed; this is longer than the practice floor's own step so the walk lands.
        practice.Floor.Tick(0.3);
        practice.Update();
    }

    /// <summary>Takes one sprinting step, then lets go of sprint and lets the step land.</summary>
    private static void Sprint(PracticeTutorial practice, Direction direction)
    {
        practice.Floor.SetSprinting(true);
        Assert.True(practice.Floor.IsSprinting);
        Assert.True(practice.Floor.TryMove(direction));

        // The window checks the lesson on the frame the step starts, while Shift is still down.
        practice.Update();
        practice.Floor.SetSprinting(false);

        // Long enough for the step to land and the stamina bar to refill before the next one.
        practice.Floor.Tick(2.0);
        practice.Update();
    }

    /// <summary>Blinks through the wall the player is facing and lets the tutorial catch up.</summary>
    private static void Blink(PracticeTutorial practice)
    {
        Assert.True(practice.Floor.TryBlink());
        practice.Floor.Tick(0.3);
        practice.Update();
    }
}
