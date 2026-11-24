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

    /// <summary>
    /// Draws the killer: a hooded wraith in a near-black, ragged cloak, with a bone jaw under the
    /// hood, two burning eyes and long bone claws at the ends of its sleeves.
    /// </summary>
    /// <param name="theme">The theme whose palette roles are used.</param>
    /// <param name="facing">Which way the figure looks.</param>
    /// <param name="frame">The reach pose; the claws, sleeves and hem swap between frames.</param>
    /// <returns>A 16x16 sprite.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// It used to be a bone-white stick figure, which read as a skeleton doodle rather than a
    /// threat. The cloak is now the darkest colour on the floor, so the hunter is a hole in the
    /// light shaped like a person; the only bright things on it are the eyes, the jaw and the
    /// claws, which is where a player should be looking. The window traces a red rim around it,
    /// so the dark body still reads against the dark floor.
    /// </para>
    /// <para>
    /// Every colour is a palette role, as with the rest of the cast: the cloak is
    /// <see cref="PaletteRole.WallEdge"/> with folds in <see cref="PaletteRole.FloorDark"/>, the
    /// hood's hollow is <see cref="PaletteRole.Shadow"/>, and the eyes are
    /// <see cref="PaletteRole.Hazard"/>, the palette's colour for danger.
    /// </para>
    /// </remarks>
    public static PixelCanvas Killer(Theme theme, RigFacing facing, KillerFrame frame)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (facing == RigFacing.West)
        {
            return Mirror(Killer(theme, RigFacing.East, frame));
        }

        var canvas = new PixelCanvas(Size, Size);
        var reach = frame == KillerFrame.Walk0 ? -1 : frame == KillerFrame.Walk1 ? 1 : 0;
        var cloak = Index(PaletteRole.WallEdge);
        var fold = Index(PaletteRole.FloorDark);
        var hollow = Index(PaletteRole.Shadow);
        var bone = Index(PaletteRole.Bone);
        var eye = Index(PaletteRole.Hazard);

        // The robe: a long bell that flares from the shoulders to the floor. In profile it leans
        // into the walk, wider behind than in front.
        var profile = facing == RigFacing.East;

        for (var y = 7; y <= 14; y++)
        {
            var flare = (y - 7) / 2;
            var left = profile ? 6 - flare : 6 - flare;
            var right = profile ? 9 + ((y - 7) / 3) : 10 + flare;
            canvas.Rect(left, y, right - left + 1, 1, cloak);
        }

        // A ragged hem: every other pixel hangs one row lower, and the tatters shift with the
        // step so the cloak drags rather than glides.
        var hemLeft = profile ? 2 : 2;
        var hemRight = profile ? 11 : 13;

        for (var x = hemLeft; x <= hemRight; x++)
        {
            if (((x + reach + 3) % 2) == 0)
            {
                canvas.Set(x, 15, cloak);
            }
        }

        // Two folds down the robe, so it is cloth and not a cut-out.
        canvas.Line(7, 9, 6 - (reach < 0 ? 1 : 0), 14, fold);
        canvas.Line(profile ? 9 : 10, 9, profile ? 10 : 11 + (reach > 0 ? 1 : 0), 14, fold);

        // The hood: a pointed cowl over the shoulders.
        canvas.Ellipse(8, 5, 3, 3, cloak);
        canvas.Set(8, 1, cloak);
        canvas.Rect(5, 7, 7, 1, cloak);
        canvas.Set(profile ? 6 : 8, 1, cloak);
        canvas.Set(profile ? 7 : 8, 0, cloak);

        if (facing == RigFacing.South)
        {
            // The face is a hollow in the hood with two burning eyes and a bone jaw.
            canvas.Rect(7, 4, 3, 3, hollow);
            canvas.Set(7, 4, eye);
            canvas.Set(9, 4, eye);
            canvas.Set(7, 6, bone);
            canvas.Set(9, 6, bone);
            canvas.Set(8, 7, bone);
        }
        else if (profile)
        {
            // In profile the hollow opens forward: one eye, and the jaw jutting past the cowl.
            canvas.Rect(9, 4, 2, 3, hollow);
            canvas.Set(10, 4, eye);
            canvas.Set(9, 4, eye);
            canvas.Set(10, 6, bone);
            canvas.Set(11, 6, bone);
        }
        else
        {
            // From behind: only the cowl's seam, and the pale crown of the skull through it.
            canvas.Line(8, 2, 8, 6, fold);
            canvas.Set(8, 1, bone);
        }

        // The arms: sleeves held out from the body at chest height, each ending in a spread bone
        // hand, so the figure is always reaching. In profile the leading arm grabs ahead and the
        // trailing one drags; face-on both are out wide, rising and falling with the step.
        if (profile)
        {
            canvas.Line(9, 8, 12, 8 + reach, cloak);
            canvas.Line(9, 9, 12, 9 + reach, cloak);
            Claw(canvas, 13, 8 + reach, 1, bone);

            canvas.Line(6, 8, 4, 10 - reach, cloak);
            Claw(canvas, 3, 10 - reach, -1, bone);
        }
        else
        {
            canvas.Line(5, 8, 3, 8 + reach, cloak);
            canvas.Line(5, 9, 3, 9 + reach, cloak);
            Claw(canvas, 2, 8 + reach, -1, bone);

            canvas.Line(11, 8, 13, 8 - reach, cloak);
            canvas.Line(11, 9, 13, 9 - reach, cloak);
            Claw(canvas, 14, 8 - reach, 1, bone);
        }

        return canvas;
    }

    /// <summary>Draws a bone hand at a sleeve's end: three fingers fanning outward into talons.</summary>
    /// <param name="canvas">The sprite being drawn.</param>
    /// <param name="x">The wrist's column.</param>
    /// <param name="y">The wrist's row.</param>
    /// <param name="outward">+1 when the hand opens to the right, -1 to the left.</param>
    /// <param name="bone">The palette index for the hand.</param>
    private static void Claw(PixelCanvas canvas, int x, int y, int outward, byte bone)
    {
        canvas.Set(x, y, bone);
        canvas.Set(x + outward, y - 1, bone);
        canvas.Set(x + outward, y + 1, bone);
        canvas.Set(x + outward, y, bone);
        canvas.Set(x + (2 * outward), y - 2, bone);
        canvas.Set(x + (2 * outward), y + 2, bone);
        canvas.Set(x + (2 * outward), y, bone);
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
