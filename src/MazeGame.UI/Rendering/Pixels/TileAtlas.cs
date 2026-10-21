using System;
using System.Numerics;
using Raylib_cs;

namespace MazeGame.UI.Rendering.Pixels;

/// <summary>
/// One theme's atlas as a texture, plus the named lookup that finds a tile inside it.
/// </summary>
/// <remarks>
/// A missing tile throws: <see cref="ThemeAtlas.Tile"/> is the single place that decides a name is
/// unknown, so a compiler or theme-table mistake fails at the first frame that needs the art rather
/// than painting an invisible or magenta square.
/// </remarks>
internal sealed class TileAtlas : IDisposable
{
    private readonly Texture2D _texture;
    private readonly ThemeAtlas _info;

    /// <summary>Loads a theme's atlas.</summary>
    /// <param name="info">The manifest entry describing it.</param>
    /// <param name="directory">The generated folder the PNG sits in.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public TileAtlas(ThemeAtlas info, string directory)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(directory);

        _info = info;
        _texture = Raylib.LoadTexture(System.IO.Path.Combine(directory, info.FileName));
        Raylib.SetTextureFilter(_texture, TextureFilter.Point);
    }

    /// <summary>Gets the theme's id.</summary>
    public string ThemeId => _info.ThemeId;

    /// <summary>Gets how many floor variants the atlas draws.</summary>
    public int FloorVariantCount => Math.Max(1, _info.FloorVariantCount);

    /// <summary>Gets how many <c>prop-n</c> tiles the theme compiled, or 0 when it has none.</summary>
    public int PropCount
    {
        get
        {
            var count = 0;

            while (_info.Tiles.ContainsKey($"prop-{count}"))
            {
                count++;
            }

            return count;
        }
    }

    /// <summary>
    /// Gets how many scenery props the floor scatters: every prop except the last, which is the
    /// floor's emblem and is placed deliberately rather than at the scenery rate.
    /// </summary>
    public int SceneryPropCount => Math.Max(0, PropCount - 1);

    /// <summary>Returns whether the atlas contains a tile with this name.</summary>
    /// <param name="name">The tile's manifest name.</param>
    /// <returns><see langword="true"/> when it can be drawn.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public bool Has(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _info.Tiles.ContainsKey(name);
    }

    /// <summary>Gets the atlas texture, for tests and the harness.</summary>
    public Texture2D Texture => _texture;

    /// <summary>Draws one tile at a canvas position.</summary>
    /// <param name="name">The tile's manifest name.</param>
    /// <param name="x">The left edge in canvas pixels.</param>
    /// <param name="y">The top edge in canvas pixels.</param>
    /// <param name="tint">A multiplier colour; <see cref="Color.White"/> draws the tile as compiled.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when the theme has no such tile.</exception>
    public void Draw(string name, int x, int y, Color tint)
    {
        var tile = _info.Tile(name);

        Raylib.DrawTextureRec(
            _texture,
            new Rectangle(tile.X, tile.Y, tile.Width, tile.Height),
            new Vector2(x, y),
            tint);
    }

    /// <summary>Draws one tile as compiled.</summary>
    /// <param name="name">The tile's manifest name.</param>
    /// <param name="x">The left edge in canvas pixels.</param>
    /// <param name="y">The top edge in canvas pixels.</param>
    public void Draw(string name, int x, int y) => Draw(name, x, y, Color.White);

    /// <summary>Releases the texture.</summary>
    public void Dispose() => Raylib.UnloadTexture(_texture);
}
