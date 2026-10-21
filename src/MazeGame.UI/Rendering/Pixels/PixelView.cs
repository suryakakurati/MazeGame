using System;
using System.Numerics;
using Raylib_cs;

namespace MazeGame.UI.Rendering.Pixels;

/// <summary>
/// The virtual canvas: everything the pixel renderer draws goes into a render texture at atlas
/// resolution, which is then blitted to the window at an integer scale.
/// </summary>
/// <remarks>
/// Drawing into a fixed-size target is what keeps the art crisp and the layout independent of the
/// window: a resize changes only <see cref="PixelLayout.PixelFit"/>, never a coordinate inside the game. The
/// texture filter is point for that reason — a linear filter on a 2x blit would smear every tile
/// edge into its neighbour.
/// </remarks>
internal sealed class PixelView : IDisposable
{
    private readonly RenderTexture2D _target;

    /// <summary>Creates the canvas.</summary>
    /// <param name="width">Canvas width in atlas pixels.</param>
    /// <param name="height">Canvas height in atlas pixels.</param>
    /// <exception cref="InvalidOperationException">Thrown when the window is not ready.</exception>
    public PixelView(int width, int height)
    {
        if (!Raylib.IsWindowReady())
        {
            throw new InvalidOperationException("PixelView must be created after Raylib.InitWindow.");
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"A canvas needs a positive size, not {width}x{height}.");
        }

        Width = width;
        Height = height;
        _target = Raylib.LoadRenderTexture(width, height);
        Raylib.SetTextureFilter(_target.Texture, TextureFilter.Point);
    }

    /// <summary>Gets the canvas width in atlas pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the canvas height in atlas pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the canvas texture, for tests and for the harness to sample.</summary>
    public Texture2D Texture => _target.Texture;

    /// <summary>Starts drawing into the canvas, clearing it to near-black cave darkness.</summary>
    /// <param name="background">The colour to clear to.</param>
    public void Begin(Color background)
    {
        Raylib.BeginTextureMode(_target);
        Raylib.ClearBackground(background);
    }

    /// <summary>Finishes the canvas pass.</summary>
    /// <remarks>Static because it only closes the render-target pass that <c>Begin</c> opened.</remarks>
    public static void End() => Raylib.EndTextureMode();

    /// <summary>
    /// Draws the canvas into the window at an integer scale, centred between the two HUD strips.
    /// </summary>
    /// <param name="topInset">Screen pixels reserved at the top.</param>
    /// <param name="bottomInset">Screen pixels reserved at the bottom.</param>
    /// <param name="margin">Screen pixels to keep free around the canvas.</param>
    /// <param name="backdrop">The colour the letterbox is cleared to, for the floor's ambience.</param>
    /// <returns>Where the canvas landed.</returns>
    public PixelLayout.PixelFit Present(int topInset, int bottomInset, int margin, Color backdrop)
    {
        var fit = PixelLayout.Fit(Width, Height, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), topInset, bottomInset, margin);

        // The letterbox is cleared here rather than by the caller: a frame that only paints inside
        // the canvas would otherwise leave the previous frame's margins on screen.
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), backdrop);

        Raylib.DrawTexturePro(
            _target.Texture,
            new Rectangle(0, 0, Width, -Height),
            new Rectangle(fit.X, fit.Y, fit.Width, fit.Height),
            Vector2.Zero,
            0f,
            Color.White);

        return fit;
    }

    /// <summary>Releases the render texture.</summary>
    public void Dispose() => Raylib.UnloadRenderTexture(_target);
}
