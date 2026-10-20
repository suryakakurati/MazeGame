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
references/                    team reference art used for the palette (not loaded by the game)
src/MazeGame.Core/
  Abstractions/                interfaces and the seeded random source
  Maze/                        disjoint set, grid/cell model, maze generators (perfect + braid)
  Pathfinding/                 binary min-heap and Dijkstra (the enemy AI lives in GameState)
  Game/                        GameConfig, GameStatus, GameState (rules, enemy AI),
                               FloorDefinition/FloorCatalog, Campaign (run sequence, hearts)
src/MazeGame.UI/
  Frontends/                   RaylibFrontend (window loop) + ConsoleFrontend (fallback loop)
  Input/                       GameCommand + GameCommands (one key-to-rule mapping)
  Rendering/                   IRenderer, RaylibRenderer (fog, vignette, HUD; campaign cards in
                               RaylibRenderer.Campaign.cs), ConsoleRenderer
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
| `IMazeGenerator` | Core / Abstractions | `Grid Generate(MazeOptions options)`; returns a fully connected grid. `MazeGenerator` gives a perfect maze; decorators may add loops. Lets tests and future maze styles swap strategies. | done (Phase 3–6) |
| `MazeGenerator` | Core / Maze | Randomized Kruskal: shuffle candidate walls, remove a wall only when `DisjointSet.Union` succeeds. Produces a perfect maze from a seed. | done (Phase 3) |
| `DeadEndBraider` | Core / Maze | Decorator that opens one extra wall at `Percent` of the maze's dead ends. Removing a wall can never create a dead end, so it only removes traps (57 → 18.8 per 15x15 map at the shipped 55%); shared walls count as braiding both cells, corridor ends with no interior wall are skipped, and the boundary is never touched. | done (Phase 6) |
| `BraidedMazeGenerator` | Core / Maze | Decorator over another generator: after it runs, removes `ExtraPassages` standing interior walls (east/south candidates only, shuffled), adding that many loops without ever opening the boundary. Clamps when asked for more walls than exist. | done (Phase 6) |
| `MinHeap<T>` | Core / Pathfinding | Hand-written binary min-heap over a flat list with sift-up/sift-down; injected `IComparer<T>` decides order. `Peek` O(1), `Insert`/`ExtractMin` O(log n). No library priority queue. | done (Phase 4) |
| `IPathfinder` | Core / Abstractions | `FindPath(grid, start, goal, Func<Cell,int> cost)` returning the cells in travel order, or an empty list when unreachable. Knows nothing about obstacles — only weights. | done (Phase 4) |
| `DijkstraPathfinder` | Core / Pathfinding | Dijkstra: cached per-cell best distance, predecessor links for rebuilding the route, stale-entry deletion instead of decrease-key, early exit when the goal is settled. Weights come from the caller (obstacle cost is a Phase 5 concern). | done (Phase 4) |
| `GameConfig` | Core / Game | Every tunable number (size, key count, obstacle cost 5, barrier budget 3, seal 10 s, tear-down 3 s, sealed cost 40, wisp count 2 / lifetime 6 s, enemy step time, vision radius 3, dead-end braid 55%, extra loops 8, killer aura radius 2 / distance 6, starting hearts 3 / ceiling 3, invulnerability 1.5 s, respawn distance 8, respawn stun 1.5 s, seed), init-only, with `Validate` enforcing the "maze is big enough for player + killer + exit + keys" rule and the heart ranges. | done (Phase 5–7) |
| `GameStatus` | Core / Game | `InProgress` / `Won` / `Caught`. On one floor, `Won` means its exit was reached (in a campaign that is progress, not victory); `Caught` means the last heart was spent and ends the run. A game leaves `InProgress` once and never returns. | done (Phase 5–8) |
| `FloorDefinition` | Core / Game | One floor's data: number, display name, theme id, maze size, key count, killer step time and heart ceiling, with `Validate`. Everything needed to build a floor; no drawing knowledge (the theme id is a string). | done (Phase 8) |
| `FloorCatalog` | Core / Game | The shipped three floors, in order, as a read-only snapshot; `ByNumber` looks one up. The content seam: a fourth floor is a new record here, not a new code path. | done (Phase 8) |
| `CampaignStatus` | Core / Game | `InProgress` / `Won` / `Lost` for the whole run, so `GameStatus.Won` can keep meaning "this floor" without overloading one enum. | done (Phase 8) |
| `Campaign` | Core / Game | One run: the floor list (snapshotted and validated), the current `GameState`, and the hearts carried between floors. `TryAdvanceFloor` builds the next floor with the hearts left (out of its own ceiling); `Status` maps a caught floor to `Lost` at once and a cleared final floor to `Won`; `FloorsCleared`, `IsOnLastFloor`, `IsFloorCleared` feed the UI. | done (Phase 8) |
| `GameState` | Core / Game | The single public entry point for the UI. Owns maze, player, killer, exit, uncollected keys, barriers and their seals, the wisp, the visible-cell set, the killer's aura cells, the player's hearts, invulnerability and the killer's stun, and status. Commands: `TryMove`, `Tick`, and the aiming set `BeginBarrierPlacement` / `BeginLurePlacement` / `MoveAimCursor` / `ConfirmPlacement` / `CancelPlacement`; `BarrierSeals`, `Lure`, `IsVisible(Cell)`, `Hearts`/`MaxHearts`, `IsInvulnerable`, `IsEnemyStunned` feed the renderers, `EnemyGlow` says which cells are lit by the killer. Contains the enemy AI (two cached Dijkstra routes compared to pick the player or the wisp, step time scaled by cell cost, seal tear-down as its own action), the catch rule (`TakeHit`: one heart, respawn at the furthest reachable non-objective cell, stun, grace window) and the win/lose rules. | done (Phase 5–7) |
| `GameFactory` | Core / Game | Builds floors and runs: `CreateCampaign()` for the shipped three floors, `CreateFloor(floor, hearts)` for one floor, and `Create()` for a single game from the base config (used by tests/tools). Each floor gets a random source seeded from the run seed plus its number (`Func<int?, IRandom>`), goes through the shipped maze pipeline (`MazeGenerator` → `DeadEndBraider` → `BraidedMazeGenerator`), and is built with `GameConfig.ForFloor`. Validates its config in the constructor. Starting or restarting a run is one call. | done (Phase 6–8) |
| `IRenderer` | UI / Rendering | `Draw(GameState)`; draws a frame and never changes the game. | done (Phase 5) |
| `ConsoleRenderer` | UI / Rendering | ASCII view: maze walls, `@` player, `K` killer, `k` key, `E` exit, `#` sealed barrier, `=` spent barrier, `*` wisp, plus a HUD line starting with `hearts n/n`. Fog-aware: unseen cells are blank, remembered ones are `.`; entities only while visible. | done (Phase 6–7) |
| `RaylibRenderer` | UI / Rendering | Hearts (filled, dim, pulsing on the last) in the footer beside the hints, and a 0.35 s red wash when one is lost (`TrackDamage` compares the rule value each frame; no event stream yet). Keys are drawn as beacons (floor pool, light shaft, ring, bobbing core, orbiting sparks) **after** the lantern falloff, because a lighting pass over contents hides exactly the things the player must see. The rest of the window is drawn as a bioluminescent cave: rounded stone floors, rock walls in two passes (dark rock, then lit rim/moss/lichen), glowing mushrooms, a hooded lantern-bearer, a spindly killer, fungus keys, an arch exit, thorn barriers (a breathing green cage while sealed, a dim husk once spent), a cyan wisp with orbiting sparks, green/pink/cyan spores, background blooms, grain, vignette, glass HUD pills (keys, barriers, wisps, exit), a mode-coloured aim reticle and end card. Also draws the title screen (`DrawTitle`), the campaign's floor intro, floor-cleared and run-summary cards (`RaylibRenderer.Campaign.cs`), and `Reset()` clears its explored-cell memory between floors. Reads visibility from Core. | done (Phase 6–8) |
| `RaylibLayout` | UI / Rendering | Window geometry shared by the frontend and the renderer: 40 px cells, margins, HUD/footer strips, `WindowSize(config)`, `CellX`/`CellY`, window title. | done (Phase 6) |
| `GameCommand` | UI / Input | Backend-independent player intentions (`MoveNorth`…`BeginAim`, `BeginLureAim`, `ConfirmAim`, `CancelAim`, `Quit`). | done (Phase 6) |
| `GameCommands` | UI / Input | Applies a command to `GameState`; the one place that knows direction keys move the aim cursor instead of the player while aiming. | done (Phase 6) |
| `RaylibFrontend` | UI / Frontends | Opens the window, owns the screen flow (title → floor intro → play → floor-cleared → next floor → run summary / lost card), maps keys, brackets drawing, and starts a run by calling the injected `Func<Campaign>`. Advances floors on Enter after a clear, restarts on R/Enter after a run ends, puts the floor number in the window title, and returns `false` when no window is available. | done (Phase 6–8) |
| `ConsoleFrontend` | UI / Frontends | Terminal campaign loop with the same command mapping, an ASCII title, a floor heading, carried-hearts line, auto-advance when input is redirected, and a run summary, plus the `--console`/fallback role. | done (Phase 6–8) |

## Data flow of one frame (as implemented)

1. `Program` builds the config and a `GameFactory`, then runs
   `RaylibFrontend.TryRun(config, factory.CreateCampaign)` — or
   `ConsoleFrontend.Run(factory.CreateCampaign)` when `--console` was passed or no window could
   be opened. Each run starts with a fresh campaign, so a replay is the same call as the first
   descent and always starts on floor 1 with full hearts.
2. The window opens on the title screen (`RaylibRenderer.DrawTitle`). The frontend owns the
   selection; confirming `DESCEND` calls the factory, resets the renderer's explored cells, shows
   the floor intro card for 1.6 s (Enter/Space skips it) and switches to the game screen. `LEAVE`
   or Esc closes the window.
3. The frontend reads its own keyboard API and translates a press into one `GameCommand`.
   `GameCommands.Apply` performs it: normally `TryMove(direction)` or
   `BeginBarrierPlacement()` / `BeginLurePlacement()`; while `IsAiming` is true the same
   direction keys go to `MoveAimCursor`, B/Space/Enter confirm and Esc cancels. No game rules
   live in the UI.
4. The frontend measures real elapsed time and calls `GameState.Tick(deltaSeconds)`, which
   advances the killer at most one step.
5. When a floor's exit is reached, `GameStatus.Won` makes the frontend show the floor-cleared
   card; Enter calls `Campaign.TryAdvanceFloor()`, which builds the next floor with the hearts
   left. On the last floor the run summary card appears instead. A lost run shows the end card
   over the last frame; R or Enter starts a fresh campaign. Quitting returns the run so the
   outcome can be printed.
6. `RaylibRenderer.Draw(state)` fills in the frame (the frontend brackets it with
   `BeginDrawing`/`EndDrawing`): night gradient, cells, killer halo, dust, HUD. `ConsoleRenderer.Draw(state)`
   rebuilds the ASCII frame and writes it. Both ask `GameState.IsVisible` what they may show,
   use `EnemyGlow` to mark the killer's light, and keep their own memory of explored cells.
7. The window runs at `Raylib.SetTargetFPS(60)`; the console loop sleeps 8 ms per frame. The
   game clock never comes from the frame rate — always from measured time passed to `Tick`.

## Game rules (Phase 5–6)

- **Placement.** A seeded shuffle picks the player's cell; the killer takes the furthest
  dead end (a leaf, so it can never cut off an objective); the exit and the keys take the
  next distinct cells. `GameConfig.Validate` guarantees there are enough cells.
- **Movement.** Walls are the only thing that blocks the player. Stepping onto the
  killer loses; stepping onto the exit wins only with zero keys left; a locked exit can be
  walked over (blocking it could soft-lock a corridor).
- **Barriers.** Aimed anywhere with a cursor while the clock is paused, then placed;
  one per cell, limited by `BarrierBudget`. A fresh barrier is sealed for
  `BarrierSealSeconds`, which prices the cell at `SealedBarrierCost` (40) — more than any
  detour — so the killer routes around it. If the seal is the only route it spends
  `BarrierBreakSeconds` tearing the barrier apart. Once the seal lapses, the husk costs
  `ObstacleCost` (5) normal steps. Nothing is ever impossible.
- **Wisps.** Aimed with the same cursor, one at a time, limited by `LureCount`. For
  `LureSeconds` the killer targets whichever of the player and the wisp has the cheaper
  Dijkstra route (ties to the player); reaching or outlasting the wisp removes it. It never
  blocks and never hides anything, so it cannot affect reachability.
- **Enemy.** Dijkstra route to the chosen target, cached and invalidated on player movement,
  placement, seal expiry, wisp consumption or wisp expiry. It waits
  `EnemyStepSeconds * cost(next cell)` before each step (0.6 s normally, 3 s into a spent
  barrier), or `BarrierBreakSeconds` before tearing a seal apart, at most one step per `Tick`.
- **Maze shape.** The graded Kruskal generator builds a perfect maze; `DeadEndBraider` opens a
  second exit at 55% of its dead ends (so a chase rarely ends in a corner) and
  `BraidedMazeGenerator` removes `ExtraLoops` further walls (so a barrier can redirect the
  killer, not merely stall it).
- **Visibility.** `IsVisible(cell)` = the player's own cell plus every cell within
  `VisionRadius` steps through open passages, so walls block sight. Once the killer is within
  `EnemyAuraDistance` steps, the cells within `EnemyAuraRadius` of it are visible too (the
  killer's own light, `EnemyGlow`). Recomputed when the player *or the killer* moves;
  renderers only darken what it reports.
- **Hearts.** The player starts each floor with `StartingHearts` (3) out of `MaxHearts` (3). A
  catch (the killer steps onto the player, or the player walks onto the killer) spends one heart
  and does **not** end the floor: the killer is teleported to the furthest reachable cell, dead
  ends preferred and keys/exit/wisp/barriers avoided while possible, then stunned for
  `RespawnStunSeconds`; the player is invulnerable for `InvulnerabilitySeconds` (the killer waits
  beside their cell instead of sharing it, and a step into it is refused). Keys, barriers, the
  wisp and the layout survive, so damage costs tempo rather than progress. At zero hearts
  `GameStatus.Caught` ends the run.
- **Floors and the run.** A run is the ordered `FloorCatalog` sequence. Clearing a floor's exit
  sets `GameStatus.Won` for that floor only; `Campaign.TryAdvanceFloor` then builds the next
  floor with `StartingHearts` equal to the hearts left and `MaxHearts` from the floor, so damage
  is remembered across the run. The run is `CampaignStatus.Won` only after the final floor's
  exit, and `CampaignStatus.Lost` as soon as any floor is `Caught`. Seeded floors use
  `seed + (number - 1) * 7919`, so a seeded run reproduces exactly while its layouts differ.
- **End.** For one floor, `Won` or `Caught` (last heart); after that every command and tick is a
  no-op. A campaign maps those to progress or a run ending as above.

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
`BraidedMazeGenerator` is tested separately and has the opposite expectation: still fully
connected, still closed at the boundary, but with exactly `cellCount - 1 + ExtraPassages`
passages, so the extra loops are counted rather than assumed. `DeadEndBraider` is tested by
counting dead ends rather than walls: zero left at 100%, fewer at a higher share, a perfect
maze at 0%, and at most one new passage per dead end (two neighbouring dead ends can share the
wall that braids both).

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
