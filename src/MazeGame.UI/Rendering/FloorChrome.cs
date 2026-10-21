using System;
using MazeGame.Core.Game;
using Raylib_cs;

namespace MazeGame.UI.Rendering;

/// <summary>
/// The floor identity the HUD needs: its number and name, the sentence that says what it does
/// differently, and the style that colours it.
/// </summary>
/// <param name="Number">The floor's one-based number in the run.</param>
/// <param name="Name">The floor's display name.</param>
/// <param name="Gimmick">The floor's one-line rule, or null when it has none.</param>
/// <param name="Style">Its accent, weather and light.</param>
/// <param name="Conditions">The descent conditions in force, or an empty list.</param>
public sealed record FloorChrome(
    int Number,
    string Name,
    string? Gimmick,
    FloorStyle Style,
    IReadOnlyList<DescentCondition> Conditions)
{
    /// <summary>Builds the identity for a floor definition.</summary>
    /// <param name="floor">The floor about to be played.</param>
    /// <param name="conditions">The descent conditions in force, or null for none.</param>
    /// <returns>The identity the HUD draws.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="floor"/> is null.</exception>
    public static FloorChrome For(FloorDefinition floor, IReadOnlyList<DescentCondition>? conditions = null)
    {
        ArgumentNullException.ThrowIfNull(floor);

        return new FloorChrome(
            floor.Number,
            floor.Name,
            floor.Gimmick,
            FloorStyleCatalog.For(floor.ThemeId),
            conditions ?? Array.Empty<DescentCondition>());
    }
}

/// <summary>
/// A small, mirrored mark generated from a floor's id.
/// </summary>
/// <remarks>
/// <para>
/// Twenty floors need twenty things a player can recognise before reading the name. The emblem is
/// a seven-by-seven half-grid mirrored down its centre: twenty-eight seeded bits, drawn as a
/// blocky sigil in the floor's own accent. It appears on the floor cards and in the HUD, and it
/// costs no atlas space, so a new floor gets a mark by existing.
/// </para>
/// <para>
/// This is deliberately the same idea as the compiler's <c>Sigil</c> prop, but not the same
/// function: the UI cannot reference the tool. A test pins that all twenty emblems are distinct.
/// </para>
/// </remarks>
public static class FloorEmblem
{
    /// <summary>How many cells the emblem is across.</summary>
    public const int Size = 7;

    /// <summary>Returns the emblem's seven-by-seven pattern, mirrored around its centre.</summary>
    /// <param name="themeId">The floor's theme id.</param>
    /// <returns>A grid where true is a drawn pixel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="themeId"/> is null.</exception>
    public static bool[,] Pattern(string themeId)
    {
        ArgumentNullException.ThrowIfNull(themeId);

        var hash = Hash(themeId);
        var pattern = new bool[Size, Size];

        // A frame and a spine first: every emblem has to read as a deliberate mark rather than
        // noise, then the seeded half fills in between them.
        for (var column = 0; column < Size; column++)
        {
            pattern[0, column] = true;
            pattern[Size - 1, column] = true;
        }

        for (var row = 1; row < Size - 1; row++)
        {
            pattern[row, 3] = true;
        }

        var bit = 0;

        for (var row = 1; row < Size - 1; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                if (((hash >> bit) & 1) == 1)
                {
                    pattern[row, column] = true;
                    pattern[row, Size - 1 - column] = true;
                }

                bit++;
            }
        }

        return pattern;
    }

    /// <summary>Draws the emblem with its top-left corner at a position.</summary>
    /// <param name="themeId">The floor's theme id.</param>
    /// <param name="x">The left edge in the current drawing space.</param>
    /// <param name="y">The top edge in the current drawing space.</param>
    /// <param name="cell">The side of one emblem cell in pixels.</param>
    /// <param name="accent">The floor's accent colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="themeId"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="cell"/> is not positive.</exception>
    public static void Draw(string themeId, int x, int y, int cell, Color accent)
    {
        ArgumentNullException.ThrowIfNull(themeId);

        if (cell < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), cell, "An emblem cell is at least one pixel.");
        }

        var pattern = Pattern(themeId);
        var dim = new Color((byte)(accent.R / 2), (byte)(accent.G / 2), (byte)(accent.B / 2), (byte)255);

        for (var row = 0; row < Size; row++)
        {
            for (var column = 0; column < Size; column++)
            {
                if (!pattern[row, column])
                {
                    continue;
                }

                var colour = row == 0 || row == Size - 1 || column == 3 ? dim : accent;

                Raylib.DrawRectangle(x + (column * cell), y + (row * cell), cell, cell, colour);
            }
        }
    }

    /// <summary>Draws the emblem horizontally centred on a column.</summary>
    /// <param name="themeId">The floor's theme id.</param>
    /// <param name="centreX">The centre column.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="cell">The side of one emblem cell in pixels.</param>
    /// <param name="accent">The floor's accent colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="themeId"/> is null.</exception>
    public static void DrawCentered(string themeId, int centreX, int y, int cell, Color accent) =>
        Draw(themeId, centreX - ((Size * cell) / 2), y, cell, accent);

    /// <summary>Returns the emblem's width in pixels at a cell size.</summary>
    /// <param name="cell">The side of one emblem cell in pixels.</param>
    /// <returns>The width in pixels.</returns>
    public static int Width(int cell) => Size * cell;

    private static int Hash(string text)
    {
        var hash = unchecked((int)2166136261);

        foreach (var character in text)
        {
            hash = unchecked((hash ^ character) * 16777619);
        }

        return hash & 0x7FFFFFFF;
    }
}
