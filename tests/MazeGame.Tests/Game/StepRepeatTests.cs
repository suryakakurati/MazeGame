using MazeGame.Core.Game;
using MazeGame.Core.Maze;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the tap-versus-hold gate: one press is one tile, a hold walks as soon as the step ends,
/// and a key released mid-step cannot leave a second step behind.
/// </summary>
/// <remarks>
/// The gate has no maze and no window, so every test drives it the way the frontend does: one call
/// per frame, with a direction (or null), whether the previous step is running, and how long that
/// step took.
/// </remarks>
public sealed class StepRepeatTests
{
    private const double Frame = 1.0 / 60.0;

    /// <summary>A plausible floor cadence, longer than one frame.</summary>
    private const double Step = 0.14;

    [Fact]
    public void ATap_SendsTheDirectionOnce()
    {
        var repeat = new StepRepeat();

        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: 0, Step));

        // Released well before the step could finish, so nothing else is ever sent.
        var now = 0.0;
        for (var frame = 1; frame <= 5; frame++)
        {
            now += Frame;
            Assert.Null(repeat.Next(null, stepping: true, now, Step));
        }
    }

    [Fact]
    public void ARelease_ClearsTheHold()
    {
        var repeat = new StepRepeat();

        repeat.Next(Direction.East, stepping: false, now: 0, Step);
        Assert.Null(repeat.Next(null, stepping: false, now: Frame, Step));

        Assert.Null(repeat.Held);
    }

    [Fact]
    public void PressingAgain_IsAlwaysOneMoreStep()
    {
        // Two taps are two steps, however quickly they follow each other: the release resets the
        // gate, so the second press is fresh rather than a repeat. The key must be seen up for at
        // least one frame, which is what a real release does.
        var repeat = new StepRepeat();

        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: 0, Step));
        Assert.Null(repeat.Next(null, stepping: false, now: Frame, Step));
        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: Frame * 2, Step));
    }

    [Fact]
    public void HoldingWalks_AsSoonAsTheStepEnds()
    {
        // The complaint this replaces: holding used to wait out a fixed delay on top of the step,
        // so crossing a room was a series of taps with a stutter. The repeat is due the moment the
        // step that the press caused finishes.
        var repeat = new StepRepeat();
        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: 0, Step));

        var now = 0.0;
        while (now + Frame < Step)
        {
            now += Frame;
            Assert.Null(repeat.Next(Direction.East, stepping: true, now, Step));
        }

        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: Step, Step));

        // And the next step's repeat comes at the same cadence, so a hold is a steady walk.
        Assert.Null(repeat.Next(Direction.East, stepping: true, now: Step + (Frame * 2), Step));
        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: Step * 2, Step));
    }

    [Fact]
    public void KeepingTheKeyDown_WaitsForTheStepRatherThanTheFrame()
    {
        var repeat = new StepRepeat();
        repeat.Next(Direction.East, stepping: false, now: 0, Step);

        // The key is down, but the step is still running: nothing is sent, at any frame rate.
        for (var frame = 1; frame * Frame < Step; frame++)
        {
            Assert.Null(repeat.Next(Direction.East, stepping: true, now: frame * Frame, Step));
        }
    }

    [Fact]
    public void ASlowTap_ReleasingDuringTheStep_SendsNoSecondStep()
    {
        // The bug the gate exists for: the key is held past the end of the step, but it is released
        // before a repeat is due, so the press cannot leak into the buffer and move a second tile.
        var repeat = new StepRepeat();
        repeat.Next(Direction.East, stepping: false, now: 0, Step);

        Assert.Null(repeat.Next(Direction.East, stepping: true, now: Step * 0.9, Step));
        Assert.Null(repeat.Next(null, stepping: false, now: Step * 1.5, Step));
    }

    [Fact]
    public void AChangeOfDirection_IsFreshMidHold()
    {
        var repeat = new StepRepeat();
        repeat.Next(Direction.East, stepping: false, now: 0, Step);
        repeat.Next(Direction.East, stepping: false, now: Step, Step);

        // A new direction must not wait for the old direction's cadence.
        Assert.Equal<Direction?>(Direction.North, repeat.Next(Direction.North, stepping: false, now: Step + Frame, Step));
    }

    [Fact]
    public void AClockGoingBackwards_DoesNotLookLikeALongHold()
    {
        var repeat = new StepRepeat();
        repeat.Next(Direction.East, stepping: false, now: 10, Step);

        Assert.Null(repeat.Next(Direction.East, stepping: false, now: 0, Step));
        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: Step, Step));
    }

    [Fact]
    public void AnInstantStepFloor_CannotWalkAtTheFrameRate()
    {
        // A debug floor with no step time reports a zero step. The gate still needs its own floor
        // between repeats, or holding a direction would move one cell per frame.
        var repeat = new StepRepeat();
        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: 0, stepSeconds: 0));

        Assert.Null(repeat.Next(Direction.East, stepping: false, now: Frame, stepSeconds: 0));
        Assert.Equal<Direction?>(
            Direction.East,
            repeat.Next(Direction.East, stepping: false, now: StepRepeat.MinRepeatSeconds, stepSeconds: 0));
    }

    [Fact]
    public void Clear_ForgetsTheHold()
    {
        var repeat = new StepRepeat();
        repeat.Next(Direction.East, stepping: false, now: 0, Step);

        repeat.Clear();

        Assert.Null(repeat.Held);
        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: Step, Step));
    }
}
