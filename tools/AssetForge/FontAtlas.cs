using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.AssetForge;

/// <summary>
/// The font sheet: every printable glyph plus the two HUD icons, in one PNG beside the theme
/// atlases.
/// </summary>
/// <remarks>
/// <para>
/// The font is not a theme: the same 5x7 type serves every floor, so it is compiled once and drawn
/// with whatever tint the screen wants. Glyphs sit on a 6x8 cell — one pixel of gap right and below
/// — so a line of text is a grid of whole pixels at any integer scale, and the sprite compiler's
/// layout rule (a tile's position is a pure function of its index) still holds.
/// </para>
/// <para>
/// The two icons that the HUD needs before the pictograms exist (a full and an empty heart, and a
/// pip for the counters) ride in the same sheet: one more row, and the game needs one texture
/// instead of three.
/// </para>
/// </remarks>
public static class FontAtlas
{
    /// <summary>The sheet's id in the manifest.</summary>
    public const string Id = "font";

    /// <summary>The family string the manifest records for it.</summary>
    public const string Family = "Font";

    /// <summary>The sheet's file name.</summary>
    public const string FileName = "font.png";

    /// <summary>Width of one glyph cell, in pixels: a 5 px glyph plus a 1 px gap.</summary>
    public const int CellWidth = Glyphs.Width + 1;

    /// <summary>Height of one glyph cell, in pixels: a 7 px glyph plus a 1 px gap.</summary>
    public const int CellHeight = Glyphs.Height + 1;

    /// <summary>How many glyph cells fit on one row of the sheet.</summary>
    public const int Columns = 16;

    /// <summary>Side of the heart icons.</summary>
    public const int IconSize = 8;

    /// <summary>Compiles the sheet.</summary>
    /// <returns>The canvas, its tiles and the encoded PNG.</returns>
    public static FontSheet Compile()
    {
        var glyphs = Glyphs.Printable;
        var rows = (glyphs.Count + Columns - 1) / Columns;
        var width = Columns * CellWidth;
        var height = (rows * CellHeight) + IconSize;

        var canvas = new PixelCanvas(width, height);
        var tiles = new List<TileInfo>(glyphs.Count + 3);

        for (var index = 0; index < glyphs.Count; index++)
        {
            var character = glyphs[index];
            var x = (index % Columns) * CellWidth;
            var y = (index / Columns) * CellHeight;

            canvas.Stamp(Glyphs.Glyph(character), x, y, skipTransparent: true);
            tiles.Add(new TileInfo($"glyph-{(int)character}", x, y, CellWidth, CellHeight));
        }

        var iconY = rows * CellHeight;

        canvas.Stamp(Glyphs.Heart(full: true), 0, iconY, skipTransparent: true);
        tiles.Add(new TileInfo("heart-full", 0, iconY, IconSize, IconSize));

        canvas.Stamp(Glyphs.Heart(full: false), IconSize, iconY, skipTransparent: true);
        tiles.Add(new TileInfo("heart-empty", IconSize, iconY, IconSize, IconSize));

        canvas.Stamp(Glyphs.Pip(), 2 * IconSize, iconY, skipTransparent: true);
        tiles.Add(new TileInfo("pip", 2 * IconSize, iconY, IconSize, IconSize));

        var png = Png.Encode(width, height, canvas.ToRgba(FontPalette()));

        return new FontSheet(canvas, new ReadOnlyCollection<TileInfo>(tiles), png);
    }

    /// <summary>
    /// Returns the palette the sheet is written with: pale text, a dim variant, a hazard red and the
    /// glow, so an icon keeps its meaning without the game re-colouring it.
    /// </summary>
    /// <returns>A palette built from a neutral stone theme.</returns>
    private static Palette FontPalette() =>
        Palette.Derive(
            new Theme
            {
                Id = Id,
                Name = "Font",
                Family = TileFamily.WorkedStone,
                Hue = 200,
                AccentHue = 160,
            });
}

/// <summary>One compiled font sheet: the canvas, its glyph cells and the encoded PNG.</summary>
/// <param name="Canvas">The sheet, before palette expansion.</param>
/// <param name="Tiles">Where each glyph and icon sits.</param>
/// <param name="Png">The encoded file, ready to write.</param>
public sealed record FontSheet(PixelCanvas Canvas, IReadOnlyList<TileInfo> Tiles, byte[] Png);
