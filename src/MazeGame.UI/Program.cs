using System;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
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

// The factory owns the shipped maze pipeline and builds one complete game per call: a seeded
// random source feeds both maze generation and entity placement, so a seed reproduces the run
// and a null seed gives a new cave every time. Because starting a game is a call rather than a
// mode, "descend again" after death is the same call as the first descent.
var factory = new GameFactory(config, () => new SeededRandom(config.Seed), new DijkstraPathfinder());

if (useConsole || !RaylibFrontend.TryRun(config, factory.Create))
{
    ConsoleFrontend.Run(factory.Create);
}
