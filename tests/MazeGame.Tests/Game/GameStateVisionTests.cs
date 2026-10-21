using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the fog-of-war rule: the player sees their own cell plus everything within
/// <c>VisionRadius</c> walking steps through open passages, and the lantern also spills onto the
/// cells within <c>NearSightRadius</c> of the player — walls or no walls — so a wall's near face is
/// never missing.
/// </summary>
/// <remarks>
/// Visibility lives in Core rather than in a renderer because it decides what the player is
/// allowed to know. These tests therefore check the rule, not the drawing.
/// </remarks>
public sealed class GameStateVisionTests
{
    [Fact]
    public void IsVisible_OwnCell_IsAlwaysVisible()
    {
        var state = TestLevels.CorridorGame(visionRadius: 0);

        Assert.True(state.IsVisible(state.Player));
    }

    [Fact]
    public void IsVisible_WithRadiusZero_SeesOnlyThePlayersCell()
    {
        var state = TestLevels.CorridorGame(visionRadius: 0);

        Assert.False(state.IsVisible(state.Maze.GetCell(0, 1)));
    }

    [Fact]
    public void IsVisible_AlongACorridor_ReachesExactlyTheRadius()
    {
        var state = TestLevels.CorridorGame(visionRadius: 3);

        Assert.True(state.IsVisible(state.Maze.GetCell(0, 3)));
        Assert.False(state.IsVisible(state.Maze.GetCell(0, 4)));
    }

    [Fact]
    public void IsVisible_ThroughAWall_IsTrueWithinTheNearSightRadius()
    {
        // The lantern spills onto the rock around the player, walls or no walls. That is what draws
        // the near face of a wall you are standing against: without it the wall belongs to a cell
        // nobody has seen and the corridor looks like it has a gap in it.
        var config = new GameConfig
        {
            Rows = 2,
            Columns = 2,
            KeyCount = 0,
            VisionRadius = 3,
            NearSightRadius = 2,
            EnemyAuraDistance = 0,
            EnemyAuraRadius = 0,
            EnemyStepSeconds = 1000.0,
        };
        var state = new GameState(config, new Grid(2, 2), new DijkstraPathfinder(), new FixedRandom(0));

        Assert.True(state.IsVisible(state.Maze.GetCell(0, 1)));
        Assert.True(state.IsVisible(state.Maze.GetCell(1, 0)));
        Assert.Equal(2, state.NearSightRadius);
    }

    [Fact]
    public void IsVisible_WithNearSightOff_ThroughAWall_IsFalse()
    {
        // The spill is a setting, not a replacement for sight: with it at zero the old rule
        // returns, which is what proves the wall still blocks the torch itself.
        var config = new GameConfig
        {
            Rows = 2,
            Columns = 2,
            KeyCount = 0,
            VisionRadius = 1,
            NearSightRadius = 0,
            EnemyAuraDistance = 0,
            EnemyAuraRadius = 0,
            EnemyStepSeconds = 1000.0,
        };
        var state = new GameState(config, new Grid(2, 2), new DijkstraPathfinder(), new FixedRandom(0));
        var player = state.Player;
        var killer = state.Enemy;

        foreach (var row in new[] { 0, 1 })
        {
            foreach (var column in new[] { 0, 1 })
            {
                var cell = state.Maze.GetCell(row, column);

                if (cell != player && cell != killer)
                {
                    Assert.False(state.IsVisible(cell));
                }
            }
        }
    }

    [Fact]
    public void IsVisible_AroundABend_SeesTheCornerAndStops()
    {
        // An L: (0,0)-(0,1)-(0,2)-(1,2). The radius is far larger than the maze, so only
        // connectivity can explain what is hidden: the bottom-left cells sit behind walls.
        var config = new GameConfig
        {
            Rows = 2,
            Columns = 3,
            KeyCount = 0,
            VisionRadius = 10,
            NearSightRadius = 0,
            EnemyStepSeconds = 1000.0,
        };
        var maze = new Grid(2, 3);
        maze.RemoveWall(0, 0, Direction.East);
        maze.RemoveWall(0, 1, Direction.East);
        maze.RemoveWall(0, 2, Direction.South);
        var state = new GameState(config, maze, new DijkstraPathfinder(), new FixedRandom(0));

        Assert.True(state.IsVisible(state.Maze.GetCell(0, 2)));
        Assert.True(state.IsVisible(state.Maze.GetCell(1, 2)));
        Assert.False(state.IsVisible(state.Maze.GetCell(1, 1)));
        Assert.False(state.IsVisible(state.Maze.GetCell(1, 0)));
    }

    [Fact]
    public void IsVisible_AfterThePlayerMoves_FollowsThePlayer()
    {
        var state = TestLevels.CorridorGame(visionRadius: 1);
        Assert.False(state.IsVisible(state.Maze.GetCell(0, 2)));

        Assert.True(state.TryMove(Direction.East));

        Assert.True(state.IsVisible(state.Maze.GetCell(0, 2)));
        Assert.True(state.IsVisible(state.Maze.GetCell(0, 0)));
        Assert.False(state.IsVisible(state.Maze.GetCell(0, 3)));
    }

    [Fact]
    public void IsVisible_TheKiller_IsHiddenUntilItEntersTheLight()
    {
        // The killer starts five cells away; with a radius of 2 the player cannot see it,
        // and every step it takes while still out of range stays invisible.
        var state = TestLevels.CorridorGame(visionRadius: 2, enemyStepSeconds: 1.0);
        var killerCell = state.Enemy;

        Assert.False(state.IsVisible(killerCell));

        state.Tick(1.0);
        state.Tick(1.0);

        Assert.False(state.IsVisible(state.Enemy));

        state.Tick(1.0);

        Assert.True(state.IsVisible(state.Enemy));
    }
}
