using System;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.UI.Frontends;

// Composition root: this is the only place that knows which concrete implementations are
// used. Swapping a renderer, the pathfinder or the frontend is a change to this file alone,
// and no game rule lives here — the frontends only forward input and elapsed time.
//
// Pass --console to play in the terminal; otherwise a Raylib window is opened, with the
// console version as an automatic fallback when no window is available.

var useConsole = Array.Exists(
    args,
    argument => string.Equals(argument, "--console", StringComparison.OrdinalIgnoreCase));

var config = new GameConfig();

// One seeded random source feeds both maze generation and entity placement, so the whole
// game is reproducible from GameConfig.Seed (null means a different game every run).
var random = new SeededRandom(config.Seed);
var maze = new MazeGenerator(random)
    .Generate(new MazeOptions(config.Rows, config.Columns, config.Seed));
var state = new GameState(config, maze, new DijkstraPathfinder(), random);

if (useConsole || !RaylibFrontend.TryRun(state))
{
    ConsoleFrontend.Run(state);
}
