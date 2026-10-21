using MazeGame.Core.Game;
using MazeGame.Core.Maze;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the tap-versus-hold gate: one press is one tile, a hold walks, and a slow tap cannot
/// leave a second step behind.
/// </summary>
/// <remarks>
/// The gate has no maze and no window, so every test drives it the way the frontend does: one call
/// per frame, with a direction (or null) and whether the previous step is still running.
/// </remarks>
public sealed class StepRepeatTests
{
    private const double Frame = 1.0 / 60.0;

    [Fact]
    public void ATap_SendsTheDirectionOnce()
    {
        var repeat = new StepRepeat();

        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: 0));

        // Held for only a few frames — well short of the delay — so nothing else is sent.
        var now = 0.0;
        for (var frame = 1; frame <= 5; frame++)
        {
            now += Frame;
            Assert.Null(repeat.Next(Direction.East, stepping: false, now));
        }
    }

    [Fact]
    public void ARelease_ClearsTheHold()
    {
        var repeat = new StepRepeat();

        repeat.Next(Direction.East, stepping: false, now: 0);
        Assert.Null(repeat.Next(null, stepping: false, now: Frame));

        Assert.Null(repeat.Held);
    }

    [Fact]
    public void PressingAgain_IsAlwaysOneMoreStep()
    {
        // Two taps are two steps, however quickly they follow each other: the release resets the
        // gate, so the second press is fresh rather than a repeat. The key must be seen up for at
        // least one frame, which is what a real release does.
        var repeat = new StepRepeat();

        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: 0));
        Assert.Null(repeat.Next(null, stepping: false, now: Frame));
        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: Frame * 2));
    }

    [Fact]
    public void KeepingTheKeyDown_IsAHoldRatherThanASecondTap()
    {
        // The other half of the same rule: with no release in between, the direction is still the
        // one being watched, so it waits out the delay instead of stepping again.
        var repeat = new StepRepeat();

        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: 0));
        Assert.Null(repeat.Next(Direction.East, stepping: false, now: Frame * 2));
    }

    [Fact]
    public void AHeldDirection_RepeatsOnlyAfterTheDelay()
    {
        var repeat = new StepRepeat();
        repeat.Next(Direction.East, stepping: false, now: 0);

        var now = 0.0;
        while (now + Frame < StepRepeat.HoldDelaySeconds)
        {
            now += Frame;
            Assert.Null(repeat.Next(Direction.East, stepping: false, now));
        }

        now = StepRepeat.HoldDelaySeconds;
        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now));

        // Once repeating, every later frame is another step, which is what the step cooldown
        // then paces.
        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: now + Frame));
    }

    [Fact]
    public void ASlowTap_WhileStepping_SendsNoSecondStep()
    {
        // The bug this gate exists for: the key is down for longer than the delay, but the step it
        // caused is still running, so the buffered press must not survive the release.
        var repeat = new StepRepeat();
        repeat.Next(Direction.East, stepping: false, now: 0);

        Assert.Null(repeat.Next(Direction.East, stepping: true, now: StepRepeat.HoldDelaySeconds * 2));
        Assert.Null(repeat.Next(null, stepping: true, now: StepRepeat.HoldDelaySeconds * 2 + Frame));
    }

    [Fact]
    public void AChangeOfDirection_IsFreshMidHold()
    {
        var repeat = new StepRepeat();
        repeat.Next(Direction.East, stepping: false, now: 0);
        repeat.Next(Direction.East, stepping: false, now: StepRepeat.HoldDelaySeconds * 2);

        // A new direction must not wait out the old direction's delay.
        Assert.Equal<Direction?>(Direction.North, repeat.Next(Direction.North, stepping: false, now: StepRepeat.HoldDelaySeconds * 2 + Frame));
    }

    [Fact]
    public void AClockGoingBackwards_DoesNotLookLikeALongHold()
    {
        var repeat = new StepRepeat();
        repeat.Next(Direction.East, stepping: false, now: 10);

        Assert.Null(repeat.Next(Direction.East, stepping: false, now: 0));
        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: StepRepeat.HoldDelaySeconds));
    }

    [Fact]
    public void Clear_ForgetsTheHold()
    {
        var repeat = new StepRepeat();
        repeat.Next(Direction.East, stepping: false, now: 0);

        repeat.Clear();

        Assert.Null(repeat.Held);
        Assert.Equal<Direction?>(Direction.East, repeat.Next(Direction.East, stepping: false, now: StepRepeat.HoldDelaySeconds));
    }
}
