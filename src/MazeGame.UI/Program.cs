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

// The factory owns the shipped maze pipeline and builds one complete floor per call. Each floor
// gets its own random source, derived from the run seed and the floor number, so a seeded run is
// reproducible and its floors are still different caves. Starting a run is a call rather than a
// mode, so "descend again" after a lost run is the same call as the first descent.
var factory = new GameFactory(config, seed => new SeededRandom(seed), new DijkstraPathfinder());

if (useConsole || !RaylibFrontend.TryRun(config, factory.CreateCampaign))
{
    ConsoleFrontend.Run(factory.CreateCampaign);
}
