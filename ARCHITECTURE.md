# ARCHITECTURE

This document explains which class does what, how the three projects fit together,
and which third-party packages are in use. Update it whenever the structure changes.

## Layering

The game is split so that **all rules live in `MazeGame.Core`** and every other
project only interprets or presents them:

- `MazeGame.Core` — pure C# logic, no rendering, no input, no UI framework. It never
  calls `System.Random` or `DateTime.Now` directly; randomness and time are injected.
- `MazeGame.UI` — Raylib window (or the console fallback), keyboard input, drawing, frame
  timing. It talks to Core **only** through `GameState` (plus read-only model types), so the
  whole UI framework could be replaced without touching game logic.
- `MazeGame.Tests` — xUnit tests for Core. References Core; nothing references the
  tests.

## Dependency diagram

```
                MazeGame.Tests  (xUnit)
                      │ references
                      ▼
   ┌──────────────  MazeGame.Core  ───────────────┐
   │  Abstractions   IRandom, SeededRandom,        │
   │                 IMazeGenerator, IPathfinder   │
   │  Maze           DisjointSet, Direction, Cell, │
   │                 Grid, MazeOptions,            │
   │                 MazeGenerator                 │
   │  Pathfinding    MinHeap<T>, DijkstraPathfinder│
   │  Game           GameConfig, GameStatus,       │
   │                 GameState                     │
   └───────────────────────▲───────────────────────┘
                           │ references (GameState only)
                MazeGame.UI (Raylib window, console fallback)
                  Frontends/  Input/  Rendering/
                  IRenderer ← RaylibRenderer, ConsoleRenderer
                  GameCommand → GameCommands → GameState
```

Rule of thumb: an arrow points from caller to callee. Core never points back at UI,
and Tests never point at UI.

## Project layout

```
MazeGame.sln
Directory.Build.props          shared compiler settings for all projects
.editorconfig                  style + naming rules
src/MazeGame.Core/
  Abstractions/                interfaces and the seeded random source
  Maze/                        disjoint set, grid/cell model, maze generator
  Pathfinding/                 binary min-heap and Dijkstra (the enemy AI lives in GameState)
  Game/                        GameConfig, GameStatus, GameState (rules, enemy AI)
src/MazeGame.UI/
  Frontends/                   RaylibFrontend (window loop) + ConsoleFrontend (fallback loop)
  Input/                       GameCommand + GameCommands (one key-to-rule mapping)
  Rendering/                   IRenderer, RaylibRenderer (fog, vignette, HUD), ConsoleRenderer
  Program.cs                   composition root: builds the game, picks a frontend
tests/MazeGame.Tests/
  Abstractions/  Maze/         one test class per Core class, mirroring Core folders
  Pathfinding/  Game/          GameTests use hand-built mazes from TestDoubles/TestLevels
  TestDoubles/                 FixedRandom (no-op shuffle) and known-shape test mazes
```

## Class responsibilities

"Status" is `done` for code that exists and is tested, with the phase that delivered it.

| Type | Project / folder | Responsibility | Status |
| --- | --- | --- | --- |
| `Program` | UI / root | Composition root: builds Core objects and hands the game to a frontend. Supports `--console`. | done (Phase 6) |
| `DisjointSet` | Core / Maze | Union-Find over cell indices with path compression and union by size. The maze generator uses it to avoid cycles. | done (Phase 2) |
| `IRandom` | Core / Abstractions | `NextInt(maxExclusive)` and `Shuffle<T>(IList<T>)`. The only way Core obtains randomness, so tests can pin the sequence. | done (Phase 3) |
| `SeededRandom` | Core / Abstractions | Deterministic `IRandom` wrapper around `System.Random`: a non-null seed reproduces a sequence, `null` gives a fresh one. Also rejects non-positive bounds. | done (Phase 3) |
| `Direction` | Core / Maze | The four cardinal directions as bit flags (1, 2, 4, 8) so a cell can store its walls in one byte. | done (Phase 3) |
| `DirectionExtensions` | Core / Maze | `Opposite()` and `Delta()` (the row/column step) with validation of unknown values. | done (Phase 3) |
| `Cell` | Core / Maze | Immutable `readonly record struct`: row, column and one byte of wall bits. No public constructor; walls change only through `Grid`. | done (Phase 3) |
| `Grid` | Core / Maze | Fixed-size rectangle of `Cell`s. Validates every coordinate, converts positions to indices, removes a wall from both sides, and reports `CountPassages` for invariant checks. | done (Phase 3) |
| `MazeOptions` | Core / Maze | Plain request value: rows, columns, nullable seed. No logic, so it can come from a config file, CLI argument or test. | done (Phase 3) |
| `IMazeGenerator` | Core / Abstractions | `Grid Generate(MazeOptions options)`; lets tests and future maze styles swap strategies. | done (Phase 3) |
| `MazeGenerator` | Core / Maze | Randomized Kruskal: shuffle candidate walls, remove a wall only when `DisjointSet.Union` succeeds. Produces a perfect maze from a seed. | done (Phase 3) |
| `MinHeap<T>` | Core / Pathfinding | Hand-written binary min-heap over a flat list with sift-up/sift-down; injected `IComparer<T>` decides order. `Peek` O(1), `Insert`/`ExtractMin` O(log n). No library priority queue. | done (Phase 4) |
| `IPathfinder` | Core / Abstractions | `FindPath(grid, start, goal, Func<Cell,int> cost)` returning the cells in travel order, or an empty list when unreachable. Knows nothing about obstacles — only weights. | done (Phase 4) |
| `DijkstraPathfinder` | Core / Pathfinding | Dijkstra: cached per-cell best distance, predecessor links for rebuilding the route, stale-entry deletion instead of decrease-key, early exit when the goal is settled. Weights come from the caller (obstacle cost is a Phase 5 concern). | done (Phase 4) |
| `GameConfig` | Core / Game | Every tunable number (size, key count, obstacle cost 5, barrier budget, enemy step time, vision radius 3, seed), init-only, with `Validate` enforcing the "maze is big enough for player + killer + exit + keys" rule. | done (Phase 5) |
| `GameStatus` | Core / Game | `InProgress` / `Won` / `Caught`; a game leaves `InProgress` once and never returns. | done (Phase 5) |
| `GameState` | Core / Game | The single public entry point for the UI. Owns maze, player, killer, exit, uncollected keys, barriers, the visible-cell set and status. Commands: `TryMove`, `Tick`, and the aiming set `BeginBarrierPlacement` / `MoveBarrierCursor` / `ConfirmBarrierPlacement` / `CancelBarrierPlacement`; `IsVisible(Cell)` answers fog. Contains the enemy AI (cached Dijkstra route, invalidated when the player moves or a barrier is placed, step time scaled by cell cost) and the win/lose rules. | done (Phase 5–6) |
| `IRenderer` | UI / Rendering | `Draw(GameState)`; draws a frame and never changes the game. | done (Phase 5) |
| `ConsoleRenderer` | UI / Rendering | ASCII view: maze walls, `@` player, `K` killer, `k` key, `E` exit, `#` barrier, plus a HUD line. Fog-aware: unseen cells are blank, remembered ones are `.`; entities only while visible. | done (Phase 6) |
| `RaylibRenderer` | UI / Rendering | Graphical window: dark floor, shapes for entities, fog (black unseen, dim remembered), torch falloff by distance, slow flicker, HUD, aim reticle and end banner. Reads visibility from Core. | done (Phase 6) |
| `RaylibLayout` | UI / Rendering | Window geometry shared by the frontend and the renderer: cell size, margins, HUD/footer strips, `WindowSize(config)`, `CellX`/`CellY`. | done (Phase 6) |
| `GameCommand` | UI / Input | Backend-independent player intentions (`MoveNorth`…`BeginAim`, `ConfirmAim`, `CancelAim`, `Quit`). | done (Phase 6) |
| `GameCommands` | UI / Input | Applies a command to `GameState`; the one place that knows direction keys move the aim cursor instead of the player while aiming. | done (Phase 6) |
| `RaylibFrontend` | UI / Frontends | Opens the window, owns the frame loop (measured frame time), maps keys, brackets drawing, keeps the last frame on screen after the game ends. Returns `false` when no window is available. | done (Phase 6) |
| `ConsoleFrontend` | UI / Frontends | Terminal loop with the same command mapping and `--console`/fallback role. | done (Phase 6) |

## Data flow of one frame (as implemented)

1. `Program` builds the config, random source, maze and `GameState`, then runs
   `RaylibFrontend.TryRun(state)` — or `ConsoleFrontend.Run(state)` when `--console` was
   passed or no window could be opened.
2. The frontend reads its own keyboard API and translates a press into one `GameCommand`.
   `GameCommands.Apply` performs it: normally `TryMove(direction)` or
   `BeginBarrierPlacement()`; while `IsPlacingBarrier` is true the same direction keys go to
   `MoveBarrierCursor`, B/Space/Enter confirm and Esc cancels. No game rules live in the UI.
3. The frontend measures real elapsed time and calls `GameState.Tick(deltaSeconds)`, which
   advances the killer at most one step.
4. `RaylibRenderer.Draw(state)` fills in the frame (the frontend brackets it with
   `BeginDrawing`/`EndDrawing`); `ConsoleRenderer.Draw(state)` rebuilds the ASCII frame and
   writes it. Both ask `GameState.IsVisible` what they may show and keep their own memory of
   explored cells.
5. The window runs at `Raylib.SetTargetFPS(60)`; the console loop sleeps 8 ms per frame. The
   game clock never comes from the frame rate — always from measured time passed to `Tick`.

## Game rules (Phase 5–6)

- **Placement.** A seeded shuffle picks the player's cell; the killer takes the furthest
  dead end (a leaf, so it can never cut off an objective); the exit and the keys take the
  next distinct cells. `GameConfig.Validate` guarantees there are enough cells.
- **Movement.** Walls are the only thing that blocks the player. Stepping onto the
  killer loses; stepping onto the exit wins only with zero keys left; a locked exit can be
  walked over (blocking it could soft-lock a corridor).
- **Barriers.** Aimed anywhere with a cursor while the clock is paused, then placed;
  one per cell, limited by `BarrierBudget`. Entering a barriered cell costs the killer
  `ObstacleCost` (5) *normal steps of time*, and is never impossible.
- **Enemy.** Dijkstra route to the player, cached and invalidated on player movement or
  barrier placement. It waits `EnemyStepSeconds * cost(next cell)` before each step
  (0.6 s normally, 3 s into a barrier), at most one step per `Tick`.
- **Visibility.** `IsVisible(cell)` = the player's own cell plus every cell within
  `VisionRadius` steps through open passages, so walls block sight. Recomputed on movement;
  renderers only darken what it reports.
- **End.** `Won` or `Caught`; after that every command and tick is a no-op.

## Dependencies

The UI project now has one package (the renderer, Phase 6); the test project has the test
stack. Core still has no packages at all.

| Package | Version | Project | Why |
| --- | --- | --- | --- |
| Microsoft.NET.Test.Sdk | 17.8.0 | MazeGame.Tests | Test host required by `dotnet test`. |
| xunit | 2.5.3 | MazeGame.Tests | Test framework required by the brief. |
| xunit.runner.visualstudio | 2.5.3 | MazeGame.Tests | Lets `dotnet test` discover xUnit tests. |
| coverlet.collector | 6.0.0 | MazeGame.Tests | Optional coverage collection. |
| Raylib-cs | 8.1.0 | MazeGame.UI | Window, shapes, text and timing for `RaylibRenderer`. Ships native binaries in the package (`runtimes/<rid>/native/libraylib.dylib`, `.so`, `.dll`; osx-arm64 included), so no asset or download step is needed beyond restore. |

.NET version: 8.0 (`net8.0`), any 8.0.x SDK. There is deliberately no `global.json`.

## Maze invariants (checked by tests)

`MazeGenerator` must always produce a **perfect** maze. The test suite asserts, over 50
fixed seeds, that:

1. every cell is reachable from (0, 0) (breadth-first search over open passages),
2. the number of passages is exactly `cellCount - 1`,
3. no wall on the outer boundary is ever removed,
4. the same seed produces the identical maze and different seeds differ.

Together, (1) and (2) mean the maze is connected and acyclic, so there is exactly one
route between any two cells — which is what the enemy's Dijkstra will later exploit.

## Pathfinding notes (for the presentation)

- `MinHeap<T>` is a **complete binary tree in a flat list**: children of index `i` are
  `2i + 1` / `2i + 2`, parent is `(i - 1) / 2`. Insert appends then sifts up; extraction
  moves the last leaf to the root then sifts down. Both repair the heap in `O(log n)`.
- `DijkstraPathfinder` settles cells in increasing cost order. Every weight is positive,
  so a settled cell's cost is final. Relaxing an edge means `best[neighbour] = min(...)`
  and pushing the improved entry.
- With `V` cells and `E` edges the cost is `O((V + E) log V)` — measured at 0.05 ms for
  a 15x15 maze, which is why the enemy can recompute on every player move.
- The only source of adjacency is `Grid.HasWall`, so a route can never cross a wall; a
  test walks every path and asserts each step is a wall-free neighbour.

## Design notes

- The Core API is designed to be additive: a new tile/objective/enemy type is a new
  class implementing an existing interface plus one registration line in
  `GameConfig`/`GameState`, not an edit to many methods.
- All randomness goes through `IRandom`; all time comes in as a `double deltaSeconds`
  parameter. That is what makes `GameState` testable without a window or a clock.
- `GameState` is the only Core type the UI may depend on. If the UI needs new data,
  expose it on `GameState` (read-only) rather than reaching into maze internals.
