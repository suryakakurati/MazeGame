using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.AssetForge;

/// <summary>The shape language a theme's tiles are drawn in.</summary>
public enum TileFamily
{
    /// <summary>Rounded boulders, moss and drips: the cave floors.</summary>
    OrganicCave,

    /// <summary>Brick courses, chipped corners and carved bands.</summary>
    WorkedStone,

    /// <summary>Ribs, membrane and veins: the floors that are alive.</summary>
    WrongOrganic,
}

/// <summary>
/// One theme's inputs: two hues and the structural parameters that make its silhouette its own.
/// </summary>
/// <remarks>
/// <para>
/// A theme is data, not code. Twenty themes are twenty of these records, and the drawing routines
/// never branch on a theme's id — they read these numbers. That is what keeps a new floor cheap and
/// what lets <see cref="StructuralDifferences"/> prove two themes really differ rather than just
/// changing colour.
/// </para>
/// <para>
/// Only the organic-cave family is implemented in this slice; the record already carries the
/// parameters the other two families will read, so adding them does not change any caller.
/// </para>
/// </remarks>
public sealed record Theme
{
    /// <summary>Gets the stable id used in file names and seeds.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the name shown on the floor card.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the family whose drawing routines the theme uses.</summary>
    public required TileFamily Family { get; init; }

    /// <summary>Gets the base hue, in degrees, for stone and floor.</summary>
    public double Hue { get; init; }

    /// <summary>Gets the accent hue, in degrees, for props and glow.</summary>
    public double AccentHue { get; init; }

    /// <summary>Gets how rounded the boulders are, 0 (angular) to 1 (pebbles).</summary>
    public double BoulderRoundness { get; init; } = 0.5;

    /// <summary>Gets how many floor variants the atlas holds, 2 to 4.</summary>
    public int VariantCount { get; init; } = 3;

    /// <summary>Gets how tall a course of stone is, 1 to 8; only the worked-stone family reads it.</summary>
    public int CourseHeight { get; init; } = 4;

    /// <summary>Gets how thick the veins and root fibres run, 0 to 1; only the wrong-organic family reads it.</summary>
    public double VeinDensity { get; init; } = 0.5;

    /// <summary>Gets how much moss grows over the floor, 0 to 1.</summary>
    public double MossDensity { get; init; } = 0.3;

    /// <summary>Gets how much grit the floor is speckled with, 0 to 1.</summary>
    public double SpeckleRate { get; init; } = 0.2;

    /// <summary>Gets how much the tile glows, 0 to 1.</summary>
    public double GlowStrength { get; init; } = 0.5;

    /// <summary>Checks the theme can be drawn and hashed.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the id or name is blank, or a parameter is outside its range.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("A theme needs an id for file names and seeds.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("A theme needs a name for the floor card.", nameof(Name));
        }

        if (VariantCount is < 2 or > 4)
        {
            throw new ArgumentException("A theme draws between 2 and 4 floor variants.", nameof(VariantCount));
        }

        if (CourseHeight is < 1 or > 8)
        {
            throw new ArgumentException("A course of stone is between 1 and 8 px tall.", nameof(CourseHeight));
        }

        RequireFraction(BoulderRoundness, nameof(BoulderRoundness));
        RequireFraction(VeinDensity, nameof(VeinDensity));
        RequireFraction(MossDensity, nameof(MossDensity));
        RequireFraction(SpeckleRate, nameof(SpeckleRate));
        RequireFraction(GlowStrength, nameof(GlowStrength));
    }

    /// <summary>
    /// Returns how many structural parameters differ between two themes.
    /// </summary>
    /// <param name="other">The theme to compare with.</param>
    /// <returns>
    /// A count from 0 to 8. A pair of themes that only differ in hue scores 0, which is exactly
    /// what the check pass rejects.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    public int StructuralDifferences(Theme other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var count = 0;

        if (Family != other.Family)
        {
            count++;
        }

        if (Math.Abs(BoulderRoundness - other.BoulderRoundness) > 0.05)
        {
            count++;
        }

        if (VariantCount != other.VariantCount)
        {
            count++;
        }

        if (CourseHeight != other.CourseHeight)
        {
            count++;
        }

        if (Math.Abs(VeinDensity - other.VeinDensity) > 0.05)
        {
            count++;
        }

        if (Math.Abs(MossDensity - other.MossDensity) > 0.05)
        {
            count++;
        }

        if (Math.Abs(SpeckleRate - other.SpeckleRate) > 0.05)
        {
            count++;
        }

        if (Math.Abs(GlowStrength - other.GlowStrength) > 0.05)
        {
            count++;
        }

        return count;
    }

    private static void RequireFraction(double value, string name)
    {
        if (!(value >= 0 && value <= 1))
        {
            throw new ArgumentException("A structural parameter is a fraction between 0 and 1.", name);
        }
    }
}

/// <summary>The themes the game ships, one per floor of the campaign.</summary>
public static class ThemeCatalog
{
    private static readonly Theme[] Themes =
    [
        new()
        {
            Id = "shallows",
            Name = "The Shallows",
            Family = TileFamily.OrganicCave,
            Hue = 168,
            AccentHue = 320,
            BoulderRoundness = 0.25,
            VariantCount = 3,
            MossDensity = 0.35,
            SpeckleRate = 0.22,
            GlowStrength = 0.60,
        },
        new()
        {
            // Roots and knotted fibres rather than boulders: a different shape language, not a
            // different tint on the same cave.
            Id = "rootbound",
            Name = "Rootbound",
            Family = TileFamily.WrongOrganic,
            Hue = 96,
            AccentHue = 40,
            BoulderRoundness = 0.35,
            VariantCount = 3,
            MossDensity = 0.55,
            VeinDensity = 0.78,
            SpeckleRate = 0.35,
            GlowStrength = 0.35,
        },
        new()
        {
            // Flooded architecture: courses, grout and carved bands, so the floor reads as a place
            // somebody built and the sea took back.
            Id = "drowned",
            Name = "Drowned Halls",
            Family = TileFamily.WorkedStone,
            Hue = 205,
            AccentHue = 190,
            BoulderRoundness = 0.90,
            CourseHeight = 4,
            VariantCount = 4,
            MossDensity = 0.18,
            SpeckleRate = 0.12,
            GlowStrength = 0.55,
        },
    ];

    /// <summary>Gets the shipped themes, in floor order.</summary>
    public static IReadOnlyList<Theme> Shipped { get; } = new ReadOnlyCollection<Theme>(Themes);

    /// <summary>Returns the theme with <paramref name="id"/>, or null when there is none.</summary>
    /// <param name="id">The theme's stable id.</param>
    /// <returns>The matching theme, or <see langword="null"/>.</returns>
    public static Theme? ById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (var theme in Themes)
        {
            if (string.Equals(theme.Id, id, StringComparison.Ordinal))
            {
                return theme;
            }
        }

        return null;
    }
}
