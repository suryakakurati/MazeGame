using System;
using MazeGame.AssetForge;
using MazeGame.Core.Maze;
using MazeGame.UI.Rendering.Pixels;

namespace MazeGame.Tests.Rendering;

/// <summary>
/// Verifies the renderer's neighbour masks against the sprite compiler's: the same bit order, the
/// same canonical corner rule, and therefore the same tile for the same shape.
/// </summary>
/// <remarks>
/// The game cannot reference the tool, so the mask logic exists twice. This is the seam that keeps
/// the two halves honest: if one rule changes without the other, every wall on the floor is drawn
/// with the wrong hole and these tests say so first.
/// </remarks>
public sealed class TileMaskTests
{
    [Fact]
    public void Canonical_ProducesTheCompilersFortySevenShapes()
    {
        var seen = new System.Collections.Generic.SortedSet<int>();

        for (var mask = 0; mask < 256; mask++)
        {
            seen.Add(TileMask.Canonical(mask));
        }

        Assert.Equal(Autotiler.BlobTileCount, seen.Count);
        Assert.Equal(Autotiler.BlobMasks, seen);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(31)]
    [InlineData(42)]
    [InlineData(93)]
    [InlineData(128)]
    [InlineData(170)]
    [InlineData(255)]
    public void BlobClass_AgreesWithTheCompilerForEveryMask(int mask)
    {
        Assert.Equal(Autotiler.BlobClass(mask), TileMask.BlobClass(mask));
    }

    [Fact]
    public void BlobClass_AgreesWithTheCompilerForAllMasks()
    {
        for (var mask = 0; mask < 256; mask++)
        {
            Assert.Equal(Autotiler.BlobClass(mask), TileMask.BlobClass(mask));
        }
    }

    [Fact]
    public void Open_OnAnIsolatedCell_IsZero()
    {
        var maze = new Grid(1, 1);
        var cell = maze.GetCell(0, 0);

        Assert.Equal(0, TileMask.Open(maze, cell));
        Assert.Equal("blob-0", TileMask.BlobTile(TileMask.Open(maze, cell)));
    }

    [Fact]
    public void Open_OnACorridor_ReportsBothPassages()
    {
        var maze = TestCorridor(3);

        Assert.Equal(TileMask.East | TileMask.West, TileMask.Open(maze, maze.GetCell(0, 1)));
        Assert.Equal(TileMask.East, TileMask.Open(maze, maze.GetCell(0, 0)));
        Assert.Equal(TileMask.West, TileMask.Open(maze, maze.GetCell(0, 2)));
    }

    [Fact]
    public void Open_CountsACornerOnlyWhenBothOfItsEdgesAreOpen()
    {
        var straight = new Grid(2, 2);
        straight.RemoveWall(0, 0, Direction.East);

        // East is open and north is not, so the north-east corner is rock.
        Assert.Equal(TileMask.East, TileMask.Open(straight, straight.GetCell(0, 0)));

        var corner = new Grid(2, 2);
        corner.RemoveWall(0, 0, Direction.East);
        corner.RemoveWall(0, 0, Direction.South);

        // East and south are open, so the south-east corner is open too.
        Assert.Equal(
            TileMask.East | TileMask.South | TileMask.SouthEast,
            TileMask.Open(corner, corner.GetCell(0, 0)));
    }

    [Fact]
    public void Open_TreatsOutsideTheMazeAsWall()
    {
        var maze = TestCorridor(3);

        Assert.Equal(0, TileMask.Open(maze, maze.GetCell(0, 0)) & TileMask.North);
        Assert.Equal(0, TileMask.Open(maze, maze.GetCell(0, 0)) & TileMask.South);
    }

    [Fact]
    public void EdgeMask_UsesTheCompilersCardinalBits()
    {
        Assert.Equal(1, TileMask.EdgeMask(TileMask.North));
        Assert.Equal(2, TileMask.EdgeMask(TileMask.East));
        Assert.Equal(4, TileMask.EdgeMask(TileMask.South));
        Assert.Equal(8, TileMask.EdgeMask(TileMask.West));
        Assert.Equal(9, TileMask.EdgeMask(TileMask.North | TileMask.West));
        Assert.Equal("edge-9", TileMask.EdgeTile(TileMask.North | TileMask.West));
    }

    [Fact]
    public void WallFace_NamesTheSideThatHasAWall()
    {
        var maze = TestCorridor(3);
        var middle = maze.GetCell(0, 1);

        Assert.Equal("wall-north", TileMask.WallFace(maze, middle, Direction.North));
        Assert.Null(TileMask.WallFace(maze, middle, Direction.East));
        Assert.Null(TileMask.WallFace(maze, middle, Direction.West));
    }

    [Fact]
    public void BlobClass_RejectsAMaskWithMoreThanEightBits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMask.BlobClass(256));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMask.BlobClass(-1));
    }

    /// <summary>Builds a single-row corridor, so open sides are known exactly.</summary>
    private static Grid TestCorridor(int length)
    {
        var grid = new Grid(1, length);

        for (var column = 0; column < length - 1; column++)
        {
            grid.RemoveWall(0, column, Direction.East);
        }

        return grid;
    }
}
