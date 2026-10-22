using System;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the bloom-glow card: a catch lights the blooms near the player for a couple of
/// seconds, the first copy reaches exactly one tile, and every further copy widens the reach.
/// </summary>
/// <remarks>
/// The level is a 1x12 corridor with the torch cut to one step, so the blooms behind the arch are
/// genuinely dark until the glow lights them. The catch is the corridor's killer walking the length
/// of it, not a hand-placed event: the reveal is a rule of being caught, so the test has to be
/// caught.
/// </remarks>
public sealed class GameStateBloomGlowTests
{
    private const int Length = 12;

    [Fact]
    public void BeforeACatch_TheBloomsAreDark()
    {
        var state = GlowGame(radius: 2);

        Assert.False(state.IsBloomGlowActive);
        Assert.False(state.IsVisible(Bloom(state, 2)));
    }

    [Fact]
    public void ACatch_LightsTheBloomsInsideTheCardsReach()
    {
        var state = GlowGame(radius: 2);

        TickUntil(state, () => state.IsBloomGlowActive);

        // Two steps away: inside the second copy's reach, past the torch.
        Assert.True(state.IsVisible(Bloom(state, 2)));

        // Three steps away is still dark, so the glow has an edge rather than lighting the floor.
        Assert.False(state.IsVisible(Bloom(state, 3)));
    }

    [Fact]
    public void TheFirstCopy_ReachesOneTileOnly()
    {
        var state = GlowGame(radius: 1);

        TickUntil(state, () => state.IsBloomGlowActive);

        // Every bloom on this floor is at least two steps away, so a one-step glow lights none.
        Assert.DoesNotContain(state.Keys, key => state.IsVisible(key));
    }

    [Fact]
    public void TheGlow_FadesWhenItsSecondsRunOut()
    {
        var state = GlowGame(radius: 2);
        TickUntil(state, () => state.IsBloomGlowActive);
        var bloom = Bloom(state, 2);

        state.Tick(2.1);

        Assert.False(state.IsBloomGlowActive);
        Assert.Equal(0.0, state.BloomGlowSecondsRemaining);
        Assert.False(state.IsVisible(bloom));
    }

    [Fact]
    public void EveryFurtherCopy_WidensTheReachByAStep()
    {
        var card = CardCatalog.ById("ashen-bloom");
        Assert.NotNull(card);

        var one = RunModifiers.None.With(card!);
        var two = one.With(card!);
        var three = two.With(card!);

        Assert.Equal(1, one.BloomGlowRadius);
        Assert.Equal(2, two.BloomGlowRadius);
        Assert.Equal(3, three.BloomGlowRadius);
    }

    /// <summary>
    /// A corridor whose torch reaches one step and whose killer walks the length of it, with the
    /// bloom glow set to <paramref name="radius"/>.
    /// </summary>
    private static GameState GlowGame(int radius) =>
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
                BloomGlowRadius = radius,
                BloomGlowSeconds = 2.0,
                Seed = null,
            });

    /// <summary>Returns the bloom at <paramref name="column"/> on the corridor's only row.</summary>
    private static Cell Bloom(GameState state, int column) => state.Maze.GetCell(0, column);

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
