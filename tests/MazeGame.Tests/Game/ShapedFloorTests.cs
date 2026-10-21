using System.Collections.Generic;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the shipped floors whose mazes are not rectangles: the run still builds, nothing is
/// placed in the rock, and every key and the exit can be reached from where the player starts.
/// </summary>
/// <remarks>
/// A shape is the one content change that can silently make a floor unwinnable, because it removes
/// cells after the size checks have already passed on the rectangle. These tests walk the real
/// catalogue rather than a sample, so a new oval floor with too many keys fails here instead of in
/// someone's run.
/// </remarks>
public sealed class ShapedFloorTests
{
    private const int Seed = 4242;

    [Fact]
    public void EveryShippedFloor_BuildsAndKeepsEverythingOnTheFloor()
    {
        var factory = new GameFactory(
            new GameConfig { Seed = Seed },
            seed => new MazeGame.Core.Abstractions.SeededRandom(seed),
            new DijkstraPathfinder());

        foreach (var floor in FloorCatalog.Shipped)
        {
            var state = factory.CreateFloor(floor, floor.MaxHearts, RunModifiers.None);
            var maze = state.Maze;

            Assert.False(maze.IsSolid(state.Player.Row, state.Player.Column), $"floor {floor.Number}: player in rock");
            Assert.False(maze.IsSolid(state.Exit.Row, state.Exit.Column), $"floor {floor.Number}: exit in rock");

            foreach (var key in state.Keys)
            {
                Assert.False(maze.IsSolid(key.Row, key.Column), $"floor {floor.Number}: key in rock");
            }

            foreach (var hunter in state.Enemies)
            {
                Assert.False(
                    maze.IsSolid(hunter.Cell.Row, hunter.Cell.Column),
                    $"floor {floor.Number}: hunter in rock");
            }
        }
    }

    [Fact]
    public void EveryShippedFloor_LeavesEveryKeyAndTheExitReachable()
    {
        var factory = new GameFactory(
            new GameConfig { Seed = Seed },
            seed => new MazeGame.Core.Abstractions.SeededRandom(seed),
            new DijkstraPathfinder());
        var pathfinder = new DijkstraPathfinder();

        foreach (var floor in FloorCatalog.Shipped)
        {
            var state = factory.CreateFloor(floor, floor.MaxHearts, RunModifiers.None);

            AssertReachable(pathfinder, state.Maze, state.Player, state.Exit, $"floor {floor.Number}: exit");

            foreach (var key in state.Keys)
            {
                AssertReachable(pathfinder, state.Maze, state.Player, key, $"floor {floor.Number}: key");
            }
        }
    }

    [Fact]
    public void TheShippedCatalogue_ActuallyUsesMoreThanOneShape()
    {
        var shapes = new HashSet<ShapeKind>();

        foreach (var floor in FloorCatalog.Shipped)
        {
            shapes.Add(floor.Shape ?? ShapeKind.Rect);
        }

        // The point of the setting is variety: a catalogue where every floor is a rectangle would
        // pass every other test here while changing nothing the player sees.
        Assert.True(shapes.Count >= 3, $"the shipped floors only use {shapes.Count} outline(s)");
    }

    private static void AssertReachable(DijkstraPathfinder pathfinder, Grid maze, Cell from, Cell to, string what)
    {
        var path = pathfinder.FindPath(maze, from, to, _ => 1);

        Assert.True(path.Count > 0, $"{what} cannot be reached from the player's start");
    }
}
