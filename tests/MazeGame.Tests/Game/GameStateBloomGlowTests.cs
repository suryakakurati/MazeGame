using System;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the bloom-glow card: a catch lights every bloom on the floor for a couple of seconds,
/// each one only on its own tile, and every further copy widens the halo around them.
/// </summary>
/// <remarks>
/// The level is a 1x12 corridor with the torch cut to one step, so all three blooms sit in the dark
/// before the catch. The catch is the corridor's killer walking the length of it: the reveal is a
/// rule of being caught, so the test has to be caught.
/// </remarks>
public sealed class GameStateBloomGlowTests
{
    private const int Length = 12;

    [Fact]
    public void BeforeACatch_TheBloomsAreDark()
    {
        var state = GlowGame(reach: 1);

        Assert.False(state.IsBloomGlowActive);
        Assert.DoesNotContain(state.Keys, bloom => state.IsVisible(bloom));
    }

    [Fact]
    public void ACatch_LightsEveryBloomOnTheFloor()
    {
        var state = GlowGame(reach: 1);

        TickUntil(state, () => state.IsBloomGlowActive);

        // Three blooms, two to four steps away: all of them, not just the near one.
        Assert.True(state.IsVisible(At(state, 2)));
        Assert.True(state.IsVisible(At(state, 3)));
        Assert.True(state.IsVisible(At(state, 4)));
    }

    [Fact]
    public void TheGlow_RevealsOnlyTheBloomsOwnTile()
    {
        var state = GlowGame(reach: 1);
        TickUntil(state, () => state.IsBloomGlowActive);

        // The bloom on the far end of the row is lit; the empty cell beside it is not, which is
        // what "one tile" means: the flower is revealed, the corridor around it is not.
        Assert.True(state.IsVisible(At(state, 4)));
        Assert.False(state.IsVisible(At(state, 5)));
        Assert.False(state.IsVisible(At(state, 6)));
    }

    [Fact]
    public void ASecondCopy_LightsTheCellsAroundEachBloom()
    {
        var state = GlowGame(reach: 2);

        TickUntil(state, () => state.IsBloomGlowActive);

        // One step of halo around each bloom, and still no further.
        Assert.True(state.IsVisible(At(state, 4)));
        Assert.True(state.IsVisible(At(state, 5)));
        Assert.False(state.IsVisible(At(state, 6)));
    }

    [Fact]
    public void TheFirstCopy_ReachesEveryBloomAndNoFurther()
    {
        var state = GlowGame(reach: 1);

        TickUntil(state, () => state.IsBloomGlowActive);

        // Every bloom is visible, so the reveal is not a radius around the player; and the halo is
        // zero cells, so it is not a map reveal either.
        Assert.All(state.Keys, bloom => Assert.True(state.IsVisible(bloom)));
        Assert.False(state.IsVisible(At(state, 8)));
    }

    [Fact]
    public void TheGlow_FadesWhenItsSecondsRunOut()
    {
        var state = GlowGame(reach: 1);
        TickUntil(state, () => state.IsBloomGlowActive);
        var bloom = At(state, 4);

        state.Tick(2.1);

        Assert.False(state.IsBloomGlowActive);
        Assert.Equal(0.0, state.BloomGlowSecondsRemaining);
        Assert.False(state.IsVisible(bloom));
    }

    [Fact]
    public void EveryFurtherCopy_WidensTheHaloByAStep()
    {
        var card = CardCatalog.ById("ashen-bloom");
        Assert.NotNull(card);

        var one = RunModifiers.None.With(card!);
        var two = one.With(card!);
        var three = two.With(card!);

        Assert.Equal(1, one.BloomGlowReach);
        Assert.Equal(2, two.BloomGlowReach);
        Assert.Equal(3, three.BloomGlowReach);
    }

    /// <summary>
    /// A corridor whose torch reaches one step and whose killer walks the length of it, with the
    /// bloom glow set to <paramref name="reach"/>.
    /// </summary>
    private static GameState GlowGame(int reach) =>
        TestLevels.GameOver(
            TestLevels.Corridor(Length),
            new GameConfig
            {
                Rows = 1,
                Columns = Length,
                KeyCount = 3,
                EnemyStepSeconds = 1.0,
                PlayerStepSeconds = 0.1,
                VisionRadius = 1,
                NearSightRadius = 1,
                EnemyAuraRadius = 0,
                EnemyAuraDistance = 0,
                StartingHearts = 3,
                MaxHearts = 3,
                BloomGlowReach = reach,
                BloomGlowSeconds = 2.0,
                Seed = null,
            });

    /// <summary>Returns the corridor cell at <paramref name="column"/>.</summary>
    private static Cell At(GameState state, int column) => state.Maze.GetCell(0, column);

    /// <summary>Ticks one second at a time until <paramref name="done"/> or the budget runs out.</summary>
    private static void TickUntil(GameState state, Func<bool> done, int maxTicks = 40)
    {
        for (var tick = 0; tick < maxTicks && !done(); tick++)
        {
            state.Tick(1.0);
        }

        Assert.True(done(), "the expected moment never happened");
    }
}
