using System;
using System.Collections.Generic;
using MazeGame.Core.Maze;

namespace MazeGame.UI.Rendering.Pixels;

/// <summary>
/// Which of a cell's eight neighbours are reachable, and which atlas tile that mask names.
/// </summary>
/// <remarks>
/// <para>
/// The bit order is the compiler's, clockwise from north: <c>N=1, NE=2, E=4, SE=8, S=16, SW=32,
/// W=64, NW=128</c>, so <c>edge-n</c> and <c>blob-n</c> mean the same thing on both sides of the
/// manifest. Only cardinal neighbours are real walls in this maze, so a diagonal is open exactly
/// when both of its edges are — which is also the rule that canonicalises 256 masks down to 47, so
/// the mask this class produces is already in canonical form.
/// </para>
/// <para>
/// This duplicates <c>tools/AssetForge/Autotiler</c> on purpose: the game cannot reference the
/// tool. A test compares both class tables bit for bit, so a change to one that is not made to the
/// other is a red test rather than a wall drawn with the wrong hole.
/// </para>
/// </remarks>
public static class TileMask
{
    /// <summary>Bits, clockwise from north.</summary>
    public const int North = 1;

    /// <summary>The north-east corner.</summary>
    public const int NorthEast = 2;

    /// <summary>The east side.</summary>
    public const int East = 4;

    /// <summary>The south-east corner.</summary>
    public const int SouthEast = 8;

    /// <summary>The south side.</summary>
    public const int South = 16;

    /// <summary>The south-west corner.</summary>
    public const int SouthWest = 32;

    /// <summary>The west side.</summary>
    public const int West = 64;

    /// <summary>The north-west corner.</summary>
    public const int NorthWest = 128;

    /// <summary>How many cardinal masks an edge tileset has.</summary>
    public const int EdgeTileCount = 16;

    /// <summary>How many distinct shapes eight-neighbour masks produce once corners are canonical.</summary>
    public const int BlobTileCount = 47;

    private static readonly byte[] ClassOfMask;

    static TileMask()
    {
        var classOf = new byte[256];
        var seen = new Dictionary<int, int>(BlobTileCount);

        for (var mask = 0; mask < 256; mask++)
        {
            var canonical = Canonical(mask);

            if (!seen.TryGetValue(canonical, out var index))
            {
                index = seen.Count;
                seen.Add(canonical, index);
            }

            classOf[mask] = (byte)index;
        }

        if (seen.Count != BlobTileCount)
        {
            throw new InvalidOperationException(
                $"The canonical corner rule produced {seen.Count} shapes, not {BlobTileCount}.");
        }

        ClassOfMask = classOf;
    }

    /// <summary>
    /// Returns the eight-neighbour mask of the cells reachable from <paramref name="cell"/>.
    /// </summary>
    /// <param name="maze">The floor's maze.</param>
    /// <param name="cell">The cell whose neighbours are tested.</param>
    /// <returns>The open mask, in canonical form.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="maze"/> is null.</exception>
    public static int Open(Grid maze, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(maze);

        var mask = 0;

        if (IsOpen(maze, cell, -1, 0, Direction.North))
        {
            mask |= North;
        }

        if (IsOpen(maze, cell, 0, 1, Direction.East))
        {
            mask |= East;
        }

        if (IsOpen(maze, cell, 1, 0, Direction.South))
        {
            mask |= South;
        }

        if (IsOpen(maze, cell, 0, -1, Direction.West))
        {
            mask |= West;
        }

        // A diagonal only counts when both of its edges are open, the same rule the compiler uses
        // to canonicalise a mask, so the two halves cannot disagree about a corner.
        if ((mask & North) != 0 && (mask & East) != 0)
        {
            mask |= NorthEast;
        }

        if ((mask & South) != 0 && (mask & East) != 0)
        {
            mask |= SouthEast;
        }

        if ((mask & South) != 0 && (mask & West) != 0)
        {
            mask |= SouthWest;
        }

        if ((mask & North) != 0 && (mask & West) != 0)
        {
            mask |= NorthWest;
        }

        return mask;
    }

    /// <summary>Clears every corner bit whose two edges are not both open.</summary>
    /// <param name="mask">An eight-neighbour mask.</param>
    /// <returns>The canonical mask for its shape.</returns>
    public static int Canonical(int mask)
    {
        if ((mask & North) == 0 || (mask & East) == 0)
        {
            mask &= ~NorthEast;
        }

        if ((mask & South) == 0 || (mask & East) == 0)
        {
            mask &= ~SouthEast;
        }

        if ((mask & South) == 0 || (mask & West) == 0)
        {
            mask &= ~SouthWest;
        }

        if ((mask & North) == 0 || (mask & West) == 0)
        {
            mask &= ~NorthWest;
        }

        return mask;
    }

    /// <summary>Returns the blob tile class for an eight-neighbour mask.</summary>
    /// <param name="mask">An eight-neighbour mask, in any form.</param>
    /// <returns>The class, 0 to 46.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the mask is outside 0..255.</exception>
    public static int BlobClass(int mask)
    {
        if (mask is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(mask), mask, "An open mask has eight bits.");
        }

        return ClassOfMask[mask];
    }

    /// <summary>Returns the four-bit cardinal mask the edge tiles are indexed by.</summary>
    /// <param name="mask">An eight-neighbour mask.</param>
    /// <returns>Bits N=1, E=2, S=4, W=8.</returns>
    public static int EdgeMask(int mask)
    {
        var edges = 0;

        if ((mask & North) != 0)
        {
            edges |= 1;
        }

        if ((mask & East) != 0)
        {
            edges |= 2;
        }

        if ((mask & South) != 0)
        {
            edges |= 4;
        }

        if ((mask & West) != 0)
        {
            edges |= 8;
        }

        return edges;
    }

    /// <summary>Returns the name of the tile that draws <paramref name="mask"/>'s rock.</summary>
    /// <param name="mask">An eight-neighbour mask.</param>
    /// <returns>The tile's manifest name.</returns>
    public static string BlobTile(int mask) => $"blob-{BlobClass(mask)}";

    /// <summary>Returns the name of the edge tile for <paramref name="mask"/>'s open sides.</summary>
    /// <param name="mask">An eight-neighbour mask.</param>
    /// <returns>The tile's manifest name.</returns>
    public static string EdgeTile(int mask) => $"edge-{EdgeMask(mask)}";

    /// <summary>
    /// Returns the name of the thin wall face on one side of a cell, or null when that side is open.
    /// </summary>
    /// <param name="maze">The floor's maze.</param>
    /// <param name="cell">The cell whose edge is tested.</param>
    /// <param name="direction">The side of the cell.</param>
    /// <returns>The tile's manifest name, or null when the side is a passage.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="maze"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="direction"/> is not a cardinal direction.</exception>
    public static string? WallFace(Grid maze, Cell cell, Direction direction)
    {
        ArgumentNullException.ThrowIfNull(maze);

        if (maze.HasWall(cell.Row, cell.Column, direction))
        {
            return direction switch
            {
                Direction.North => "wall-north",
                Direction.East => "wall-east",
                Direction.South => "wall-south",
                Direction.West => "wall-west",
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Not a cardinal direction."),
            };
        }

        return null;
    }

    /// <summary>
    /// Tests whether the cell one step away in a direction is inside the maze and reachable.
    /// </summary>
    private static bool IsOpen(Grid maze, Cell cell, int rowDelta, int columnDelta, Direction direction)
    {
        var row = cell.Row + rowDelta;
        var column = cell.Column + columnDelta;

        if (row < 0 || row >= maze.Rows || column < 0 || column >= maze.Columns)
        {
            return false;
        }

        return !maze.HasWall(cell.Row, cell.Column, direction);
    }
}
