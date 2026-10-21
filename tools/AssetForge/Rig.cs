using System;

namespace MazeGame.AssetForge;

/// <summary>Which way a figure faces.</summary>
public enum RigFacing
{
    /// <summary>Facing away from the viewer.</summary>
    North,

    /// <summary>Facing right.</summary>
    East,

    /// <summary>Facing the viewer.</summary>
    South,

    /// <summary>Facing left.</summary>
    West,
}

/// <summary>The player's animation frames.</summary>
public enum PlayerFrame
{
    /// <summary>Standing still.</summary>
    Idle,

    /// <summary>First walking frame, weight on the left foot.</summary>
    Walk0,

    /// <summary>Passing pose.</summary>
    Walk1,

    /// <summary>Second walking frame, weight on the right foot.</summary>
    Walk2,
}

/// <summary>The killer's animation frames.</summary>
public enum KillerFrame
{
    /// <summary>Standing still, limbs drawn in.</summary>
    Idle,

    /// <summary>Reaching with the left arm.</summary>
    Walk0,

    /// <summary>Reaching with the right arm.</summary>
    Walk1,
}

/// <summary>
/// Draws the cast: the explorer, the killer, and the four props, all 16x16 and all from palette
/// roles.
/// </summary>
/// <remarks>
/// <para>
/// Four facings come from two drawings plus a mirror: east is drawn, west is east mirrored, and
/// north/south differ only in whether the face is visible. The walk cycle is three poses on one
/// body, so a new theme changes the palette and nothing here.
/// </para>
/// <para>
/// Nothing is coloured by name: every routine writes a <see cref="PaletteRole"/>, which is what
/// lets the same rig serve all twenty planned themes.
/// </para>
/// </remarks>
public static class Rig
{
    private const int Size = CaveTiles.TileSize;

    /// <summary>Gets how many frames each player animation has.</summary>
    public static int PlayerFrameCount => Enum.GetValues<PlayerFrame>().Length;

    /// <summary>Gets how many frames the killer has.</summary>
    public static int KillerFrameCount => Enum.GetValues<KillerFrame>().Length;

    /// <summary>Draws the explorer, lantern in hand.</summary>
    /// <param name="theme">The theme whose palette roles are used.</param>
    /// <param name="facing">Which way the figure looks.</param>
    /// <param name="frame">The walk-cycle pose.</param>
    /// <returns>A 16x16 sprite.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Player(Theme theme, RigFacing facing, PlayerFrame frame)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (facing == RigFacing.West)
        {
            return Mirror(Player(theme, RigFacing.East, frame));
        }

        var canvas = new PixelCanvas(Size, Size);
        var step = frame switch
        {
            PlayerFrame.Walk0 => -1,
            PlayerFrame.Walk2 => 1,
            _ => 0,
        };

        // Cloak first, then the hood rim, so the rim reads as a lit edge rather than a hat.
        canvas.Ellipse(8, 10 + step, 4, 5, Index(PaletteRole.Flesh));
        canvas.Ellipse(8, 15, 4, 1, Index(PaletteRole.Shadow));

        var hoodY = 5 + step;
        canvas.Ellipse(8, hoodY, 3, 3, Index(PaletteRole.Flesh));
        canvas.Line(5, hoodY + 2, 11, hoodY + 2, Index(PaletteRole.Bone));

        if (facing is RigFacing.South)
        {
            // Two dark eyes in the hood: the only time the explorer has a face.
            canvas.Set(7, hoodY + 1, Index(PaletteRole.Shadow));
            canvas.Set(9, hoodY + 1, Index(PaletteRole.Shadow));
        }

        // The lantern hangs on the leading side and swings with the step. The passing pose (Walk1)
        // sits at the same height as Idle, so it swings the lantern inward instead — otherwise
        // two frames of the cycle would be the same drawing.
        var swing = frame == PlayerFrame.Walk1 ? 1 : 0;
        var lanternX = (facing == RigFacing.North ? 4 : 12) - swing;
        var lanternY = 10 + (step == 0 ? 0 : -step) + swing;
        canvas.Line(11, 9 + step, lanternX, lanternY - 1, Index(PaletteRole.WallEdge));
        canvas.Rect(lanternX - 1, lanternY, 3, 3, Index(PaletteRole.Glow));
        canvas.Set(lanternX, lanternY + 1, Index(PaletteRole.UiText));

        return canvas;
    }

    /// <summary>Draws the killer: long limbs, small head, two bright eyes.</summary>
    /// <param name="theme">The theme whose palette roles are used.</param>
    /// <param name="facing">Which way the figure looks.</param>
    /// <param name="frame">The reach pose; the arms and legs swap between frames.</param>
    /// <returns>A 16x16 sprite.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Killer(Theme theme, RigFacing facing, KillerFrame frame)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (facing == RigFacing.West)
        {
            return Mirror(Killer(theme, RigFacing.East, frame));
        }

        var canvas = new PixelCanvas(Size, Size);
        var reach = frame == KillerFrame.Walk0 ? -1 : frame == KillerFrame.Walk1 ? 1 : 0;

        canvas.Rect(7, 5, 2, 7, Index(PaletteRole.Bone));
        canvas.Ellipse(8, 3, 2, 2, Index(PaletteRole.Bone));

        // Ribs: three dark gaps, so the torso is not a white post.
        canvas.Rect(7, 7, 2, 1, Index(PaletteRole.WallEdge));
        canvas.Rect(7, 9, 2, 1, Index(PaletteRole.WallEdge));

        canvas.Line(7, 6, 3, 9 + reach, Index(PaletteRole.Bone));
        canvas.Line(8, 6, 13, 9 - reach, Index(PaletteRole.Bone));
        canvas.Line(7, 12, 5 - reach, 15, Index(PaletteRole.Bone));
        canvas.Line(8, 12, 10 + reach, 15, Index(PaletteRole.Bone));

        if (facing != RigFacing.North)
        {
            var eyeY = 3;
            canvas.Set(facing == RigFacing.South ? 7 : 8, eyeY, Index(PaletteRole.Glow));
            canvas.Set(facing == RigFacing.South ? 9 : 9, eyeY, Index(PaletteRole.Glow));
        }

        return canvas;
    }

    /// <summary>Draws the key: a glowing cap on a pale stem, bobbing over three frames.</summary>
    /// <param name="theme">The theme whose palette roles are used.</param>
    /// <param name="frame">0..2; the cap bobs and the glow pulses.</param>
    /// <returns>A 16x16 sprite.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame is outside 0..2.</exception>
    public static PixelCanvas Key(Theme theme, int frame)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (frame is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), frame, "The key has three frames.");
        }

        var canvas = new PixelCanvas(Size, Size);
        var bob = frame == 1 ? -1 : 0;
        var centreY = 7 + bob;

        canvas.Ellipse(8, 15, 4, 1, Index(PaletteRole.Shadow));
        canvas.Rect(8, centreY + 3, 1, 5, Index(PaletteRole.Bone));
        canvas.Ellipse(8, centreY, frame == 2 ? 4 : 3, frame == 2 ? 2 : 3, Index(PaletteRole.Glow));
        canvas.Ellipse(8, centreY, 2, 2, Index(PaletteRole.Accent));
        canvas.Set(8, centreY, Index(PaletteRole.UiText));

        return canvas;
    }

    /// <summary>Draws the wisp: a small flame that flickers over three frames.</summary>
    /// <param name="theme">The theme whose palette roles are used.</param>
    /// <param name="frame">0..2; the flame narrows and widens.</param>
    /// <returns>A 16x16 sprite.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame is outside 0..2.</exception>
    public static PixelCanvas Wisp(Theme theme, int frame)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (frame is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), frame, "The wisp has three frames.");
        }

        var canvas = new PixelCanvas(Size, Size);

        canvas.Ellipse(8, 14, 3, 1, Index(PaletteRole.Shadow));
        canvas.Ellipse(8, 8, frame == 2 ? 4 : 3, frame == 1 ? 5 : 4, Index(PaletteRole.Glow));
        canvas.Ellipse(8, 9, 2, 2, Index(PaletteRole.Accent));
        canvas.Line(8, 3 + (frame == 1 ? 1 : 0), 8, 5, Index(PaletteRole.Glow));
        canvas.Set(8, 8, Index(PaletteRole.UiText));

        return canvas;
    }

    /// <summary>Draws a placed barrier: a slab that cracks as its seal ages.</summary>
    /// <param name="theme">The theme whose palette roles are used.</param>
    /// <param name="cracked">Whether the seal has begun to fail.</param>
    /// <returns>A 16x16 sprite.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Barrier(Theme theme, bool cracked)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var canvas = new PixelCanvas(Size, Size);

        canvas.Rect(2, 1, 12, 14, Index(PaletteRole.Wall));
        canvas.Rect(2, 1, 12, 1, Index(PaletteRole.WallTop));
        canvas.Rect(2, 14, 12, 1, Index(PaletteRole.WallEdge));
        canvas.Dither(4, 3, 8, 9, Index(PaletteRole.Moss), 0);

        if (cracked)
        {
            canvas.Line(5, 6, 9, 9, Index(PaletteRole.WallEdge));
            canvas.Line(9, 9, 7, 13, Index(PaletteRole.WallEdge));
            canvas.Line(6, 11, 3, 13, Index(PaletteRole.WallEdge));
            canvas.Set(9, 6, Index(PaletteRole.Hazard));
            canvas.Set(11, 11, Index(PaletteRole.Hazard));
        }
        else
        {
            canvas.Line(4, 4, 4, 12, Index(PaletteRole.Accent));
            canvas.Set(8, 6, Index(PaletteRole.Glow));
        }

        return canvas;
    }

    /// <summary>Draws the arch that ends a floor, locked or thrown open.</summary>
    /// <param name="theme">The theme whose palette roles are used.</param>
    /// <param name="open">Whether the floor's keys have opened it.</param>
    /// <returns>A 16x16 sprite.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Arch(Theme theme, bool open)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var canvas = new PixelCanvas(Size, Size);

        canvas.Rect(2, 1, 12, 14, Index(PaletteRole.WallEdge));
        canvas.Ellipse(8, 12, 5, 7, Index(open ? PaletteRole.Glow : PaletteRole.Wall));
        canvas.Rect(3, 9, 10, 6, Index(open ? PaletteRole.Glow : PaletteRole.Wall));

        canvas.Line(2, 15, 2, 6, Index(PaletteRole.Bone));
        canvas.Line(13, 15, 13, 6, Index(PaletteRole.Bone));
        canvas.Ellipse(8, 5, 6, 2, Index(PaletteRole.Bone));

        if (open)
        {
            canvas.Ellipse(8, 11, 3, 5, Index(PaletteRole.Accent));
            canvas.Set(8, 8, Index(PaletteRole.UiText));
        }
        else
        {
            canvas.Rect(7, 9, 2, 2, Index(PaletteRole.Hazard));
            canvas.Dither(4, 5, 8, 3, Index(PaletteRole.WallTop), 1);
        }

        return canvas;
    }

    private static byte Index(PaletteRole role) => CaveTiles.Index(role);

    private static PixelCanvas Mirror(PixelCanvas source)
    {
        var canvas = new PixelCanvas(source.Width, source.Height);
        canvas.Stamp(source, 0, 0, skipTransparent: false);
        canvas.Mirror();
        return canvas;
    }
}
