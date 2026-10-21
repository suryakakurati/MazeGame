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
/// The pattern a floor's walkable surface is drawn in.
/// </summary>
/// <remarks>
/// The family supplies the three older grammars (boulders, flagstones, roots); these motifs are
/// the rest of the palette of places, so a wet floor, a bone floor, a metal floor and a woven one
/// cannot be mistaken for each other even in a screenshot cropped to a single tile.
/// </remarks>
public enum FloorMotif
{
    /// <summary>Rounded stones on grit: the cave family's floor.</summary>
    Boulders,

    /// <summary>Laid flagstones with offset joints: the worked-stone family's floor.</summary>
    Flagstones,

    /// <summary>Matted fibres and knots: the wrong-organic family's floor.</summary>
    Roots,

    /// <summary>Long horizontal water lines and glints.</summary>
    Ripples,

    /// <summary>Wind-blown ash, sand or snow drifting in one direction.</summary>
    Drift,

    /// <summary>Small square tesserae with grout between them.</summary>
    Mosaic,

    /// <summary>Long bones and joints laid across dark ground.</summary>
    Bones,

    /// <summary>Angular crystal shards catching the light.</summary>
    Shards,

    /// <summary>Radial silk with dew, spun from one corner.</summary>
    Web,

    /// <summary>Riveted metal plates with rust in the seams.</summary>
    Plates,

    /// <summary>Soft mire, blobs and bubbles.</summary>
    Mire,

    /// <summary>Iron grate slits with something glowing underneath.</summary>
    Vents,
}

/// <summary>
/// The pattern a wall's face is drawn in.
/// </summary>
/// <remarks>
/// Walls are five-pixel bands, so this pattern is read at a glance: vertical ribs, horizontal
/// plates, a woven mat, stacked bone, or a grate. It is deliberately independent of
/// <see cref="FloorMotif"/>; a floor of bone under a wall of iron is a different place again.
/// </remarks>
public enum WallMotif
{
    /// <summary>Mossy rock with a lit ridge: the cave family's wall.</summary>
    MossRock,

    /// <summary>Cut blocks in courses: the worked-stone family's wall.</summary>
    CutBlock,

    /// <summary>Knotted root mass: the wrong-organic family's wall.</summary>
    WovenRoot,

    /// <summary>Vertical ribs with a membrane between them.</summary>
    Ribbed,

    /// <summary>Horizontal metal plates and rivets.</summary>
    StackedPlate,

    /// <summary>Faceted crystal with bright edges.</summary>
    Crystal,

    /// <summary>Woven silk threads with hanging strands.</summary>
    Silk,

    /// <summary>Stacked long bones with dark joints.</summary>
    Bone,

    /// <summary>Iron bars with a glow behind them.</summary>
    Grate,

    /// <summary>Pale, cracked ice.</summary>
    Frost,
}

/// <summary>
/// The one large object a floor is remembered by: its signature tile.
/// </summary>
/// <remarks>
/// A theme's floor and wall patterns make a place; its feature makes a postcard. One of these sits
/// in a handful of cells on its floor, and the same shape is the floor's emblem on the cards.
/// </remarks>
public enum FeatureKind
{
    /// <summary>A still pool with a stone rim.</summary>
    Pool,

    /// <summary>A nest of fibres holding pale eggs.</summary>
    Nest,

    /// <summary>A carved rune with a cold light in it.</summary>
    Rune,

    /// <summary>An iron grate with embers below.</summary>
    Grate,

    /// <summary>A stopped clock face.</summary>
    Clock,

    /// <summary>A standing mirror of black glass.</summary>
    Mirror,

    /// <summary>A cluster of tall crystals.</summary>
    Crystal,

    /// <summary>A dense corner web with dew.</summary>
    Web,

    /// <summary>A pile of bones and a small skull.</summary>
    Bones,

    /// <summary>A wax seal with a ribbon.</summary>
    Seal,

    /// <summary>A beating heart, veined and wet.</summary>
    Heart,
}

/// <summary>
/// What drifts through a floor's air.
/// </summary>
/// <remarks>
/// The renderer turns this into a small particle layer. It is presentation, not a rule, so it lives
/// with the theme rather than in Core: a blind floor and a bright one can share a maze and still
/// feel like different weather.
/// </remarks>
public enum AmbientKind
{
    /// <summary>Lantern-lit spores that hang and drift.</summary>
    Motes,

    /// <summary>Bubbles rising through standing water.</summary>
    Bubbles,

    /// <summary>Spores that pulse and settle.</summary>
    Spores,

    /// <summary>Embers rising and fading.</summary>
    Embers,

    /// <summary>Water falling in short, fast drops.</summary>
    Drips,

    /// <summary>Slow dust turning in the light.</summary>
    Dust,

    /// <summary>Moths fluttering in irregular arcs.</summary>
    Moths,

    /// <summary>Petals or leaves tumbling down.</summary>
    Petals,

    /// <summary>Ash falling in a steady drift.</summary>
    Ash,

    /// <summary>Rain streaks driven at an angle.</summary>
    Rain,

    /// <summary>A restless static that flickers in place.</summary>
    Static,

    /// <summary>Fireflies that wander and blink.</summary>
    Fireflies,
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

    /// <summary>Gets the pattern the walkable floor is drawn in.</summary>
    public FloorMotif Floor { get; init; } = FloorMotif.Boulders;

    /// <summary>Gets the pattern the wall band is drawn in.</summary>
    public WallMotif Wall { get; init; } = WallMotif.MossRock;

    /// <summary>Gets the floor's signature object, scattered a few times per maze.</summary>
    public FeatureKind Feature { get; init; } = FeatureKind.Pool;

    /// <summary>Gets what drifts through this floor's air.</summary>
    public AmbientKind Ambient { get; init; } = AmbientKind.Motes;

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

        if (Floor != other.Floor)
        {
            count++;
        }

        if (Wall != other.Wall)
        {
            count++;
        }

        if (Feature != other.Feature)
        {
            count++;
        }

        if (Ambient != other.Ambient)
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

/// <summary>
/// The themes the game ships, one per floor of the campaign: twenty places, three shape languages.
/// </summary>
/// <remarks>
/// The order matches <see cref="MazeGame.Core.Game.FloorCatalog"/> — theme index n is floor n + 1 —
/// and <c>--check</c> holds two promises about it: neighbouring themes are far enough apart in
/// colour, and they differ in at least two structural parameters, so a new floor cannot ship as a
/// hue swap of the one before it.
/// </remarks>
public static class ThemeCatalog
{
    private static readonly Theme[] Themes =
    [
        new()
        {
            Id = "shallows",
            Name = "The Shallows",
            // Rounded boulders, moss and drifting motes: the floor that teaches the loop.
            Family = TileFamily.OrganicCave,
            Floor = FloorMotif.Boulders,
            Wall = WallMotif.MossRock,
            Feature = FeatureKind.Pool,
            Ambient = AmbientKind.Motes,
            Hue = 168,
            AccentHue = 320,
            BoulderRoundness = 0.25,
            VariantCount = 3,
            CourseHeight = 4,
            VeinDensity = 0.50,
            MossDensity = 0.35,
            SpeckleRate = 0.22,
            GlowStrength = 0.60,
        },
        new()
        {
            Id = "rootbound",
            Name = "Rootbound",
            // Roots and knotted fibres rather than boulders: a different shape language, not a
            // different tint on the same cave.
            Family = TileFamily.WrongOrganic,
            Floor = FloorMotif.Roots,
            Wall = WallMotif.WovenRoot,
            Feature = FeatureKind.Nest,
            Ambient = AmbientKind.Spores,
            Hue = 96,
            AccentHue = 40,
            BoulderRoundness = 0.35,
            VariantCount = 3,
            CourseHeight = 4,
            VeinDensity = 0.78,
            MossDensity = 0.55,
            SpeckleRate = 0.35,
            GlowStrength = 0.35,
        },
        new()
        {
            Id = "drowned",
            Name = "Drowned Halls",
            // Flooded architecture: courses, grout and carved bands, so the floor reads as a place
            // somebody built and the sea took back.
            Family = TileFamily.WorkedStone,
            Floor = FloorMotif.Flagstones,
            Wall = WallMotif.CutBlock,
            Feature = FeatureKind.Pool,
            Ambient = AmbientKind.Drips,
            Hue = 205,
            AccentHue = 190,
            BoulderRoundness = 0.90,
            VariantCount = 4,
            CourseHeight = 4,
            VeinDensity = 0.50,
            MossDensity = 0.18,
            SpeckleRate = 0.12,
            GlowStrength = 0.55,
        },
        new()
        {
            Id = "saltchoir",
            Name = "The Salt Choir",
            Family = TileFamily.WorkedStone,
            Floor = FloorMotif.Mosaic,
            Wall = WallMotif.Ribbed,
            Feature = FeatureKind.Rune,
            Ambient = AmbientKind.Dust,
            Hue = 232,
            AccentHue = 58,
            BoulderRoundness = 0.72,
            VariantCount = 3,
            CourseHeight = 6,
            VeinDensity = 0.50,
            MossDensity = 0.10,
            SpeckleRate = 0.18,
            GlowStrength = 0.30,
        },
        new()
        {
            Id = "ember",
            Name = "The Ember Vault",
            Family = TileFamily.OrganicCave,
            Floor = FloorMotif.Vents,
            Wall = WallMotif.Grate,
            Feature = FeatureKind.Grate,
            Ambient = AmbientKind.Embers,
            Hue = 18,
            AccentHue = 45,
            BoulderRoundness = 0.42,
            VariantCount = 4,
            CourseHeight = 4,
            VeinDensity = 0.50,
            MossDensity = 0.12,
            SpeckleRate = 0.30,
            GlowStrength = 0.85,
        },
        new()
        {
            Id = "gilded",
            Name = "The Gilded Cage",
            Family = TileFamily.WorkedStone,
            Floor = FloorMotif.Plates,
            Wall = WallMotif.StackedPlate,
            Feature = FeatureKind.Clock,
            Ambient = AmbientKind.Dust,
            Hue = 48,
            AccentHue = 12,
            BoulderRoundness = 0.55,
            VariantCount = 3,
            CourseHeight = 3,
            VeinDensity = 0.50,
            MossDensity = 0.06,
            SpeckleRate = 0.10,
            GlowStrength = 0.65,
        },
        new()
        {
            Id = "hush",
            Name = "The Hush",
            Family = TileFamily.OrganicCave,
            Floor = FloorMotif.Mire,
            Wall = WallMotif.Silk,
            Feature = FeatureKind.Rune,
            Ambient = AmbientKind.Static,
            Hue = 266,
            AccentHue = 300,
            BoulderRoundness = 0.30,
            VariantCount = 2,
            CourseHeight = 4,
            VeinDensity = 0.50,
            MossDensity = 0.20,
            SpeckleRate = 0.26,
            GlowStrength = 0.18,
        },
        new()
        {
            Id = "gallery",
            Name = "The Long Gallery",
            Family = TileFamily.WorkedStone,
            Floor = FloorMotif.Flagstones,
            Wall = WallMotif.CutBlock,
            Feature = FeatureKind.Mirror,
            Ambient = AmbientKind.Dust,
            Hue = 191,
            AccentHue = 210,
            BoulderRoundness = 0.85,
            VariantCount = 4,
            CourseHeight = 8,
            VeinDensity = 0.50,
            MossDensity = 0.14,
            SpeckleRate = 0.08,
            GlowStrength = 0.45,
        },
        new()
        {
            Id = "warren",
            Name = "The Warren",
            Family = TileFamily.WrongOrganic,
            Floor = FloorMotif.Mire,
            Wall = WallMotif.WovenRoot,
            Feature = FeatureKind.Nest,
            Ambient = AmbientKind.Dust,
            Hue = 82,
            AccentHue = 70,
            BoulderRoundness = 0.28,
            VariantCount = 3,
            CourseHeight = 4,
            VeinDensity = 0.55,
            MossDensity = 0.60,
            SpeckleRate = 0.40,
            GlowStrength = 0.25,
        },
        new()
        {
            Id = "orchard",
            Name = "The Glass Orchard",
            Family = TileFamily.OrganicCave,
            Floor = FloorMotif.Shards,
            Wall = WallMotif.Crystal,
            Feature = FeatureKind.Crystal,
            Ambient = AmbientKind.Petals,
            Hue = 140,
            AccentHue = 92,
            BoulderRoundness = 0.62,
            VariantCount = 4,
            CourseHeight = 4,
            VeinDensity = 0.50,
            MossDensity = 0.45,
            SpeckleRate = 0.18,
            GlowStrength = 0.50,
        },
        new()
        {
            Id = "blindmile",
            Name = "The Blind Mile",
            Family = TileFamily.WrongOrganic,
            Floor = FloorMotif.Web,
            Wall = WallMotif.Silk,
            Feature = FeatureKind.Web,
            Ambient = AmbientKind.Static,
            Hue = 302,
            AccentHue = 250,
            BoulderRoundness = 0.48,
            VariantCount = 3,
            CourseHeight = 4,
            VeinDensity = 0.90,
            MossDensity = 0.25,
            SpeckleRate = 0.30,
            GlowStrength = 0.12,
        },
        new()
        {
            Id = "foundry",
            Name = "The Foundry",
            Family = TileFamily.WorkedStone,
            Floor = FloorMotif.Plates,
            Wall = WallMotif.Grate,
            Feature = FeatureKind.Grate,
            Ambient = AmbientKind.Embers,
            Hue = 14,
            AccentHue = 28,
            BoulderRoundness = 0.68,
            VariantCount = 3,
            CourseHeight = 5,
            VeinDensity = 0.50,
            MossDensity = 0.05,
            SpeckleRate = 0.35,
            GlowStrength = 0.75,
        },
        new()
        {
            Id = "mothgarden",
            Name = "The Moth Garden",
            Family = TileFamily.OrganicCave,
            Floor = FloorMotif.Drift,
            Wall = WallMotif.WovenRoot,
            Feature = FeatureKind.Nest,
            Ambient = AmbientKind.Moths,
            Hue = 56,
            AccentHue = 78,
            BoulderRoundness = 0.18,
            VariantCount = 3,
            CourseHeight = 4,
            VeinDensity = 0.50,
            MossDensity = 0.50,
            SpeckleRate = 0.28,
            GlowStrength = 0.90,
        },
        new()
        {
            Id = "ossuary",
            Name = "The Ossuary",
            Family = TileFamily.WorkedStone,
            Floor = FloorMotif.Bones,
            Wall = WallMotif.Bone,
            Feature = FeatureKind.Bones,
            Ambient = AmbientKind.Dust,
            Hue = 350,
            AccentHue = 20,
            BoulderRoundness = 0.60,
            VariantCount = 4,
            CourseHeight = 6,
            VeinDensity = 0.50,
            MossDensity = 0.08,
            SpeckleRate = 0.16,
            GlowStrength = 0.40,
        },
        new()
        {
            Id = "tide",
            Name = "The Tide",
            Family = TileFamily.OrganicCave,
            Floor = FloorMotif.Ripples,
            Wall = WallMotif.CutBlock,
            Feature = FeatureKind.Pool,
            Ambient = AmbientKind.Bubbles,
            Hue = 196,
            AccentHue = 160,
            BoulderRoundness = 0.72,
            VariantCount = 4,
            CourseHeight = 4,
            VeinDensity = 0.50,
            MossDensity = 0.40,
            SpeckleRate = 0.24,
            GlowStrength = 0.55,
        },
        new()
        {
            Id = "bramble",
            Name = "The Bramble",
            Family = TileFamily.WrongOrganic,
            Floor = FloorMotif.Roots,
            Wall = WallMotif.WovenRoot,
            Feature = FeatureKind.Seal,
            Ambient = AmbientKind.Petals,
            Hue = 122,
            AccentHue = 142,
            BoulderRoundness = 0.55,
            VariantCount = 3,
            CourseHeight = 4,
            VeinDensity = 0.70,
            MossDensity = 0.70,
            SpeckleRate = 0.38,
            GlowStrength = 0.20,
        },
        new()
        {
            Id = "vault",
            Name = "The Vault",
            Family = TileFamily.WorkedStone,
            Floor = FloorMotif.Plates,
            Wall = WallMotif.StackedPlate,
            Feature = FeatureKind.Seal,
            Ambient = AmbientKind.Dust,
            Hue = 252,
            AccentHue = 268,
            BoulderRoundness = 0.95,
            VariantCount = 2,
            CourseHeight = 2,
            VeinDensity = 0.50,
            MossDensity = 0.12,
            SpeckleRate = 0.06,
            GlowStrength = 0.35,
        },
        new()
        {
            Id = "loom",
            Name = "The Loom",
            Family = TileFamily.WrongOrganic,
            Floor = FloorMotif.Web,
            Wall = WallMotif.Silk,
            Feature = FeatureKind.Web,
            Ambient = AmbientKind.Motes,
            Hue = 208,
            AccentHue = 232,
            BoulderRoundness = 0.40,
            VariantCount = 4,
            CourseHeight = 4,
            VeinDensity = 0.80,
            MossDensity = 0.22,
            SpeckleRate = 0.20,
            GlowStrength = 0.65,
        },
        new()
        {
            Id = "ashgarden",
            Name = "The Ash Garden",
            Family = TileFamily.OrganicCave,
            Floor = FloorMotif.Drift,
            Wall = WallMotif.MossRock,
            Feature = FeatureKind.Rune,
            Ambient = AmbientKind.Ash,
            Hue = 8,
            AccentHue = 35,
            BoulderRoundness = 0.34,
            VariantCount = 2,
            CourseHeight = 4,
            VeinDensity = 0.50,
            MossDensity = 0.15,
            SpeckleRate = 0.48,
            GlowStrength = 0.45,
        },
        new()
        {
            Id = "deep",
            Name = "The Deep",
            Family = TileFamily.OrganicCave,
            Floor = FloorMotif.Ripples,
            Wall = WallMotif.Ribbed,
            Feature = FeatureKind.Heart,
            Ambient = AmbientKind.Fireflies,
            Hue = 234,
            AccentHue = 205,
            BoulderRoundness = 0.48,
            VariantCount = 3,
            CourseHeight = 4,
            VeinDensity = 0.50,
            MossDensity = 0.30,
            SpeckleRate = 0.20,
            GlowStrength = 0.70,
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
