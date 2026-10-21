# MazeGame — COP3503H Group Project (Group 5)

A horror-themed maze game written in C# on .NET 8. The player wakes up in a
randomly generated maze, must collect the objectives, and reach the exit before the
thing that hunts them catches up.

This is a course project, so the graded data structures and algorithms
(Disjoint Set / Union-Find for maze generation, Dijkstra with a hand-written binary
min-heap for the enemy) are implemented from scratch in this repository. No
graph/pathfinding libraries are used.

> **Status (2026-10-20):** Course Phases 1–6 complete, and roguelike **Phases 1–4 plus the first
> slice of Phase 6** are in — the
> game is **playable in a Raylib window** as a **three-floor campaign with a card draft and timed
> steps**. Each floor is a braided maze **drawn from the compiled pixel-art atlases** on a virtual
> canvas at an integer scale, with fog of war, a per-cell lantern lightmap and a killer that carries
> its own light: collect the keys, open the arch, and descend.
> **You have three hearts**: a catch costs one, throws the killer to a distant dead end and stuns
> it, and **hearts carry between floors**, so only the third catch on the whole run ends it. Walking
> is a cadence (0.11 s per cell, tightening as you descend), one press is buffered, **Shift
> sprints** on a stamina bar. The explorer snaps to its tile (one press, one tile) while the killer
> slides between cells. Floors differ in name, accent, key count and killer pace (The Shallows →
> Rootbound → Drowned Halls), with an intro card, a floor-cleared card, **a draft of three cards
> between floors — take one or skip for a heart** — from a catalog of thirteen, including four
> movement cards, and a run summary (574 unit tests; the sprite compiler has produced 327 validated
> pixel-art tiles, and the game draws the floor from them). The phased plan to a full roguelike is in [`FUTURE.md`](FUTURE.md); current
> state in [`PROGRESS.md`](PROGRESS.md).

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

dotnet run --project tools/AssetForge             # recompile the committed sprite atlases
dotnet run --project tools/AssetForge -- --check  # validate themes and atlases, write nothing
```

`tools/AssetForge` is the sprite compiler (see [`FUTURE.md`](FUTURE.md) §6.6): it draws the
16x16 pixel-art atlases from theme parameters and writes them to
`src/MazeGame.UI/Content/Generated`, which **is committed**, so building and playing never needs
the tool. `--check` is the brake: it fails on an invalid theme, on two neighbouring floors whose
palettes or shapes are too similar, if two compiles differ, or if a committed atlas has drifted
from the code that claims to draw it. The game loads these atlases at startup (they are copied
beside the executable, and found in the repository when running from source), so the floor is
real pixel art: a 16 px-per-cell canvas at an integer scale, thin wall faces along closed edges,
the cast animated from the step clocks, and one multiplied lightmap for the lantern and the fog.
The menus, the draft and the run summary are still the older vector renderer until the font atlas
lands — that is the next slice of the phase.

`dotnet run --project src/MazeGame.UI` opens the title screen; `--console` plays the same
campaign in the terminal, and the console view is also used automatically when no window can be
opened (over SSH, for example). A run is three floors of 15x15, with three barriers, two wisps,
a vision radius of 3 cells and a key count per floor; the killer starts at 0.6 s per step and
speeds up each floor. All of that is tunable in `GameConfig` and `FloorCatalog`. Every run
generates new caves (`Seed = null`); set a seed to replay the exact same campaign.

The first restore downloads the Raylib native library for your platform (see
`ARCHITECTURE.md` for the exact package version).

## Controls

| Key | Action |
| --- | --- |
| W / S or arrows (title) | Select `DESCEND` or `LEAVE` |
| Enter or Space (title) | Confirm the highlighted entry |
| W A S D / arrow keys | Walk: a tap is one tile, a hold keeps walking at the cadence |
| Shift (hold) | Sprint, while the stamina bar lasts |
| B or Space | Start aiming a barrier (the game pauses) |
| R | Start aiming a wisp (the game pauses) |
| WASD / arrows (while aiming) | Move the aim cursor anywhere on the map |
| B, Space or Enter (while aiming) | Place the aimed barrier or wisp on the cursor |
| Esc (while aiming) | Cancel the aim |
| A / D, arrows (draft) | Move between the cards |
| Enter, Space or 1–4 (draft) | Take the highlighted card |
| S (draft) | Skip: take a heart instead |
| R or Enter (after a run) | Descend again, on a new maze |
| Q or Esc | Quit |

## How to play

The game opens on a title screen over the cave: `BIOLUMINESCENT / DEPTHS`, with `DESCEND` and
`LEAVE`. Starting a run (or pressing R after a run ends) builds a brand-new campaign: floor 1,
three hearts, every budget full. A floor intro card names the floor (The Shallows, Rootbound,
Drowned Halls) before play begins.

A run is three floors. Clearing a floor's arch shows a floor-cleared card; Enter opens the
**draft**: three cards, each naming its own price in the same sentence as its benefit ("Vision
+2, but the killer's light reaches 3 steps further"). Take one with Enter or its number, or skip
with S for one heart. Whatever you take applies to every floor from then on, and the draft screen
shows the run's totals underneath the title. Then the next floor begins with the hearts you have
left. The third arch wins the run and shows a summary; there is no draft after it. Losing all
three hearts ends the run whichever floor you are on.

The window is an underground cave lit by living light. You are a small hooded figure carrying
a lantern; the killer is a tall, spindly silhouette with pink rim light and two pale eyes; keys
are magenta beacons — a pool of light on the floor, a shaft rising out of the cell, a pulsing
ring and glowing fungus at the core, so they read at the edge of the torch; the exit is a stone
arch (cyan once open); barriers are thorny
barricades of cave moss. Mushrooms, lichen and spore motes glow green, pink and cyan in the
dark. The terminal view uses `@` for you, `K` for the killer, `k` for a key, `E` for the exit,
`#` for a sealed barrier, `=` for a spent one, `*` for a wisp and `X` for the aiming cursor.

Walking is timed rather than instant: a step takes about a tenth of a second, a press during that
window is remembered for the next step, and holding a direction keeps walking at that cadence.
Hold Shift to sprint for 40% more speed while the stamina bar lasts (it only drains while you are
actually moving, so holding Shift on the spot costs nothing); when it runs dry you have to wait for
a quarter of it to come back. Sprint is for breaking line of sight, not for going fast.

1. Collect all three keys — the exit stays `LOCKED` until the last one is picked up.
2. Reach the exit. You start with three hearts, shown beside the control hints at the bottom:
   a catch costs one heart, throws the killer to a far dead end, stuns it for 1.5 seconds and
   gives you 1.5 seconds of grace, and **keeps your keys and barriers**. Only the third catch
   ends the run, so a mistake costs time rather than progress. The screen washes red when you
   lose a heart, and the last heart pulses.
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
  Game/                      GameConfig, GameStatus, GameState (rules + enemy AI), GameFactory,
                             FloorDefinition/FloorCatalog, Campaign (run sequence, carried hearts)
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
warning when the killer is close, and the heart row sits in the footer next to the hints. Walls
are drawn in two passes (all rock, then all lit edges)
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

Beyond the course scope, [`FUTURE.md`](FUTURE.md) lays out the roguelike conversion in nine
phases — hearts, a three-floor campaign, a card draft and smooth timed movement (all four done),
followed by a generated pixel-art renderer, synthesised audio, twenty themes, and polish.

## Contributing

Read [`HANDOFF.md`](HANDOFF.md) first, then [`CONTRIBUTING.md`](CONTRIBUTING.md).
Every change must leave `dotnet build` warning-free and `dotnet test` green.

## Team

COP3503H — Group 5. Contributors are listed in the course submission; add your name
here when you make your first commit.
