using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.AssetForge;

namespace MazeGame.Tests.Assets;

/// <summary>
/// Verifies the rig: every pose is drawn, facings differ, west is the mirror of east, and the props
/// animate by frame rather than by accident.
/// </summary>
public sealed class RigTests
{
    private static readonly Theme Theme = ThemeCatalog.ById("shallows")!;

    [Fact]
    public void ThePlayer_HasFourFacingsAndFourFrames()
    {
        var frames = Enum.GetValues<PlayerFrame>();
        var facings = Enum.GetValues<RigFacing>();

        Assert.Equal(4, frames.Length);
        Assert.Equal(4, facings.Length);
        Assert.Equal(frames.Length, Rig.PlayerFrameCount);

        foreach (var facing in facings)
        {
            foreach (var frame in frames)
            {
                var sprite = Rig.Player(Theme, facing, frame);

                Assert.Equal(CaveTiles.TileSize, sprite.Width);
                Assert.Equal(CaveTiles.TileSize, sprite.Height);
                Assert.True(sprite.DrawnPixels() > 40, $"{facing}/{frame} is nearly empty");
            }
        }
    }

    [Fact]
    public void ThePlayersFourFacings_AreFourDifferentSprites()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var facing in Enum.GetValues<RigFacing>())
        {
            Assert.True(seen.Add(Pixels(Rig.Player(Theme, facing, PlayerFrame.Idle))), $"{facing} duplicates another facing");
        }
    }

    [Fact]
    public void FacingWest_IsMirroredEast()
    {
        var east = Rig.Player(Theme, RigFacing.East, PlayerFrame.Walk0);
        var west = Rig.Player(Theme, RigFacing.West, PlayerFrame.Walk0);

        for (var row = 0; row < CaveTiles.TileSize; row++)
        {
            for (var column = 0; column < CaveTiles.TileSize; column++)
            {
                Assert.Equal(east[CaveTiles.TileSize - 1 - column, row], west[column, row]);
            }
        }
    }

    [Fact]
    public void TheWalkFrames_DifferFromIdle()
    {
        var idle = Pixels(Rig.Player(Theme, RigFacing.South, PlayerFrame.Idle));

        foreach (var frame in new[] { PlayerFrame.Walk0, PlayerFrame.Walk1, PlayerFrame.Walk2 })
        {
            Assert.NotEqual(idle, Pixels(Rig.Player(Theme, RigFacing.South, frame)));
        }
    }

    [Fact]
    public void TheKiller_HasFramesAndFacings()
    {
        Assert.Equal(3, Rig.KillerFrameCount);

        var a = Pixels(Rig.Killer(Theme, RigFacing.East, KillerFrame.Walk0));
        var b = Pixels(Rig.Killer(Theme, RigFacing.East, KillerFrame.Walk1));

        Assert.NotEqual(a, b);
        Assert.True(Rig.Killer(Theme, RigFacing.South, KillerFrame.Idle).DrawnPixels() > 30);
    }

    [Fact]
    public void TheProps_AnimateByFrame()
    {
        var keyFrames = Enumerable.Range(0, 3).Select(frame => Pixels(Rig.Key(Theme, frame))).ToList();
        var wispFrames = Enumerable.Range(0, 3).Select(frame => Pixels(Rig.Wisp(Theme, frame))).ToList();

        Assert.Equal(3, keyFrames.Distinct().Count());
        Assert.Equal(3, wispFrames.Distinct().Count());

        Assert.NotEqual(Pixels(Rig.Barrier(Theme, cracked: false)), Pixels(Rig.Barrier(Theme, cracked: true)));
        Assert.NotEqual(Pixels(Rig.Arch(Theme, open: false)), Pixels(Rig.Arch(Theme, open: true)));
    }

    [Fact]
    public void TheProps_RejectFramesOutsideTheirRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rig.Key(Theme, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rig.Wisp(Theme, -1));
        Assert.Throws<ArgumentNullException>(() => Rig.Key(null!, 0));
        Assert.Throws<ArgumentNullException>(() => Rig.Player(null!, RigFacing.South, PlayerFrame.Idle));
    }

    [Fact]
    public void EveryPose_IsReproducible()
    {
        Assert.Equal(
            Pixels(Rig.Player(Theme, RigFacing.North, PlayerFrame.Walk1)),
            Pixels(Rig.Player(Theme, RigFacing.North, PlayerFrame.Walk1)));
    }

    [Fact]
    public void TheCastIsDrawnInTheThemesOwnColours()
    {
        // The rig writes roles, not RGB: the same pose under two themes must differ, which is what
        // proves a new theme needs no new sprite code.
        var other = ThemeCatalog.ById("drowned")!;

        Assert.NotEqual(
            Pixels(Rig.Player(Theme, RigFacing.South, PlayerFrame.Idle), Theme),
            Pixels(Rig.Player(other, RigFacing.South, PlayerFrame.Idle), other));
    }

    private static string Pixels(PixelCanvas canvas) => Pixels(canvas, Theme);

    private static string Pixels(PixelCanvas canvas, Theme theme) =>
        Convert.ToBase64String(canvas.ToRgba(Palette.Derive(theme)));
}
