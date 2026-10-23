using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MazeGame.Core.Game;

namespace MazeGame.UI.Persistence;

/// <summary>
/// The two things the game remembers between sessions: the run in progress and the garden grown
/// across every run.
/// </summary>
/// <remarks>
/// <para>
/// Both live under the platform's per-user application-data folder, so a rebuild never wipes a
/// player's progress. The store never throws on a bad file: a corrupt save is moved aside with a
/// <c>.bad</c> suffix and reported as absent, because losing a save is bad and refusing to start
/// the game is worse.
/// </para>
/// <para>
/// Nothing here knows a rule. The run file is a <see cref="RunSnapshot"/>, and loading it is
/// <see cref="Campaign.Restore"/>'s job.
/// </para>
/// </remarks>
public sealed class SaveStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;

    /// <summary>
    /// Creates a store over <paramref name="directory"/>, or the game's own data folder.
    /// </summary>
    /// <param name="directory">
    /// The folder to use, or null for <c>%APPDATA%/MazeGame</c> (or its platform equivalent).
    /// </param>
    public SaveStore(string? directory = null) =>
        _directory = directory ?? DefaultDirectory();

    /// <summary>
    /// The folder the game uses when nothing else is specified.
    /// </summary>
    /// <remarks>
    /// <c>MAZEGAME_SAVE_DIR</c> overrides it, which is how a test run or a portable install keeps
    /// its saves out of the player's profile.
    /// </remarks>
    private static string DefaultDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable("MAZEGAME_SAVE_DIR");

        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return overridden;
        }

        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.Create),
            "MazeGame");
    }

    /// <summary>Gets the folder the files live in.</summary>
    public string Directory => _directory;

    /// <summary>Gets whether there is a run on disk worth offering CONTINUE for.</summary>
    public bool HasRun => File.Exists(RunPath);

    private string RunPath => Path.Combine(_directory, "run.json");

    private string GardenPath => Path.Combine(_directory, "garden.json");

    /// <summary>
    /// Loads the saved run, or returns null when there is none or it cannot be read.
    /// </summary>
    /// <returns>The saved run, or <see langword="null"/>.</returns>
    public RunSnapshot? LoadRun() => Read<RunSnapshot>(RunPath);

    /// <summary>Writes the run, creating the folder if it is the first save.</summary>
    /// <param name="snapshot">The run to write.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> is null.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    public void SaveRun(RunSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Write(RunPath, snapshot);
    }

    /// <summary>Deletes the saved run, if there is one. A finished run has nothing to resume.</summary>
    public void ClearRun()
    {
        if (File.Exists(RunPath))
        {
            File.Delete(RunPath);
        }
    }

    /// <summary>Loads the garden, or a fresh one when the file is missing or unreadable.</summary>
    /// <returns>The garden profile, never null.</returns>
    public GardenProfile LoadGarden() => Read<GardenProfile>(GardenPath) ?? GardenProfile.Empty;

    /// <summary>Writes the garden, creating the folder if it is the first save.</summary>
    /// <param name="garden">The garden to write.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="garden"/> is null.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    public void SaveGarden(GardenProfile garden)
    {
        ArgumentNullException.ThrowIfNull(garden);

        Write(GardenPath, garden);
    }

    /// <summary>
    /// Adds one finished run to the garden and returns the garden as it now stands.
    /// </summary>
    /// <param name="blooms">The blooms the run grew.</param>
    /// <param name="floorReached">The deepest floor the run reached.</param>
    /// <returns>The updated garden.</returns>
    public GardenProfile RecordRun(int blooms, int floorReached)
    {
        var garden = LoadGarden().Add(blooms, floorReached);

        SaveGarden(garden);

        return garden;
    }

    private static T? Read<T>(string path)
        where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch (JsonException)
        {
            // A file half-written by a crash is unreadable, not fatal: keep it for diagnosis and
            // behave as if there were no save.
            Quarantine(path);
            return null;
        }
        catch (IOException)
        {
            Quarantine(path);
            return null;
        }
    }

    private static void Write<T>(string path, T value)
    {
        var folder = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
        }

        // Write-then-move: a crash mid-write leaves the previous save intact rather than a
        // truncated file that the next launch has to throw away.
        var temporary = path + ".tmp";

        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
        File.Move(temporary, path, overwrite: true);
    }

    private static void Quarantine(string path)
    {
        try
        {
            File.Move(path, path + ".bad", overwrite: true);
        }
        catch (IOException)
        {
            // Nothing more to do: the store simply reports no save.
        }
    }
}
