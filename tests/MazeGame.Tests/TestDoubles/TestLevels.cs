using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;

namespace MazeGame.Tests.TestDoubles;

/// <summary>
/// Hand-built mazes with <b>known</b> entity placement, so tests can assert exact cells
/// instead of hunting for things in a random maze.
/// </summary>
/// <remarks>
/// Placement is predictable because <see cref="FixedRandom"/>'s shuffle does nothing: cells
/// come out in row-major order, so the player is the first cell, the killer is the opposite
/// corner, the exit is the next free cell and the keys follow.
/// </remarks>
internal static class TestLevels
{
    /// <summary>Enemy step time used by the test levels: one cell per second.</summary>
    public const double OneSecondPerStep = 1.0;

    /// <summary>
    /// Builds a straight single-row corridor of <paramref name="length"/> cells.
    /// </summary>
    public static Grid Corridor(int length = 6)
    {
        var grid = new Grid(1, length);

        for (var column = 0; column < length - 1; column++)
        {
            grid.RemoveWall(0, column, Direction.East);
        }

        return grid;
    }

    /// <summary>
    /// Builds a 2x3 maze with two routes between (0,0) and (0,2): a short top route through
    /// (0,1) and a long bottom route through the whole bottom row.
    /// </summary>
    public static Grid TwoRoutes()
    {
        var grid = new Grid(2, 3);

        grid.RemoveWall(0, 0, Direction.East);
        grid.RemoveWall(0, 1, Direction.East);

        grid.RemoveWall(0, 0, Direction.South);
        grid.RemoveWall(1, 0, Direction.East);
        grid.RemoveWall(1, 1, Direction.East);
        grid.RemoveWall(1, 2, Direction.North);

        return grid;
    }

    /// <summary>
    /// A 1x6 corridor game: player (0,0), exit (0,1), keys (0,2)-(0,4), killer (0,5).
    /// </summary>
    public static GameState CorridorGame(
        int length = 6,
        int keyCount = 3,
        int barrierBudget = 2,
        double enemyStepSeconds = OneSecondPerStep,
        int visionRadius = 3) =>
        new(
            CorridorConfig(length, keyCount, barrierBudget, enemyStepSeconds, visionRadius),
            Corridor(length),
            new DijkstraPathfinder(),
            new FixedRandom(0));

    /// <summary>
    /// The config matching <see cref="Corridor"/>.
    /// </summary>
    public static GameConfig CorridorConfig(
        int length = 6,
        int keyCount = 3,
        int barrierBudget = 2,
        double enemyStepSeconds = OneSecondPerStep,
        int visionRadius = 3) => new()
        {
            Rows = 1,
            Columns = length,
            KeyCount = keyCount,
            BarrierBudget = barrierBudget,
            EnemyStepSeconds = enemyStepSeconds,
            VisionRadius = visionRadius,
            Seed = null,
        };

    /// <summary>
    /// Aims a barrier at a specific cell (the cursor ignores walls) and confirms it.
    /// </summary>
    /// <returns><see langword="true"/> when the barrier was placed.</returns>
    public static bool PlaceBarrierAt(GameState state, int row, int column)
    {
        if (!state.BeginBarrierPlacement())
        {
            return false;
        }

        var target = state.Maze.GetCell(row, column);

        while (state.BarrierCursor != target)
        {
            if (!state.MoveBarrierCursor(StepToward(state.BarrierCursor, target)))
            {
                state.CancelBarrierPlacement();
                return false;
            }
        }

        return state.ConfirmBarrierPlacement();
    }

    private static Direction StepToward(Cell from, Cell to)
    {
        if (to.Row < from.Row)
        {
            return Direction.North;
        }

        if (to.Row > from.Row)
        {
            return Direction.South;
        }

        return to.Column > from.Column ? Direction.East : Direction.West;
    }

    /// <summary>
    /// A two-route game: player (0,0), exit (0,1), one key at (0,2), killer (1,2).
    /// </summary>
    public static GameState TwoRouteGame(int keyCount = 1, int barrierBudget = 2) =>
        new(
            new GameConfig
            {
                Rows = 2,
                Columns = 3,
                KeyCount = keyCount,
                BarrierBudget = barrierBudget,
                EnemyStepSeconds = OneSecondPerStep,
                Seed = null,
            },
            TwoRoutes(),
            new DijkstraPathfinder(),
            new FixedRandom(0));
}
