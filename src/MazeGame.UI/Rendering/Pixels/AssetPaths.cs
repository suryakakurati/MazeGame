using System;
using System.IO;

namespace MazeGame.UI.Rendering.Pixels;

/// <summary>
/// Finds the generated art the game draws from: <c>Content/Generated</c>, next to the executable
/// or inside the repository.
/// </summary>
/// <remarks>
/// <para>
/// A committed atlas must work from three places: a published build (the files sit beside the
/// executable), <c>dotnet run</c> from the repository, and the test runner in its own output
/// folder. The search is therefore the executable's folder first, then every parent of it looking
/// for the repository's layout — and it fails with the paths it tried, because a missing atlas is a
/// build problem, not something to route around.
/// </para>
/// <para>
/// Nothing is cached: resolution is a few <see cref="File.Exists"/> calls and happens once per run
/// (the manifest and the atlases are loaded at startup).
/// </para>
/// </remarks>
public static class AssetPaths
{
    /// <summary>The folder the sprite compiler writes into, relative to the repository root.</summary>
    public const string RepositoryFolder = "src/MazeGame.UI/Content/Generated";

    /// <summary>The folder name inside a published build.</summary>
    public const string PublishedFolder = "Content/Generated";

    private const int MaximumClimb = 8;

    private const string ManifestFileName = "manifest.txt";

    /// <summary>
    /// Returns the folder holding <c>manifest.txt</c> and the theme PNGs.
    /// </summary>
    /// <returns>The absolute path to the generated folder.</returns>
    /// <exception cref="FileNotFoundException">
    /// Thrown when neither a published copy nor the repository's folder can be found.
    /// </exception>
    public static string GeneratedDirectory()
    {
        var tried = new System.Collections.Generic.List<string>();

        if (Find(AppContext.BaseDirectory, tried) is { } found)
        {
            return found;
        }

        throw new FileNotFoundException(
            "The generated sprite atlases could not be found. Looked in: " + string.Join(", ", tried) +
            ". Run 'dotnet run --project tools/AssetForge -- --all' and commit the output.",
            Path.Combine(AppContext.BaseDirectory, PublishedFolder, ManifestFileName));
    }

    /// <summary>
    /// Returns the absolute path of one atlas inside the generated folder.
    /// </summary>
    /// <param name="fileName">The PNG's file name, as the manifest records it.</param>
    /// <returns>The absolute path to the PNG.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileName"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the atlas does not exist.</exception>
    public static string Atlas(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var path = Path.Combine(GeneratedDirectory(), fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The atlas '{fileName}' is missing: {path}", path);
        }

        return path;
    }

    /// <summary>
    /// Walks up from <paramref name="start"/> looking for a generated folder.
    /// </summary>
    /// <param name="start">The folder to begin at.</param>
    /// <param name="tried">Collects every folder that was rejected, for the error message.</param>
    /// <returns>The folder, or <see langword="null"/> when there is none.</returns>
    private static string? Find(string start, System.Collections.Generic.List<string> tried)
    {
        var directory = new DirectoryInfo(start);

        for (var climb = 0; climb < MaximumClimb && directory is not null; climb++)
        {
            foreach (var relative in new[] { PublishedFolder, RepositoryFolder })
            {
                var candidate = Path.Combine(directory.FullName, relative);
                var manifest = Path.Combine(candidate, ManifestFileName);

                if (File.Exists(manifest))
                {
                    return candidate;
                }

                tried.Add(candidate);
            }

            directory = directory.Parent;
        }

        return null;
    }
}
