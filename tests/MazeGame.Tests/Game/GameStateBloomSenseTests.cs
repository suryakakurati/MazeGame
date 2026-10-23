using System;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;
using Xunit;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the Grave Lantern's sense: a bloom within ten tiles glows on its own tile through the
/// dark, and nothing else does.
/// </summary>
/// <remarks>
/// The level is two long corridors joined only at their ends, with no torch at all. Walking east
/// along the lower corridor leaves the blooms in the upper one untouched, so the player can stand
/// a known number of tiles away from them and the sense is the only thing that can reveal them.
/// </remarks>
public sealed class GameStateBloomSenseTests
{
    private const int Columns = 20;

    [Fact]
    public void WithoutTheSense_TheBloomsAreDark()
    {
        var state = SenseGame(radius: 0);

        Assert.DoesNotContain(state.Keys, bloom => state.IsVisible(bloom));
    }

    [Fact]
    public void ABloomWithinTenTiles_GlowsOnItsOwnTileThroughAWall()
    {
        var state = SenseGame(radius: 10);

        // Nine columns and one row away, and its own south wall is still standing: the sense does
        // not need a route, only a direction.
        Assert.True(WalkTo(state, At(state, 1, 11)));

        var bloom = At(state, 0, 2);

        Assert.Contains(bloom, state.Keys);
        Assert.True(state.IsVisible(bloom));

        // Only the flower is lit. The cell directly beneath it is a floor tile the torch cannot
        // reach, and it stays dark.
        Assert.False(state.IsVisible(At(state, 1, 2)));
    }

    [Fact]
    public void TheSense_IsTilesNotSteps()
    {
        var state = SenseGame(radius: 10);

        Assert.True(WalkTo(state, At(state, 1, 11)));

        // Fourteen steps of walking away, nine tiles in a straight line: it glows.
        var steps = new DijkstraPathfinder()
            .FindPath(state.Maze, At(state, 1, 11), At(state, 0, 2), _ => 1)
            .Count - 1;

        Assert.True(steps > 10, $"the bloom is {steps} steps away");
        Assert.True(state.IsVisible(At(state, 0, 2)));
    }

    [Fact]
    public void ABloomFurtherThanTenTiles_StaysDark()
    {
        var state = SenseGame(radius: 10);

        Assert.True(WalkTo(state, At(state, 1, 17)));

        // Sixteen columns and a row: well beyond the reach, through no fault of the walls.
        Assert.False(state.IsVisible(At(state, 0, 2)));
    }

    [Fact]
    public void TheGraveLantern_IsTheRelicThatGrantsTheSense()
    {
        var relic = RelicCatalog.CardFor(RelicKind.GraveLantern);

        Assert.Equal(10, relic.Effect.BloomSenseRadius);
        Assert.Equal(RelicRarity.Legendary, RelicCatalog.RarityOf(RelicKind.GraveLantern));

        var config = RunModifiers.None.With(relic).Apply(new GameConfig());

        Assert.Equal(10, config.BloomSenseRadius);
    }

    /// <summary>
    /// A two-corridor room joined at both ends: the player starts at (0,0), the arch at (0,1),
    /// the blooms at (0,2) to (0,4) and the killer at the far lower corner.
    /// </summary>
    private static Grid SenseRoom()
    {
        var grid = new Grid(2, Columns);

        for (var column = 0; column < Columns - 1; column++)
        {
            grid.RemoveWall(0, column, Direction.East);
            grid.RemoveWall(1, column, Direction.East);
        }

        // Only the ends join the two corridors, so the walk to the far end cannot brush a bloom.
        grid.RemoveWall(0, 0, Direction.South);
        grid.RemoveWall(0, Columns - 1, Direction.South);

        return grid;
    }

    /// <summary>A floor with no torch, a still killer and the sense set to <paramref name="radius"/>.</summary>
    private static GameState SenseGame(int radius) =>
        TestLevels.GameOver(
            SenseRoom(),
            new GameConfig
            {
                Rows = 2,
                Columns = Columns,
                KeyCount = 3,
                EnemyStepSeconds = 1000.0,
                PlayerStepSeconds = 0,
                VisionRadius = 0,
                NearSightRadius = 0,
                EnemyAuraRadius = 0,
                EnemyAuraDistance = 0,
                StartingHearts = 3,
                MaxHearts = 3,
                BloomSenseRadius = radius,
                Seed = null,
            });

    /// <summary>Returns the cell of the state's maze at a row and column.</summary>
    private static Cell At(GameState state, int row, int column) => state.Maze.GetCell(row, column);

    /// <summary>Walks the player to <paramref name="target"/> along the shortest route.</summary>
    private static bool WalkTo(GameState state, Cell target)
    {
        var path = new DijkstraPathfinder().FindPath(state.Maze, state.Player, target, _ => 1);

        for (var index = 1; index < path.Count; index++)
        {
            if (!state.TryMove(DirectionToward(state.Player, path[index])))
            {
                return false;
            }
        }

        return state.Player.Row == target.Row && state.Player.Column == target.Column;
    }

    private static Direction DirectionToward(Cell from, Cell to)
    {
        if (to.Row < from.Row)
        {
            return Direction.North;
        }

        if (to.Row > from.Row)
        {
            return Direction.South;
        }

        return to.Column < from.Column ? Direction.West : Direction.East;
    }
}
