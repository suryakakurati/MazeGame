using System;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;
using Xunit;

namespace MazeGame.Tests.Game;

/// <summary>
/// The floor's optional find: where it is placed, what each kind pays, and what the floor does to
/// the player who touches it.
/// </summary>
public sealed class CuriosityTests
{
    [Fact]
    public void EveryShippedFloor_HoldsOneFind_AndTheCageHoldsItsLocks()
    {
        var factory = new GameFactory(
            new GameConfig { Seed = 424242 },
            seed => new SeededRandom(seed),
            new DijkstraPathfinder());

        foreach (var floor in FloorCatalog.Shipped)
        {
            var state = factory.CreateFloor(floor, startingHearts: 3, RunModifiers.None);

            Assert.NotNull(state.CuriosityCell);
            Assert.DoesNotContain(state.CuriosityCell!.Value, state.Keys);
            Assert.Equal(0, state.BonusBlooms);

            if (floor.Rule == FloorRule.ThreeLocks)
            {
                Assert.Equal(3, state.Locks.Count);
                Assert.False(state.IsExitUnlocked);
            }
            else
            {
                Assert.Empty(state.Locks);
            }
        }
    }

    [Fact]
    public void TheFind_IsPlacedOffTheShortestRoute()
    {
        var state = TestLevels.TwoRouteGame();

        var cell = state.CuriosityCell;

        Assert.NotNull(cell);
        Assert.NotEqual(state.Player, cell!.Value);
        Assert.NotEqual(state.Exit, cell.Value);
        Assert.DoesNotContain(cell.Value, state.Keys);
    }

    [Fact]
    public void TheBell_PaysAHeart_AndWakesTheSleeper()
    {
        var state = Build(CuriosityKind.Bell, rule: FloorRule.Sleeper);
        var before = state.Hearts;

        Assert.True(WalkTo(state, state.CuriosityCell!.Value));

        Assert.True(state.IsCuriositySpent);
        Assert.Equal(before + 1, state.Hearts);
        Assert.DoesNotContain(state.Enemies, hunter => hunter.Dormant);
    }

    [Fact]
    public void TheReliquary_SpendsAHeart_ForBloomsAndACharge()
    {
        var state = Build(CuriosityKind.Reliquary, hearts: 3);
        var charges = TotalCharges(state.PowerupsRemaining);

        Assert.True(WalkTo(state, state.CuriosityCell!.Value));

        Assert.True(state.IsCuriositySpent);
        Assert.Equal(2, state.Hearts);
        Assert.Equal(2, state.BonusBlooms);
        Assert.Equal(charges + 1, TotalCharges(state.PowerupsRemaining));
    }

    [Fact]
    public void TheReliquary_StaysSealed_WhenTheHeartIsTheLastOne()
    {
        var state = Build(CuriosityKind.Reliquary, hearts: 1);

        Assert.True(WalkTo(state, state.CuriosityCell!.Value));

        Assert.False(state.IsCuriositySpent);
        Assert.Equal(1, state.Hearts);
        Assert.Equal(0, state.BonusBlooms);
    }

    [Fact]
    public void TheShrine_GivesAStepOfSight_AndABloom()
    {
        var state = Build(CuriosityKind.Shrine);
        var before = state.EffectiveVisionRadius;

        Assert.True(WalkTo(state, state.CuriosityCell!.Value));

        Assert.True(state.IsCuriositySpent);
        Assert.Equal(before + 1, state.EffectiveVisionRadius);
        Assert.Equal(1, state.BonusBlooms);
    }

    [Fact]
    public void TheGreatCage_HoldsThreeLocks_AndTheArchWaitsForThem()
    {
        var state = Build(CuriosityKind.Shrine, rule: FloorRule.ThreeLocks);

        Assert.Equal(3, state.Locks.Count);
        Assert.Equal(3, state.LocksRemaining);
        Assert.False(state.IsExitUnlocked);

        for (var index = 0; index < 3; index++)
        {
            var lockCell = state.Locks[index];

            Assert.True(WalkTo(state, lockCell));
            Assert.True(state.IsLockThrown(lockCell));
            Assert.Equal(2 - index, state.LocksRemaining);
        }

        Assert.True(state.IsExitUnlocked);
    }

    [Fact]
    public void TheTide_FloodsOnItsCycle_AndTakesAStepOfSightWithIt()
    {
        var state = Build(CuriosityKind.Bell, rule: FloorRule.Tide);
        var dry = state.EffectiveVisionRadius;

        Assert.Equal(TidePhase.Dry, state.Tide);

        // Ten seconds of dry hall, then the two-second swell, then the water is in.
        state.Tick(10.5);
        Assert.Equal(TidePhase.Swell, state.Tide);

        state.Tick(2.0);
        Assert.Equal(TidePhase.Flood, state.Tide);
        Assert.Equal(dry - 1, state.EffectiveVisionRadius);

        state.Tick(8.0);
        Assert.Equal(TidePhase.Dry, state.Tide);
        Assert.Equal(dry, state.EffectiveVisionRadius);
    }

    /// <summary>
    /// Builds a nine-by-nine open room holding the wanted curiosity, on a floor seeded so the draw
    /// lands on it. Open rather than a corridor because a find has to have somewhere to hide.
    /// </summary>
    private static GameState Build(
        CuriosityKind kind,
        FloorRule? rule = null,
        int hearts = 3)
    {
        var seed = SeedFor(kind);

        return TestLevels.GameOver(
            RoomWithAnExitLeaf(9),
            new GameConfig
            {
                Rows = 9,
                Columns = 9,
                KeyCount = 0,
                EnemyStepSeconds = 60.0,
                StartingHearts = hearts,
                MaxHearts = hearts,
                Seed = seed,
                FloorNumber = 4,
                Rule = rule,
            });
    }

    /// <summary>Finds a seed whose curiosity draw lands on <paramref name="kind"/>.</summary>
    private static int SeedFor(CuriosityKind kind)
    {
        for (var seed = 0; seed < 100_000; seed++)
        {
            if (CuriosityCatalog.Pick(seed, 4) == kind)
            {
                return seed;
            }
        }

        throw new InvalidOperationException($"No seed draws {kind}.");
    }

    /// <summary>
    /// A square room with every interior wall removed, except that the arch's cell is a leaf off
    /// the room.
    /// </summary>
    /// <remarks>
    /// The exit is placed first after the player, so on a plain open room it would sit right beside
    /// the starting cell and every walk would step over it — and with no blooms on these test
    /// floors, stepping on the arch would win the floor instead of reaching the find.
    /// </remarks>
    private static Grid RoomWithAnExitLeaf(int size)
    {
        var grid = new Grid(size, size);

        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size - 1; column++)
            {
                // Leave the walls around (0,1) standing except its way in from (1,1), so the
                // arch is reachable but is nobody's shortcut.
                if (row == 0 && column is 0 or 1)
                {
                    continue;
                }

                grid.RemoveWall(row, column, Direction.East);
            }
        }

        for (var column = 0; column < size; column++)
        {
            for (var row = 0; row < size - 1; row++)
            {
                grid.RemoveWall(row, column, Direction.South);
            }
        }

        return grid;
    }

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

    private static int TotalCharges(PowerupCharges charges) =>
        charges.Blink + charges.Veil + charges.Drag + charges.Lantern + charges.Shove;

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
