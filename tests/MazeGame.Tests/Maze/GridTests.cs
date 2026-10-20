using System;
using MazeGame.Core.Maze;

namespace MazeGame.Tests.Maze;

/// <summary>
/// Verifies that <see cref="Grid"/> starts fully walled, keeps shared walls consistent,
/// and rejects every out-of-range coordinate or direction instead of failing silently.
/// </summary>
public sealed class GridTests
{
    private const int Rows = 3;
    private const int Columns = 4;

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Constructor_WithInvalidSize_Throws(int rows, int columns)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Grid(rows, columns));
    }

    [Fact]
    public void Constructor_SetsDimensionsAndCellCount()
    {
        var grid = new Grid(Rows, Columns);

        Assert.Equal(Rows, grid.Rows);
        Assert.Equal(Columns, grid.Columns);
        Assert.Equal(Rows * Columns, grid.CellCount);
    }

    [Fact]
    public void NewGrid_EveryCellHasFourWalls()
    {
        var grid = new Grid(Rows, Columns);

        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var cell = grid.GetCell(row, column);

                Assert.Equal(4, cell.WallCount);
                Assert.Equal(row, cell.Row);
                Assert.Equal(column, cell.Column);
            }
        }
    }

    [Fact]
    public void RemoveWall_OpensThePassageFromBothSides()
    {
        var grid = new Grid(Rows, Columns);

        grid.RemoveWall(1, 1, Direction.East);

        Assert.False(grid.HasWall(1, 1, Direction.East));
        Assert.False(grid.HasWall(1, 2, Direction.West));
        Assert.True(grid.HasWall(1, 1, Direction.South));
        Assert.True(grid.HasWall(1, 2, Direction.East));
        Assert.Equal(1, grid.CountPassages());
    }

    [Fact]
    public void RemoveWall_DoesNotTouchAnyOtherWall()
    {
        var grid = new Grid(Rows, Columns);

        grid.RemoveWall(0, 0, Direction.South);

        var wallsBefore = CountTotalWalls(grid);
        Assert.Equal((Rows * Columns * 4) - 2, wallsBefore);
    }

    [Fact]
    public void RemoveWall_Twice_ChangesNothingFurther()
    {
        var grid = new Grid(Rows, Columns);

        grid.RemoveWall(0, 0, Direction.East);
        grid.RemoveWall(0, 0, Direction.East);

        Assert.Equal(1, grid.CountPassages());
    }

    [Theory]
    [InlineData(0, 0, Direction.North)]
    [InlineData(0, 0, Direction.West)]
    [InlineData(Rows - 1, 0, Direction.South)]
    [InlineData(0, Columns - 1, Direction.East)]
    public void RemoveWall_OnOuterBoundary_Throws(int row, int column, Direction direction)
    {
        var grid = new Grid(Rows, Columns);
        var passagesBefore = grid.CountPassages();

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.RemoveWall(row, column, direction));
        Assert.Equal(passagesBefore, grid.CountPassages());
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(Rows, 0)]
    [InlineData(0, Columns)]
    public void GetCell_OutsideGrid_Throws(int row, int column)
    {
        var grid = new Grid(Rows, Columns);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetCell(row, column));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.HasWall(row, column, Direction.North));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetIndex(row, column));
    }

    [Fact]
    public void RemoveWall_OutsideGrid_Throws()
    {
        var grid = new Grid(Rows, Columns);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.RemoveWall(Rows, 0, Direction.East));
    }

    [Fact]
    public void HasWall_WithUndefinedDirection_Throws()
    {
        var grid = new Grid(Rows, Columns);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.HasWall(0, 0, (Direction)99));
    }

    [Fact]
    public void RemoveWall_WithUndefinedDirection_Throws()
    {
        var grid = new Grid(Rows, Columns);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.RemoveWall(0, 0, (Direction)99));
    }

    [Fact]
    public void GetIndex_MapsRowMajorOrder()
    {
        var grid = new Grid(Rows, Columns);

        Assert.Equal(0, grid.GetIndex(0, 0));
        Assert.Equal(Columns - 1, grid.GetIndex(0, Columns - 1));
        Assert.Equal(Columns, grid.GetIndex(1, 0));
        Assert.Equal(grid.CellCount - 1, grid.GetIndex(Rows - 1, Columns - 1));
    }

    [Fact]
    public void GetIndex_IsUniqueForEveryCell()
    {
        var grid = new Grid(Rows, Columns);
        var seen = new bool[grid.CellCount];

        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var index = grid.GetIndex(row, column);

                Assert.False(seen[index]);
                seen[index] = true;
            }
        }
    }

    [Fact]
    public void CountPassages_OnNewGrid_IsZero()
    {
        var grid = new Grid(Rows, Columns);

        Assert.Equal(0, grid.CountPassages());
    }

    private static int CountTotalWalls(Grid grid)
    {
        var walls = 0;

        for (var row = 0; row < grid.Rows; row++)
        {
            for (var column = 0; column < grid.Columns; column++)
            {
                walls += grid.GetCell(row, column).WallCount;
            }
        }

        return walls;
    }
}
