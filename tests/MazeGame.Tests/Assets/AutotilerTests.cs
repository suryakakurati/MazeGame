using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.AssetForge;

namespace MazeGame.Tests.Assets;

/// <summary>
/// Verifies the autotiler: 16 edge tiles, 47 provable blob classes, and that a tile's open sides
/// are exactly the mask it was asked for.
/// </summary>
public sealed class AutotilerTests
{
    private const int North = 1;
    private const int East = 4;
    private const int South = 16;
    private const int West = 64;

    [Fact]
    public void TheBlobTable_HasFortySevenClasses()
    {
        Assert.Equal(Autotiler.BlobTileCount, Autotiler.BlobMasks.Count);
        Assert.Equal(47, Autotiler.BlobMasks.Select(Autotiler.BlobClass).Distinct().Count());
    }

    [Fact]
    public void EveryMask_MapsIntoTheTable()
    {
        for (var mask = 0; mask < 256; mask++)
        {
            Assert.InRange(Autotiler.BlobClass(mask), 0, Autotiler.BlobTileCount - 1);
        }
    }

    [Fact]
    public void ADiagonalWithoutItsEdges_IsTheSameAsSolidWall()
    {
        // The corner bit only matters when both of its edges are open; otherwise the tile is the
        // plain wall, which is what keeps the table at 47 rather than 256.
        Assert.Equal(Autotiler.BlobClass(0), Autotiler.BlobClass(2));    // NE alone
        Assert.Equal(Autotiler.BlobClass(0), Autotiler.BlobClass(128));  // NW alone
        Assert.NotEqual(Autotiler.BlobClass(North | East), Autotiler.BlobClass(North | East | 2));
    }

    [Fact]
    public void AnEdgeTile_CarvesExactlyTheOpenSides()
    {
        var theme = ThemeCatalog.ById("shallows")!;
        var centre = CaveTiles.TileSize / 2;

        var wall = Autotiler.EdgeTile(theme, 0);
        AssertRock(wall, centre, 2);
        AssertRock(wall, 2, centre);

        var northOpen = Autotiler.EdgeTile(theme, 1);
        AssertFloor(northOpen, centre, 2);
        AssertRock(northOpen, centre, 12);

        var eastOpen = Autotiler.EdgeTile(theme, 2);
        AssertFloor(eastOpen, 13, centre);
        AssertRock(eastOpen, 2, centre);

        var southOpen = Autotiler.EdgeTile(theme, 4);
        AssertFloor(southOpen, centre, 13);

        var westOpen = Autotiler.EdgeTile(theme, 8);
        AssertFloor(westOpen, 2, centre);
    }

    [Fact]
    public void ABlobTile_CarvesTheSameSidesAsItsMask()
    {
        var theme = ThemeCatalog.ById("drowned")!;

        var open = Autotiler.BlobTile(theme, North | South | West);
        var closed = Autotiler.BlobTile(theme, 0);

        AssertFloor(open, 8, 2);
        AssertFloor(open, 8, 13);
        AssertFloor(open, 2, 8);
        AssertRock(open, 13, 8);

        AssertRock(closed, 8, 2);
    }

    [Fact]
    public void TwoMasksOfTheSameClass_DrawTheSameTile()
    {
        var theme = ThemeCatalog.ById("rootbound")!;

        // North open but East shut makes the NE corner redundant, so the two masks are one shape.
        var withCorner = Autotiler.BlobTile(theme, North | 2);
        var withoutCorner = Autotiler.BlobTile(theme, North);

        for (var row = 0; row < CaveTiles.TileSize; row++)
        {
            for (var column = 0; column < CaveTiles.TileSize; column++)
            {
                Assert.Equal(withoutCorner[column, row], withCorner[column, row]);
            }
        }
    }

    [Fact]
    public void EveryEdgeAndBlobTile_IsFullyOpaque()
    {
        var theme = ThemeCatalog.ById("shallows")!;
        var expected = CaveTiles.TileSize * CaveTiles.TileSize;

        for (var mask = 0; mask < Autotiler.EdgeTileCount; mask++)
        {
            Assert.Equal(expected, Autotiler.EdgeTile(theme, mask).DrawnPixels());
        }

        foreach (var mask in Autotiler.BlobMasks)
        {
            Assert.Equal(expected, Autotiler.BlobTile(theme, mask).DrawnPixels());
        }
    }

    [Fact]
    public void TheAutotiler_RejectsMasksOutsideItsRange()
    {
        var theme = ThemeCatalog.ById("shallows")!;

        Assert.Throws<ArgumentOutOfRangeException>(() => Autotiler.EdgeTile(theme, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => Autotiler.BlobTile(theme, 256));
        Assert.Throws<ArgumentOutOfRangeException>(() => Autotiler.BlobClass(300));
        Assert.Throws<ArgumentNullException>(() => Autotiler.EdgeTile(null!, 1));
    }

    private static void AssertFloor(PixelCanvas canvas, int x, int y) =>
        Assert.True(
            canvas[x, y] is (byte)PaletteRole.Floor or (byte)PaletteRole.FloorDark,
            $"({x},{y}) should be floor, is role {canvas[x, y]}");

    private static void AssertRock(PixelCanvas canvas, int x, int y) =>
        Assert.False(
            canvas[x, y] is (byte)PaletteRole.Floor or (byte)PaletteRole.FloorDark,
            $"({x},{y}) should be rock, is role {canvas[x, y]}");

    [Fact]
    public void TheEdgeTiles_AreAllDifferent()
    {
        var theme = ThemeCatalog.ById("shallows")!;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var mask = 0; mask < Autotiler.EdgeTileCount; mask++)
        {
            var pixels = Convert.ToBase64String(Autotiler.EdgeTile(theme, mask).ToRgba(Palette.Derive(theme)));
            Assert.True(seen.Add(pixels), $"edge mask {mask} draws an existing tile");
        }
    }
}
