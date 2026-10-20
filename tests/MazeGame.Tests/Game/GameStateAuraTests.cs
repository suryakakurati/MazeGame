using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the killer's own light: once it is within a set number of steps, the cells around
/// it become visible even when the corridor between it and the player is dark.
/// </summary>
/// <remarks>
/// The aura is a Core rule because it decides what the player may know. Renderers only tint the
/// answer; `EnemyGlow` exists so they can tint it differently from the player's torch.
/// </remarks>
public sealed class GameStateAuraTests
{
    [Fact]
    public void EnemyGlow_WhileTheKillerIsFarAway_IsEmpty()
    {
        var state = TestLevels.CorridorGame(visionRadius: 1, enemyAuraRadius: 2, enemyAuraDistance: 2);

        Assert.Empty(state.EnemyGlow);
        Assert.False(state.IsVisible(state.Enemy));
    }

    [Fact]
    public void EnemyGlow_WhenTheKillerComesClose_LightsItsSurroundings()
    {
        // Player (0,0), killer (0,5); three steps bring it to (0,2), exactly the aura distance.
        var state = TestLevels.CorridorGame(visionRadius: 1, enemyAuraRadius: 2, enemyAuraDistance: 2, enemyStepSeconds: 1.0);
        for (var tick = 0; tick < 3; tick++)
        {
            state.Tick(1.0);
        }

        Assert.Equal(state.Maze.GetCell(0, 2), state.Enemy);
        Assert.NotEmpty(state.EnemyGlow);
        Assert.True(state.IsVisible(state.Enemy));

        // (0,4) is two steps from the killer but four from the player, well outside the torch:
        // it is visible only because the killer is lighting it.
        Assert.True(state.EnemyGlow.Contains(state.Maze.GetCell(0, 4)));
        Assert.True(state.IsVisible(state.Maze.GetCell(0, 4)));
    }

    [Fact]
    public void EnemyGlow_AtExactlyTheAuraDistance_Appears()
    {
        var state = TestLevels.CorridorGame(visionRadius: 1, enemyAuraRadius: 1, enemyAuraDistance: 5);

        Assert.NotEmpty(state.EnemyGlow);
    }

    [Fact]
    public void EnemyGlow_JustBeyondTheAuraDistance_StaysEmpty()
    {
        var state = TestLevels.CorridorGame(visionRadius: 1, enemyAuraRadius: 1, enemyAuraDistance: 4);

        Assert.Empty(state.EnemyGlow);
        Assert.False(state.IsVisible(state.Enemy));
    }

    [Fact]
    public void EnemyGlow_WithZeroRadius_LightsOnlyTheKillersCell()
    {
        var state = TestLevels.CorridorGame(visionRadius: 1, enemyAuraRadius: 0, enemyAuraDistance: 5);

        Assert.Equal(new[] { state.Enemy }, state.EnemyGlow);
        Assert.False(state.IsVisible(state.Maze.GetCell(0, 4)));
    }

    [Fact]
    public void EnemyGlow_ShinesAroundABend_WhereThePlayerCannotSee()
    {
        // An L: (0,0)-(0,1)-(0,2)-(1,2). The killer is three steps away around the corner, so
        // the player's own torch (radius 1) cannot reach it, but its aura can be seen.
        var config = new GameConfig
        {
            Rows = 2,
            Columns = 3,
            KeyCount = 0,
            VisionRadius = 1,
            EnemyAuraRadius = 1,
            EnemyAuraDistance = 3,
            EnemyStepSeconds = 1000.0,
        };
        var maze = new Grid(2, 3);
        maze.RemoveWall(0, 0, Direction.East);
        maze.RemoveWall(0, 1, Direction.East);
        maze.RemoveWall(0, 2, Direction.South);
        var state = new GameState(config, maze, new DijkstraPathfinder(), new FixedRandom(0));

        Assert.Equal(maze.GetCell(1, 2), state.Enemy);
        Assert.True(state.IsVisible(maze.GetCell(1, 2)));
        Assert.True(state.IsVisible(maze.GetCell(0, 2)));
        Assert.Contains(maze.GetCell(1, 2), state.EnemyGlow);

        // Aura still respects walls: (1,1) is walled off from the killer, so it stays dark.
        Assert.False(state.IsVisible(maze.GetCell(1, 1)));
    }

    [Fact]
    public void IsVisible_StillHidesEverythingBeyondBothLights()
    {
        var state = TestLevels.CorridorGame(
            length: 10,
            keyCount: 3,
            visionRadius: 1,
            enemyAuraRadius: 1,
            enemyAuraDistance: 2);

        // The killer starts at (0,9): far outside the aura distance, so only the torch is left.
        Assert.False(state.IsVisible(state.Maze.GetCell(0, 2)));
        Assert.Empty(state.EnemyGlow);
    }
}
