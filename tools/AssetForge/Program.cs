using System;
using System.Globalization;
using MazeGame.AssetForge;

// AssetForge: compiles theme atlases into src/MazeGame.UI/Content/Generated.
//
//   dotnet run --project tools/AssetForge -- --all
//   dotnet run --project tools/AssetForge -- --theme shallows
//   dotnet run --project tools/AssetForge -- --check
//
// --check writes nothing and exits non-zero when a theme is invalid, two neighbouring themes are
// too similar, the compiler is not deterministic, or a committed atlas no longer matches.

var themes = ThemeCatalog.Shipped;
var directory = SpriteCompiler.DefaultOutputDirectory;
var check = false;
string? only = null;

for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--check":
            check = true;
            break;
        case "--all":
            // No command already means "all"; the flag is accepted for scripting symmetry.
            break;
        case "--theme":
            if (index + 1 >= args.Length)
            {
                Console.Error.WriteLine("--theme needs an id.");
                return 2;
            }

            only = args[++index];
            break;
        case "--out":
            if (index + 1 >= args.Length)
            {
                Console.Error.WriteLine("--out needs a directory.");
                return 2;
            }

            directory = args[++index];
            break;
        case "--help":
        case "-h":
            Console.WriteLine("usage: AssetForge [--all | --theme <id>] [--out <dir>] [--check]");
            return 0;
        default:
            Console.Error.WriteLine($"unknown argument: {args[index]}");
            return 2;
    }
}

if (check)
{
    var problems = SpriteCompiler.Check(themes, directory);

    if (problems.Count == 0)
    {
        var tileCount = 0;

        foreach (var theme in themes)
        {
            tileCount += SpriteCompiler.Compile(theme).Tiles.Count;
        }

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"CHECK_OK themes={themes.Count} tiles={tileCount} dir={directory}"));
        return 0;
    }

    Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture, $"CHECK_FAILED problems={problems.Count}"));

    foreach (var problem in problems)
    {
        Console.Error.WriteLine($"  {problem}");
    }

    return 1;
}

if (only is not null)
{
    var theme = ThemeCatalog.ById(only);

    if (theme is null)
    {
        Console.Error.WriteLine($"no theme with id '{only}'");
        return 2;
    }

    var atlas = SpriteCompiler.Compile(theme);
    SpriteCompiler.WriteAll([theme], directory);
    Console.WriteLine(
        string.Create(
            CultureInfo.InvariantCulture,
            $"WROTE theme={theme.Id} tiles={atlas.Tiles.Count} dir={directory}"));
    return 0;
}

SpriteCompiler.WriteAll(themes, directory);

var total = 0;

foreach (var theme in themes)
{
    var atlas = SpriteCompiler.Compile(theme);
    total += atlas.Tiles.Count;

    Console.WriteLine(
        string.Create(
            CultureInfo.InvariantCulture,
            $"theme {theme.Id,-10} {atlas.Canvas.Width}x{atlas.Canvas.Height} " +
            $"tiles={atlas.Tiles.Count} sha={Png.Checksum(atlas.Png):x8}"));
}

Console.WriteLine(
    string.Create(
        CultureInfo.InvariantCulture,
        $"COMPILED themes={themes.Count} tiles={total} dir={directory}"));
return 0;
