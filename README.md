# MazeGame — COP3503H Group Project (Group 5)

A horror-themed maze game written in C# on .NET 8. The player wakes up in a
randomly generated maze, must collect the objectives, and reach the exit before the
thing that hunts them catches up.

This is a course project, so the graded data structures and algorithms
(Disjoint Set / Union-Find for maze generation, Dijkstra with a hand-written binary
min-heap for the enemy) are implemented from scratch in this repository. No
graph/pathfinding libraries are used.

> **Status (2026-10-20):** Phases 1–6 complete — the game is **playable in a Raylib
> window**: a braided maze drawn as a bioluminescent cave, fog of war, glowing flora, and a
> killer that carries its own light. Three keys and an exit are placed, and the killer hunts
> you with Dijkstra while you collect the keys, seal barriers behind you and throw wisps to
> misdirect it. It opens on a title screen and you can descend again the moment you die (279
> unit tests). Sound and final polish are Phase 7; see [`PROGRESS.md`](PROGRESS.md) for the
> current state.

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

`dotnet run --project src/MazeGame.UI` opens the title screen; `--console` plays the same game
in the terminal, and the console view is also used automatically when no window can be
opened (over SSH, for example). The default maze is 15x15 with three keys, three barriers, two
wisps, a vision radius of 3 cells and a killer that moves every 0.6 seconds; all of that is
tunable in `GameConfig`. Every run generates a new cave (`Seed = null`); set a seed to replay
the exact same one.

The first restore downloads the Raylib native library for your platform (see
`ARCHITECTURE.md` for the exact package version).

## Controls

| Key | Action |
| --- | --- |
| W / S or arrows (title) | Select `DESCEND` or `LEAVE` |
| Enter or Space (title) | Confirm the highlighted entry |
| W A S D / arrow keys | Move one cell |
| B or Space | Start aiming a barrier (the game pauses) |
| R | Start aiming a wisp (the game pauses) |
| WASD / arrows (while aiming) | Move the aim cursor anywhere on the map |
| B, Space or Enter (while aiming) | Place the aimed barrier or wisp on the cursor |
| Esc (while aiming) | Cancel the aim |
| R or Enter (after a run) | Descend again, on a new maze |
| Q or Esc | Quit |

## How to play

The game opens on a title screen over the cave: `BIOLUMINESCENT / DEPTHS`, with `DESCEND` and
`LEAVE`. Starting a run (or pressing R on the end card) builds a brand-new game — new maze,
full keys, barriers and wisps — so you never have to close and reopen the window to try again.

The window is an underground cave lit by living light. You are a small hooded figure carrying
a lantern; the killer is a tall, spindly silhouette with pink rim light and two pale eyes; keys
are clusters of glowing fungus; the exit is a stone arch (cyan once open); barriers are thorny
barricades of cave moss. Mushrooms, lichen and spore motes glow green, pink and cyan in the
dark. The terminal view uses `@` for you, `K` for the killer, `k` for a key, `E` for the exit,
`#` for a sealed barrier, `=` for a spent one, `*` for a wisp and `X` for the aiming cursor.

1. Collect all three keys — the exit stays `LOCKED` until the last one is picked up.
2. Reach the exit without stepping onto the killer.
3. When the killer gets close, press B to aim, move the cursor anywhere on the map (`X` in
   the terminal) and confirm. A fresh barrier is **sealed**: the killer treats it as a wall,
   so it takes another route when one exists, and only tears it apart (3 seconds) when the
   seal is the only way through. After 10 seconds the seal cracks into a husk that still costs
   it 3 seconds to cross. Aiming pauses the clock, so take your time placing it on the corridor
   it is about to use.
4. Press R to throw a **wisp** instead: a glowing decoy that burns for 6 seconds. For as long
   as it glows the killer walks to whichever of you and the wisp is cheaper by Dijkstra, so a
   wisp dropped down a side passage buys you the time to take the other fork.

The maze is hidden by fog of war: you see your own cell and 3 steps down the corridors from
it, and walls block the light. Cells you have already seen stay dimly drawn so you can
remember the layout, but the killer, keys and exit are only shown while you can actually see
them.

The killer carries its own violet light. Once it is within 6 walking steps of you, the cells
around it light up — even when the corridor between you is pitch black — so you get a warning
glow before it arrives. The maze is also braided: about 55% of its dead ends are opened up, so
there are roughly 19 dead ends instead of 57 on a 15x15 map, and eight extra loops run across
it. That is what stops a chase from ending in a corner and what lets a barrier actually send
the killer the long way round.

## Project structure

```
MazeGame.sln
Directory.Build.props        shared compiler settings (net8.0, nullable, warnings-as-errors)
.editorconfig                code style and naming rules
src/MazeGame.Core/           all game logic, no UI references
  Abstractions/              IRandom, IMazeGenerator, IPathfinder, ...
  Maze/                      DisjointSet, Grid/Cell, MazeGenerator, DeadEndBraider, BraidedMazeGenerator
  Pathfinding/               MinHeap, DijkstraPathfinder
  Game/                      GameConfig, GameStatus, GameState (rules + enemy AI), GameFactory (builds a run)
src/MazeGame.UI/             window/console frontends, input mapping, rendering, frame loop
tests/MazeGame.Tests/        xUnit tests for Core
references/                 team reference art (palette source; not loaded by the game)
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

The perfect maze is then shaped by two decorators, so the graded generator itself is untouched:

- `DeadEndBraider` opens one extra wall at `GameConfig.DeadEndBraidPercent` (55%) of the dead
  ends. Removing a wall can never create a new dead end, so this only ever removes traps: a
  15x15 map goes from ~57 dead ends to ~19. It also cannot open the outer boundary, and the
  maze stays connected.
- `BraidedMazeGenerator` then removes `GameConfig.ExtraLoops` (8) more standing interior walls.
  Each removal adds exactly one loop, so the played maze has `cellCount - 1 + 8 + braided`
  passages: still fully connected, no longer a tree.

Both are decorators over `IMazeGenerator`, so `DeadEndBraidPercent = 0` and `ExtraLoops = 0`
restore a pure perfect maze.

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
cell costs `1`; a cell holding a spent barrier costs `ObstacleCost` (5), and a cell holding a
**sealed** one costs `SealedBarrierCost` (40), which is far more than any detour on a 15x15
map — so the killer walks around a seal while it holds. Both weights are finite on purpose:
that is what guarantees the player can never wall the killer in, and why a seal that truly cuts
the route ends in a tear-down rather than a stuck game.

The same search also picks the killer's *target*: the player, or an active wisp, whichever route
is cheaper. One algorithm, two jobs.

Dijkstra runs on a hand-written binary min-heap: each cell is extracted once in order
of increasing distance, and neighbours are relaxed through cell walls only, so a path
can never cut through a wall. The path is recomputed whenever the player moves or the
obstacles change.

The pathfinder takes the weight of entering a cell as a function, so it knows nothing
about obstacles — `GameState` supplies `sealed ? 40 : spent ? 5 : 1`:

```csharp
var pathfinder = new DijkstraPathfinder();
IReadOnlyList<Cell> route = pathfinder.FindPath(
    grid, playerCell, exitCell,
    cell => seals.ContainsKey(cell) ? 40 : barriers.Contains(cell) ? 5 : 1);
// route[0] == playerCell, route[^1] == exitCell, adjacent steps never cross a wall
```

### The player's skill mechanic (barriers and wisps)

The player carries a small, finite supply of barriers (three by default) and wisps (two). Press
B or R and the game pauses so you can aim: WASD moves an `X` cursor anywhere on the map,
Space/Enter drops the item there, Esc cancels.

A barrier has two phases:

1. **Sealed** for `BarrierSealSeconds` (10). The cell is priced at `SealedBarrierCost` (40), so
   the killer takes another route while one exists. If the seal is the only way through, the
   killer tears it apart, which takes `BarrierBreakSeconds` (3) and destroys the barrier.
2. **Spent.** Once the seal lapses, the husk stays on the map and costs `ObstacleCost` (5), or
   3 seconds, to cross.

The wisp is the other half of the same idea: it is a decoy, not a wall. While it glows (6
seconds), the killer runs to whichever of the player and the wisp is cheaper by Dijkstra. A
wisp in a side branch pulls the chase off your route, and a wisp thrown *between* you and the
killer is simply ignored, because the player is closer.

Measured on eight 15x15 seeds with the shipped settings: 81% of the cells on the killer's
route admit a detour around a seal (up from 40% before the dead ends were braided), and the
remaining 19% have to be torn down.

### Presentation (fog, killer light, bioluminescent cave)

What the player may see is a rule, so `GameState.IsVisible(cell)` answers it: a
breadth-first walk out from the player through open passages, cut off at `VisionRadius`.
Walls therefore block sight, and the radius is a lantern range down corridors rather than a
circle drawn through rock.

The killer has a second light. Once it is within `EnemyAuraDistance` (6) steps, the cells
within `EnemyAuraRadius` (2) of it become visible too, and `GameState.EnemyGlow` marks which
ones were lit that way so the renderer can glow them pink. Its own light ignores the wall
between you, which is exactly why it can be seen around a corner.

The art is drawn entirely from Raylib primitives, not textures: rounded stone floor slabs with
grit and cracks, walls made of thick rounded rock strokes with a green rim, moss and glowing
lichen, mushrooms that grow in some cells, and spores drifting only inside the lantern light.
Characters have silhouettes rather than marker dots — a hooded explorer with a warm lantern, a
spindly killer with a pink rim and eyes — and the HUD is a row of glass pills with a pink
warning when the killer is close. Walls are drawn in two passes (all rock, then all lit edges)
so a shared wall keeps the rim on both sides. The renderers only decide how to express all
this; they never decide what may be seen. Each keeps its own set of explored cells, so the map
does not blink in and out as you walk, and `ConsoleRenderer` follows the same visibility rule
in ASCII, which keeps the terminal fallback a fair way to play rather than a map cheat.

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
