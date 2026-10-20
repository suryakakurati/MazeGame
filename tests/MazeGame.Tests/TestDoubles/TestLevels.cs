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
    /// <param name="length">Number of cells in the corridor.</param>
    /// <param name="keyCount">How many keys the floor needs.</param>
    /// <param name="barrierBudget">Barriers the player may place.</param>
    /// <param name="enemyStepSeconds">Seconds per killer step.</param>
    /// <param name="visionRadius">The player's torch range.</param>
    /// <param name="enemyAuraRadius">Radius of the killer's own light.</param>
    /// <param name="enemyAuraDistance">How close the killer must be for its light to show.</param>
    /// <param name="barrierSealSeconds">How long a fresh seal holds.</param>
    /// <param name="startingHearts">
    /// Hearts the player starts with. One by default, so these chase tests keep their original
    /// "caught means over" meaning; the heart economy has its own tests and its own levels.
    /// </param>
    public static GameState CorridorGame(
        int length = 6,
        int keyCount = 3,
        int barrierBudget = 2,
        double enemyStepSeconds = OneSecondPerStep,
        int visionRadius = 3,
        int enemyAuraRadius = 0,
        int enemyAuraDistance = 0,
        double barrierSealSeconds = 10.0,
        int startingHearts = 1) =>
        new(
            CorridorConfig(
                length,
                keyCount,
                barrierBudget,
                enemyStepSeconds,
                visionRadius,
                enemyAuraRadius,
                enemyAuraDistance,
                barrierSealSeconds,
                startingHearts),
            Corridor(length),
            new DijkstraPathfinder(),
            new FixedRandom(0));

    /// <summary>
    /// The config matching <see cref="Corridor"/>.
    /// </summary>
    /// <remarks>
    /// The killer's aura is switched off by default here even though the shipped config turns
    /// it on: most rule tests want the plain torch behaviour, and the aura has its own tests.
    /// </remarks>
    public static GameConfig CorridorConfig(
        int length = 6,
        int keyCount = 3,
        int barrierBudget = 2,
        double enemyStepSeconds = OneSecondPerStep,
        int visionRadius = 3,
        int enemyAuraRadius = 0,
        int enemyAuraDistance = 0,
        double barrierSealSeconds = 10.0,
        int startingHearts = 1) => new()
        {
            Rows = 1,
            Columns = length,
            KeyCount = keyCount,
            BarrierBudget = barrierBudget,
            EnemyStepSeconds = enemyStepSeconds,
            VisionRadius = visionRadius,
            EnemyAuraRadius = enemyAuraRadius,
            EnemyAuraDistance = enemyAuraDistance,
            BarrierSealSeconds = barrierSealSeconds,
            StartingHearts = startingHearts,
            MaxHearts = startingHearts,
            Seed = null,
        };

    /// <summary>
    /// Aims a barrier at a specific cell (the cursor ignores walls) and confirms it.
    /// </summary>
    /// <returns><see langword="true"/> when the barrier was placed.</returns>
    public static bool PlaceBarrierAt(GameState state, int row, int column) =>
        PlaceAt(state, row, column, state.BeginBarrierPlacement());

    /// <summary>
    /// Aims a wisp at a specific cell and confirms it.
    /// </summary>
    /// <returns><see langword="true"/> when the wisp was lit.</returns>
    public static bool PlaceLureAt(GameState state, int row, int column) =>
        PlaceAt(state, row, column, state.BeginLurePlacement());

    /// <summary>
    /// Walks the aiming cursor to a cell over the maze (the cursor ignores walls) and confirms
    /// whatever was aimed.
    /// </summary>
    /// <param name="state">The game to place in.</param>
    /// <param name="row">Target row.</param>
    /// <param name="column">Target column.</param>
    /// <param name="aiming">Whether the step before aiming succeeded.</param>
    /// <returns><see langword="true"/> when the placement was confirmed.</returns>
    private static bool PlaceAt(GameState state, int row, int column, bool aiming)
    {
        if (!aiming)
        {
            return false;
        }

        var target = state.Maze.GetCell(row, column);

        while (state.AimCursor != target)
        {
            if (!state.MoveAimCursor(StepToward(state.AimCursor, target)))
            {
                state.CancelPlacement();
                return false;
            }
        }

        return state.ConfirmPlacement();
    }

    /// <summary>
    /// Builds a game over an existing maze with a config of the caller's choosing, for tests
    /// that need to tune a rule the corridors deliberately leave at their shipped defaults.
    /// </summary>
    public static GameState GameOver(Grid maze, GameConfig config) =>
        new(config, maze, new DijkstraPathfinder(), new FixedRandom(0));

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
    public static GameState TwoRouteGame(int keyCount = 1, int barrierBudget = 2, int startingHearts = 1) =>
        new(
            new GameConfig
            {
                Rows = 2,
                Columns = 3,
                KeyCount = keyCount,
                BarrierBudget = barrierBudget,
                EnemyStepSeconds = OneSecondPerStep,
                StartingHearts = startingHearts,
                MaxHearts = startingHearts,
                Seed = null,
            },
            TwoRoutes(),
            new DijkstraPathfinder(),
            new FixedRandom(0));
}
