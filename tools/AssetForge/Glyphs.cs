using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.AssetForge;

/// <summary>
/// The 5x7 pixel font, plus the two HUD icons the interface needs, all hand-coded as bitmaps.
/// </summary>
/// <remarks>
/// <para>
/// A glyph is seven rows of five characters, <c>#</c> for a lit pixel and <c>.</c> for nothing, so
/// the font can be read in the file it ships in and a typo is visible as a lopsided letter rather
/// than as a silent hole. Rasterising it turns those rows into a <see cref="PixelCanvas"/> of
/// <see cref="PaletteRole.UiText"/> pixels, which the atlas writer then paints.
/// </para>
/// <para>
/// Capitals and digits all sit on the same baseline (row 6 is a capital's last row), and the
/// descenders (<c>g j p q y , ;</c>) drop into it, so a line of text has one baseline and no glyph
/// is clipped. The font is drawn at 1x for body text and twice that for headings.
/// </para>
/// </remarks>
public static class Glyphs
{
    /// <summary>The width of one glyph cell in pixels.</summary>
    public const int Width = 5;

    /// <summary>The height of one glyph cell in pixels.</summary>
    public const int Height = 7;

    private const char Lit = '#';

    private const char Blank = '.';

    /// <summary>
    /// Every glyph, as seven rows of five characters separated by <c>/</c>.
    /// </summary>
    /// <remarks>
    /// Rows are top to bottom. The table covers the whole printable ASCII range; a missing entry is
    /// a bug the caller finds immediately, because <see cref="Glyph"/> refuses to draw it.
    /// </remarks>
    private static readonly Dictionary<char, string> Shapes = new(95)
    {
        // Punctuation, digits and the first symbols.
        [' '] = "...../...../...../...../...../...../.....",
        ['!'] = "..#../..#../..#../..#../..#../...../..#..",
        ['"'] = ".#.#./.#.#./...../...../...../...../.....",
        ['#'] = ".#.#./.#.#./#####/.#.#./#####/.#.#./.#.#.",
        ['$'] = "..#../.####/#.#../.###./..#.#/####./..#..",
        ['%'] = "##.../##..#/...#./..#../.#.../#..##/...##",
        ['&'] = ".##../#..#./#.#../.#.../#.#.#/#..#./.##.#",
        ['\''] = "..#../..#../...../...../...../...../.....",
        ['('] = "...#./..#../.#.../.#.../.#.../..#../...#.",
        [')'] = ".#.../..#../...#./...#./...#./..#../.#...",
        ['*'] = "...../..#../#.#.#/.###./#.#.#/..#../.....",
        ['+'] = "...../..#../..#../#####/..#../..#../.....",
        [','] = "...../...../...../...../.##../..#../.#...",
        ['-'] = "...../...../...../#####/...../...../.....",
        ['.'] = "...../...../...../...../...../.##../.##..",
        ['/'] = "....#/...#./...#./..#../.#.../.#.../#....",
        ['0'] = ".###./#...#/#..##/#.#.#/##..#/#...#/.###.",
        ['1'] = "..#../.##../..#../..#../..#../..#../.###.",
        ['2'] = ".###./#...#/....#/...#./..#../.#.../#####",
        ['3'] = "#####/....#/...#./..##./....#/#...#/.###.",
        ['4'] = "...#./..##./.#.#./#..#./#####/...#./...#.",
        ['5'] = "#####/#..../####./....#/....#/#...#/.###.",
        ['6'] = "..##./.#.../#..../####./#...#/#...#/.###.",
        ['7'] = "#####/....#/...#./..#../.#.../.#.../.#...",
        ['8'] = ".###./#...#/#...#/.###./#...#/#...#/.###.",
        ['9'] = ".###./#...#/#...#/.####/....#/...#./.##..",
        [':'] = "...../.##../.##../...../.##../.##../.....",
        [';'] = "...../.##../.##../...../.##../..#../.#...",
        ['<'] = "...#./..#../.#.../#..../.#.../..#../...#.",
        ['='] = "...../...../#####/...../#####/...../.....",
        ['>'] = ".#.../..#../...#./....#/...#./..#../.#...",
        ['?'] = ".###./#...#/....#/...#./..#../...../..#..",
        ['@'] = ".###./#...#/#.###/#.#.#/#.###/#..../.###.",

        // Capitals, all sitting on row 6.
        ['A'] = ".###./#...#/#...#/#####/#...#/#...#/#...#",
        ['B'] = "####./#...#/#...#/####./#...#/#...#/####.",
        ['C'] = ".###./#...#/#..../#..../#..../#...#/.###.",
        ['D'] = "###../#..#./#...#/#...#/#...#/#..#./###..",
        ['E'] = "#####/#..../#..../####./#..../#..../#####",
        ['F'] = "#####/#..../#..../####./#..../#..../#....",
        ['G'] = ".###./#...#/#..../#.###/#...#/#...#/.####",
        ['H'] = "#...#/#...#/#...#/#####/#...#/#...#/#...#",
        ['I'] = ".###./..#../..#../..#../..#../..#../.###.",
        ['J'] = "..###/...#./...#./...#./...#./#..#./.##..",
        ['K'] = "#...#/#..#./#.#../##.../#.#../#..#./#...#",
        ['L'] = "#..../#..../#..../#..../#..../#..../#####",
        ['M'] = "#...#/##.##/#.#.#/#.#.#/#...#/#...#/#...#",
        ['N'] = "#...#/##..#/#.#.#/#..##/#...#/#...#/#...#",
        ['O'] = ".###./#...#/#...#/#...#/#...#/#...#/.###.",
        ['P'] = "####./#...#/#...#/####./#..../#..../#....",
        ['Q'] = ".###./#...#/#...#/#...#/#.#.#/#..#./.##.#",
        ['R'] = "####./#...#/#...#/####./#.#../#..#./#...#",
        ['S'] = ".####/#..../#..../.###./....#/....#/####.",
        ['T'] = "#####/..#../..#../..#../..#../..#../..#..",
        ['U'] = "#...#/#...#/#...#/#...#/#...#/#...#/.###.",
        ['V'] = "#...#/#...#/#...#/#...#/#...#/.#.#./..#..",
        ['W'] = "#...#/#...#/#...#/#.#.#/#.#.#/##.##/#...#",
        ['X'] = "#...#/#...#/.#.#./..#../.#.#./#...#/#...#",
        ['Y'] = "#...#/#...#/.#.#./..#../..#../..#../..#..",
        ['Z'] = "#####/....#/...#./..#../.#.../#..../#####",

        // Brackets, the rest of the symbols, and the lower case.
        ['['] = ".###./.#.../.#.../.#.../.#.../.#.../.###.",
        ['\\'] = "#..../#..../.#.../..#../...#./....#/....#",
        [']'] = ".###./...#./...#./...#./...#./...#./.###.",
        ['^'] = "..#../.#.#./#...#/...../...../...../.....",
        ['_'] = "...../...../...../...../...../...../#####",
        ['`'] = ".#.../..#../...../...../...../...../.....",
        ['a'] = "...../...../.###./....#/.####/#...#/.####",
        ['b'] = "#..../#..../####./#...#/#...#/#...#/####.",
        ['c'] = "...../...../.###./#..../#..../#..../.###.",
        ['d'] = "....#/....#/.####/#...#/#...#/#...#/.####",
        ['e'] = "...../...../.###./#...#/#####/#..../.###.",
        ['f'] = "..##./.#.../.#.../####./.#.../.#.../.#...",
        ['g'] = "...../.####/#...#/#...#/.####/....#/.###.",
        ['h'] = "#..../#..../####./#...#/#...#/#...#/#...#",
        ['i'] = "..#../...../.##../..#../..#../..#../.###.",
        ['j'] = "...#./...../..##./...#./...#./#..#./.##..",
        ['k'] = "#..../#..../#..#./#.#../##.../#.#../#..#.",
        ['l'] = ".##../..#../..#../..#../..#../..#../.###.",
        ['m'] = "...../...../##.#./#.#.#/#.#.#/#.#.#/#.#.#",
        ['n'] = "...../...../####./#...#/#...#/#...#/#...#",
        ['o'] = "...../...../.###./#...#/#...#/#...#/.###.",
        ['p'] = "...../...../####./#...#/####./#..../#....",
        ['q'] = "...../...../.####/#...#/.####/....#/....#",
        ['r'] = "...../...../#.##./##.../#..../#..../#....",
        ['s'] = "...../...../.####/#..../.###./....#/####.",
        ['t'] = ".#.../.#.../####./.#.../.#.../.#.../..##.",
        ['u'] = "...../...../#...#/#...#/#...#/#..##/.##.#",
        ['v'] = "...../...../#...#/#...#/#...#/.#.#./..#..",
        ['w'] = "...../...../#...#/#...#/#.#.#/#.#.#/.#.#.",
        ['x'] = "...../...../#...#/.#.#./..#../.#.#./#...#",
        ['y'] = "...../...../#...#/#...#/.####/....#/.###.",
        ['z'] = "...../...../#####/...#./..#../.#.../#####",
        ['{'] = "..##./.#.../.#.../##.../.#.../.#.../..##.",
        ['|'] = "..#../..#../..#../..#../..#../..#../..#..",
        ['}'] = ".##../...#./...#./...##/...#./...#./.##..",
        ['~'] = "...../...../.#..#/#.#.#/#..#./...../.....",
    };

    /// <summary>Gets every glyph this font can draw, in code-point order.</summary>
    public static IReadOnlyList<char> Printable { get; } = BuildPrintable();

    /// <summary>Draws one glyph.</summary>
    /// <param name="character">The character to draw; must be printable ASCII.</param>
    /// <returns>A 5x7 canvas of text pixels, transparent where the glyph is empty.</returns>
    /// <exception cref="ArgumentException">Thrown when the font has no glyph for the character.</exception>
    public static PixelCanvas Glyph(char character)
    {
        if (!Shapes.TryGetValue(character, out var shape))
        {
            throw new ArgumentException(
                $"The font has no glyph for '{(character < ' ' || character > '~' ? '?' : character)}' (U+{(int)character:X4}).",
                nameof(character));
        }

        var rows = shape.Split('/');

        if (rows.Length != Height)
        {
            throw new InvalidOperationException($"The glyph for '{character}' has {rows.Length} rows, not {Height}.");
        }

        var canvas = new PixelCanvas(Width, Height);
        var index = CaveTiles.Index(PaletteRole.UiText);

        for (var row = 0; row < Height; row++)
        {
            if (rows[row].Length != Width)
            {
                throw new InvalidOperationException(
                    $"Row {row} of the glyph for '{character}' is {rows[row].Length} wide, not {Width}.");
            }

            for (var column = 0; column < Width; column++)
            {
                if (rows[row][column] == Lit || rows[row][column] == Blank)
                {
                    if (rows[row][column] == Lit)
                    {
                        canvas.Set(column, row, index);
                    }

                    continue;
                }

                throw new InvalidOperationException(
                    $"The glyph for '{character}' uses '{rows[row][column]}' at {column},{row}; only '{Lit}' and '{Blank}' are allowed.");
            }
        }

        return canvas;
    }

    /// <summary>Draws a HUD heart, the icon for one of the player's lives.</summary>
    /// <param name="full">Whether the heart is still the player's, or has been spent.</param>
    /// <returns>An 8x8 heart: red when full, grey when empty.</returns>
    public static PixelCanvas Heart(bool full)
    {
        var canvas = new PixelCanvas(8, 8);
        var rows = new[]
        {
            ".##..##.",
            "########",
            "########",
            "########",
            ".######.",
            "..####..",
            "...##...",
            "........",
        };

        var index = CaveTiles.Index(full ? PaletteRole.Hazard : PaletteRole.UiDim);

        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                if (rows[row][column] == Lit)
                {
                    canvas.Set(column, row, index);
                }
            }
        }

        // A single lit pixel in the upper left is what makes a full heart read as glassy rather
        // than as a flat blob; an empty one keeps its grey so a lost life is still countable.
        if (full)
        {
            canvas.Set(1, 1, CaveTiles.Index(PaletteRole.UiText));
        }

        return canvas;
    }

    /// <summary>Draws the 4x4 pip used for keys, barriers and wisps in the HUD.</summary>
    /// <returns>A glowing pip.</returns>
    public static PixelCanvas Pip()
    {
        var canvas = new PixelCanvas(4, 4);
        var index = CaveTiles.Index(PaletteRole.Glow);

        canvas.Set(1, 0, index);
        canvas.Set(2, 0, index);
        canvas.Set(0, 1, index);
        canvas.Set(1, 1, index);
        canvas.Set(2, 1, index);
        canvas.Set(3, 1, index);
        canvas.Set(0, 2, index);
        canvas.Set(1, 2, index);
        canvas.Set(2, 2, index);
        canvas.Set(3, 2, index);
        canvas.Set(1, 3, index);
        canvas.Set(2, 3, index);

        return canvas;
    }

    /// <summary>Builds <see cref="Printable"/> from the ASCII range the font covers.</summary>
    private static ReadOnlyCollection<char> BuildPrintable()
    {
        var characters = new char['~' - ' ' + 1];

        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = (char)(' ' + index);
        }

        return new ReadOnlyCollection<char>(characters);
    }
}
