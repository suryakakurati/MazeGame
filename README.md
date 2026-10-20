# MazeGame — COP3503H Group Project (Group 5)

A horror-themed maze game written in C# on .NET 8. The player wakes up in a
randomly generated maze, must collect the objectives, and reach the exit before the
thing that hunts them catches up.

This is a course project, so the graded data structures and algorithms
(Disjoint Set / Union-Find for maze generation, Dijkstra with a hand-written binary
min-heap for the enemy) are implemented from scratch in this repository. No
graph/pathfinding libraries are used.

> **Status (2026-10-20):** Phases 1–6 complete — the game is **playable in a Raylib
> window**, with fog of war and a terminal fallback. A seeded maze is generated, three keys
> and an exit are placed, and a killer hunts you with Dijkstra while you collect the keys
> and place barriers (198 unit tests). Sound and final polish are Phase 7; see
> [`PROGRESS.md`](PROGRESS.md) for the current state.

## Requirements

- .NET 8 SDK (any 8.0.x)
- macOS, Linux, or Windows
- A display for the game window; the `--console` version needs only a terminal

## Build, run, test

From the repository root (the folder containing `MazeGame.sln`):

```bash
dotnet build                        # must be warning-free
dotnet test                         # unit tests for MazeGame.Core
dotnet run --project src/MazeGame.UI              # Raylib window
dotnet run --project src/MazeGame.UI -- --console # terminal version
```

`dotnet run --project src/MazeGame.UI` opens a game window; `--console` plays the same game
in the terminal, and the console view is also used automatically when no window can be
opened (over SSH, for example). The default maze is 15x15 with three keys, three barriers, a
vision radius of 3 cells and a killer that moves every 0.6 seconds; all of that is tunable
in `GameConfig`.

The first restore downloads the Raylib native library for your platform (see
`ARCHITECTURE.md` for the exact package version).

## Controls

| Key | Action |
| --- | --- |
| W A S D / arrow keys | Move one cell |
| B or Space | Start aiming a barrier (the game pauses) |
| WASD / arrows (while aiming) | Move the aim cursor anywhere on the map |
| B, Space or Enter (while aiming) | Place the barrier on the cursor |
| Esc (while aiming) | Cancel the aim |
| Q or Esc | Quit |

## How to play

In the window you are a warm dot, the killer is a red circle, keys are gold, the exit is an
outlined square (green once open) and barriers are grey blocks. The terminal view uses
`@` for you, `K` for the killer, `k` for a key, `E` for the exit, `#` for a barrier and `X`
for the aiming cursor.

1. Collect all three keys — the exit stays `LOCKED` until the last one is picked up.
2. Reach the exit without stepping onto the killer.
3. When the killer gets close, press B to aim, move the cursor anywhere on the map (`X` in
   the terminal) and confirm: the killer stalls at that cell for 3 seconds before forcing
   through. Aiming pauses the clock, so take your time placing it on the corridor it is
   about to use.

The maze is hidden by fog of war: you see your own cell and 3 steps down the corridors from
it, and walls block the light. Cells you have already seen stay dimly drawn so you can
remember the layout, but the killer, keys and exit are only shown while you can actually see
them. The killer starts in a dead end far from you, so you always have a route to every key
at the start.

## Project structure

```
MazeGame.sln
Directory.Build.props        shared compiler settings (net8.0, nullable, warnings-as-errors)
.editorconfig                code style and naming rules
src/MazeGame.Core/           all game logic, no UI references
  Abstractions/              IRandom, IMazeGenerator, IPathfinder, ...
  Maze/                      DisjointSet, Grid/Cell, MazeGenerator
  Pathfinding/               MinHeap, DijkstraPathfinder
  Game/                      GameConfig, GameStatus, GameState (rules + enemy AI)
src/MazeGame.UI/             window/console frontends, input mapping, rendering, frame loop
tests/MazeGame.Tests/        xUnit tests for Core
PROGRESS.md                  current status and next steps (single source of truth)
ARCHITECTURE.md              class responsibilities and dependency diagram
DECISIONS.md                 why the design looks like this
CONTRIBUTING.md              conventions and extension guides
HANDOFF.md                   onboarding briefing
```

## How it works

### Maze generation (Union-Find)

The maze is a grid of cells where every cell starts in its own set. Candidate walls
between adjacent cells are shuffled with a seeded random source; for each candidate,
the generator asks the Disjoint Set whether its two cells already belong to the same
set. If they do, removing the wall would create a cycle, so the wall stays. If they
do not, the wall is removed and the two sets are merged. After every candidate has
been considered, the result is a *perfect* maze: fully connected, no loops, exactly
one path between any two cells, and exactly `cellCount - 1` removed walls.

`Find` uses path compression and `Union` uses union by size, which keeps operations
effectively constant time. The generator takes an optional seed, so a maze can be
reproduced exactly for debugging, replays, and tests.

```csharp
var generator = new MazeGenerator(new SeededRandom(seed: 42));
Grid maze = generator.Generate(new MazeOptions(Rows: 15, Columns: 15, Seed: 42));
bool wallAbove = maze.HasWall(row: 0, column: 0, direction: Direction.North);
int passages = maze.CountPassages(); // == maze.CellCount - 1 for a perfect maze
```

### Enemy pathfinding (Dijkstra + binary min-heap)

The killer moves toward the player along the shortest weighted path. Every passable
cell costs `1`; a cell holding a player-placed obstacle costs a higher but **finite**
amount (5 by default). Obstacles therefore slow the killer down and make it route
around them when that is cheaper, but they can never make the player unreachable —
the pathfinder always returns a path, and the enemy never gets permanently stuck.

Dijkstra runs on a hand-written binary min-heap: each cell is extracted once in order
of increasing distance, and neighbours are relaxed through cell walls only, so a path
can never cut through a wall. The path is recomputed whenever the player moves or the
obstacles change.

The pathfinder takes the weight of entering a cell as a function, so it knows nothing
about obstacles — `GameState` supplies `barrier ? ObstacleCost : 1`:

```csharp
var pathfinder = new DijkstraPathfinder();
IReadOnlyList<Cell> route = pathfinder.FindPath(
    grid, playerCell, exitCell, cell => barriers.Contains(cell) ? 5 : 1);
// route[0] == playerCell, route[^1] == exitCell, adjacent steps never cross a wall
```

### The player's skill mechanic (placeable barriers)

The player carries a small, finite supply of barriers (three by default). Press B and the
game pauses so you can aim: WASD moves an `X` cursor anywhere on the map, B/Space/Enter
drops the barrier there, Esc cancels. That cell now costs the killer 5 normal steps of
time to enter (3 seconds), and it still never blocks it outright.

Because a perfect maze has only one route between two cells, a barrier buys *time* rather
than a detour. Placing it on the corridor the killer is already walking is what makes it
worth spending — which is why the aiming pause exists.

### Presentation (fog of war, vignette, flicker)

What the player may see is a rule, so `GameState.IsVisible(cell)` answers it: a
breadth-first walk out from the player through open passages, cut off at `VisionRadius`.
Walls therefore block sight, and the radius is a torch range down corridors rather than a
circle drawn through rock.

The renderers only decide how to darken that answer. `RaylibRenderer` draws unseen cells
black, remembered cells dim, and dims by distance from the player (the torch falloff and the
vignette in one); a slow sum of sine waves makes the light breathe instead of strobing. Each
renderer keeps its own set of explored cells, so the map does not blink in and out as you
walk. `ConsoleRenderer` follows the same rule in ASCII, which keeps the terminal fallback a
fair way to play rather than a map cheat.

## Roadmap

| Phase | Scope |
| --- | --- |
| 1 | Solution, projects, build rules, tracking docs — **done** |
| 2 | Disjoint Set + tests — **done** |
| 3 | Grid/Cell model, maze generator behind `IMazeGenerator`, seeded tests — **done** |
| 4 | Binary min-heap, Dijkstra behind `IPathfinder`, weighted-obstacle tests — **done** |
| 5 | `GameState`: player, objectives, exit, enemy, obstacles, win/lose, console renderer — **done** |
| 6 | Raylib-cs UI: maze rendering, input, fog of war, vignette — **done** |
| 7 | Skill-expression mechanic and polish (audio, flicker, difficulty) |
| 8 | Final cleanup, README screenshots, presentation notes |

## Contributing

Read [`HANDOFF.md`](HANDOFF.md) first, then [`CONTRIBUTING.md`](CONTRIBUTING.md).
Every change must leave `dotnet build` warning-free and `dotnet test` green.

## Team

COP3503H — Group 5. Contributors are listed in the course submission; add your name
here when you make your first commit.
