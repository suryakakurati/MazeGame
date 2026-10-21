using System;
using System.Collections.Generic;
using Raylib_cs;

namespace MazeGame.UI.Rendering.Pixels;

/// <summary>
/// The compiled bitmap font: a string is a row of 5x7 glyph cells drawn at an integer scale, so it
/// stays one crisp pixel grid at every size.
/// </summary>
/// <remarks>
/// <para>
/// The sheet is compiled by <c>tools/AssetForge</c> next to the theme atlases and loaded from the
/// same manifest, so text and art cannot drift apart. A glyph cell is 6x8 — the 5x7 shape plus a
/// row and a column of gap — which makes the advance and the line height whole pixels.
/// </para>
/// <para>
/// It draws where the caller is drawing: inside the canvas for a HUD, or in window pixels after
/// the blit for the campaign cards. Nothing here knows the screen size.
/// </para>
/// <para>
/// A character the font does not have is drawn as <c>?</c> rather than throwing: the text comes
/// from game data, and a stray symbol in a floor's name should not crash a run mid-frame.
/// </para>
/// </remarks>
public sealed class PixelFont : IDisposable
{
    /// <summary>The font atlas's id in the manifest.</summary>
    public const string AtlasId = "font";

    /// <summary>How far one character advances, in font pixels.</summary>
    public const int Advance = 6;

    /// <summary>The height of a line, in font pixels.</summary>
    public const int LineHeight = 8;

    /// <summary>The side of a HUD icon (heart, pip), in font pixels.</summary>
    public const int IconSize = 8;

    private const char Fallback = '?';

    private readonly Texture2D _texture;

    private readonly ThemeAtlas _info;

    /// <summary>Loads the sheet described by <paramref name="info"/>.</summary>
    /// <param name="info">The manifest entry for the font atlas.</param>
    /// <param name="directory">The generated folder the PNG sits in.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public PixelFont(ThemeAtlas info, string directory)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(directory);

        _info = info;
        _texture = Raylib.LoadTexture(System.IO.Path.Combine(directory, info.FileName));
        Raylib.SetTextureFilter(_texture, TextureFilter.Point);
    }

    /// <summary>Loads the font the game ships with.</summary>
    /// <returns>The loaded sheet.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the manifest has no font atlas.</exception>
    /// <exception cref="System.IO.FileNotFoundException">Thrown when the generated folder is missing.</exception>
    public static PixelFont LoadShipped()
    {
        var atlases = AtlasManifest.LoadShipped();

        if (!atlases.TryGetValue(AtlasId, out var info))
        {
            throw new KeyNotFoundException(
                $"The manifest has no '{AtlasId}' atlas; re-run tools/AssetForge and commit the output.");
        }

        return new PixelFont(info, AssetPaths.GeneratedDirectory());
    }

    /// <summary>Gets the atlas texture, for tests and the harness.</summary>
    public Texture2D Texture => _texture;

    /// <summary>Returns how wide <paramref name="text"/> draws, in pixels, ignoring the trailing gap.</summary>
    /// <param name="text">The text to measure; null measures 0.</param>
    /// <param name="scale">The integer scale.</param>
    /// <returns>The width in pixels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scale"/> is not positive.</exception>
    public static int MeasureWidth(string? text, int scale)
    {
        RequireScale(scale);

        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return ((text.Length * Advance) - 1) * scale;
    }

    /// <summary>Draws text with its top-left corner at a position.</summary>
    /// <param name="text">The text; null or empty draws nothing.</param>
    /// <param name="x">The left edge in the current drawing space.</param>
    /// <param name="y">The top edge in the current drawing space.</param>
    /// <param name="scale">The integer scale.</param>
    /// <param name="tint">A multiplier colour; <see cref="Color.White"/> draws the sheet as compiled.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scale"/> is not positive.</exception>
    public void Draw(string? text, int x, int y, int scale, Color tint)
    {
        RequireScale(scale);

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        for (var index = 0; index < text.Length; index++)
        {
            DrawGlyph(text[index], x + (index * Advance * scale), y, scale, tint);
        }
    }

    /// <summary>Draws text horizontally centred on a position.</summary>
    /// <param name="text">The text; null or empty draws nothing.</param>
    /// <param name="centreX">The centre column.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="scale">The integer scale.</param>
    /// <param name="tint">A multiplier colour.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scale"/> is not positive.</exception>
    public void DrawCentered(string? text, int centreX, int y, int scale, Color tint) =>
        Draw(text, centreX - (MeasureWidth(text, scale) / 2), y, scale, tint);

    /// <summary>Draws one glyph cell at a position, using the fallback for an unknown character.</summary>
    /// <param name="character">The character to draw.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="scale">The integer scale.</param>
    /// <param name="tint">A multiplier colour.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scale"/> is not positive.</exception>
    public void DrawGlyph(char character, int x, int y, int scale, Color tint)
    {
        RequireScale(scale);

        var name = $"glyph-{(int)character}";

        if (!_info.Tiles.ContainsKey(name))
        {
            name = $"glyph-{(int)Fallback}";
        }

        DrawCell(name, x, y, Advance, LineHeight, scale, tint);
    }

    /// <summary>Draws a HUD icon — <c>heart-full</c>, <c>heart-empty</c> or <c>pip</c>.</summary>
    /// <param name="name">The icon's manifest name.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="scale">The integer scale.</param>
    /// <param name="tint">A multiplier colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the sheet has no such icon.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scale"/> is not positive.</exception>
    public void DrawIcon(string name, int x, int y, int scale, Color tint)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!_info.Tiles.ContainsKey(name))
        {
            throw new KeyNotFoundException($"The font sheet has no icon called '{name}'.");
        }

        DrawCell(name, x, y, IconSize, IconSize, scale, tint);
    }

    /// <summary>Releases the texture.</summary>
    public void Dispose() => Raylib.UnloadTexture(_texture);

    private void DrawCell(string name, int x, int y, int width, int height, int scale, Color tint)
    {
        var tile = _info.Tile(name);

        Raylib.DrawTexturePro(
            _texture,
            new Rectangle(tile.X, tile.Y, tile.Width, tile.Height),
            new Rectangle(x, y, width * scale, height * scale),
            System.Numerics.Vector2.Zero,
            0f,
            tint);
    }

    private static void RequireScale(int scale)
    {
        if (scale < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "A glyph scale is at least 1.");
        }
    }
}
