# PROGRESS

Single source of truth for the state of the project. Update this file at the end of
every work session (see the handoff protocol in `HANDOFF.md`).

Last updated: 2026-10-20 (Phase 6 complete: Raylib window, fog of war, vignette; barrier
delay retuned to 3 s after play-test feedback)

## Current Status

The solution builds with zero warnings, `dotnet test` is green (198 tests), and the game runs
**in a Raylib window** with fog of war: a seeded 15x15 maze is generated, three keys and an
exit are placed, the player moves with WASD/arrows, aims and plants barriers with B, and a
killer hunts them one step every 0.6 s using Dijkstra. The terminal view is still available
(`--console`) and is used automatically when no window can be opened.

What changed in this session:

- **Barrier delay retuned to 3 seconds.** The play-test feedback was that 15 s per barrier was
  too long. `ObstacleCost` is now 5 instead of 25, and at the default 0.6 s step that is
  exactly 3 s per barrier (9 s for the full budget of three). It is still a single knob and
  still charges before the killer steps.
- **Fog of war in Core.** `GameState.IsVisible(Cell)` answers what the player may know: their
  own cell plus everything within `VisionRadius` (3) *walking steps through open passages*, so
  walls block sight. The answer is cached and recomputed when the player moves.
- **Graphical renderer.** `RaylibRenderer` draws the maze on a dark floor, entities as shapes,
  fog (unseen = black, explored = dim), torch falloff by distance and a slow flicker; it also
  draws the HUD, the aim reticle and an end banner. `ConsoleRenderer` was made fog-aware so the
  fallback plays the same game rather than revealing the map.
- **Frontends.** `Program` is now a small composition root; `RaylibFrontend` owns the window and
  frame loop, `ConsoleFrontend` owns the terminal loop, and both translate keys into
  `GameCommand` values applied by `GameCommands` (one place for the key-to-rule mapping).

Verified end-to-end:

- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test` → 198/198 passing.
- Console frontend, real build: the first frame shows only the cells around the player; walls
  and contents outside the torch are blank, remembered cells are '.', the HUD and hints print.
  With `VisionRadius` raised to 99 the same code draws the complete, correctly aligned maze.
- Raylib renderer driven directly for 408 frames through fog, aiming, barrier placement and a
  caught game: no exceptions.
- The real game binary in window mode stayed alive for the whole 6 s test window with an empty
  log (SIGTERM at the end was the test stopping it).

Core contains:
- `DisjointSet` (path compression, union by size) and `MazeGenerator` (randomised Kruskal).
- `IRandom`/`SeededRandom`, `Direction`/`Cell`/`Grid`, `MinHeap<T>`,
  `DijkstraPathfinder` behind `IPathfinder`.
- `GameConfig` (every tunable, validated), `GameStatus`, and `GameState` — the single
  object the UI talks to: placement, movement, keys, exit, barriers, visibility, win/lose,
  enemy AI.

## Current Task

None in flight. Phase 6 is complete and verified by build, tests and two frontend smoke runs.
The next session starts Phase 7 (see below).

## Next Steps

1. **Play the window build and tune difficulty.** Barrier delay (3 s, `ObstacleCost`), killer
   pace (0.6 s), vision radius (3), key count (3) and maze size (15x15) are all one-number
   changes in `GameConfig`. Watch in particular whether vision radius 3 feels too blind now
   that the map is hidden.
2. **Decide the braided-maze question** (Open Question 3). Loops would let barriers redirect
   the killer instead of only delaying it; it can only be an optional generator behind
   `IMazeGenerator`, with the perfect one kept as the default.
3. **Audio** only after the scope question is answered: `Raylib.InitAudioDevice` plus a couple
   of generated tones would avoid shipping asset files, but it adds a device dependency.
4. **Polish for Phase 7:** a short rise/fall on the torch when the killer is close, a barriered
   cell that reads as "slowing" rather than "blocked", and a start screen explaining the
   controls.
5. Re-run `dotnet build`, `dotnet test` and a window play session; update all tracking files.

## Completed

- [x] 2026-10-20 — Phase 1: solution, three projects, project references,
      `Directory.Build.props`, `.editorconfig`, `.gitignore`, folder layout and docs.
- [x] 2026-10-20 — Phase 2: `DisjointSet` with path compression and union by size
      (15 tests).
- [x] 2026-10-20 — Phase 3: `IRandom`/`SeededRandom`, `Direction`/`DirectionExtensions`,
      immutable `Cell`, `Grid`, `MazeOptions`, `IMazeGenerator`, randomised-Kruskal
      `MazeGenerator`; perfect-maze invariants over 50 seeds (57 tests).
- [x] 2026-10-20 — Phase 4: `MinHeap<T>`, `IPathfinder`, `DijkstraPathfinder`; heap
      ordering, weighted detours, optimality against an independent BFS (25 tests).
- [x] 2026-10-20 — Phase 5: `GameConfig` with validation, `GameStatus`, `GameState`
      (placement, movement, key collection, locked exit, barriers, timed enemy, win/lose),
      `IRenderer` + `ConsoleRenderer`, and a playable `Program` composition root.
- [x] 2026-10-20 — Phase 5: 74 new tests (171 total), including placement distinctness,
      softlock guards, barrier budget and steering, tick pacing, game-over freezing, and a
      long random-play invariant test that re-checks "the killer always has a route".
- [x] 2026-10-20 — Phase 5 bug fixed: the killer used to start in the corner opposite the
      player, which could put it on the only route to a key (a perfect maze is a tree, so
      the player can never walk past it). It now starts in the furthest dead end, which is
      a leaf and therefore never disconnects the maze. See `DECISIONS.md`.
- [x] 2026-10-20 — Test strength checked by mutation: disabling the dead-end rule makes the
      three placement/win tests fail, and not invalidating the killer's cached route makes
      `Tick_AfterThePlayerMoves_RecomputesTheRoute` fail. Both were restored and the suite
      re-run green, so these tests guard the behaviour rather than merely passing.
- [x] 2026-10-20 — Play-test fix 1: a barrier now multiplies the killer's step time
      (`ObstacleCost * EnemyStepSeconds`) instead of only changing its route weight, which
      had no effect in a perfect maze. 7 new delay tests; mutation-checked (removing the
      delay fails 5 of them).
- [x] 2026-10-20 — Play-test fix 2: aiming a barrier pauses the game and moves a cursor with
      WASD; B/Space/Enter places, Esc cancels. `TryPlaceBarrier` was replaced by
      `BeginBarrierPlacement` / `MoveBarrierCursor` / `ConfirmBarrierPlacement` /
      `CancelBarrierPlacement`. 19 barrier tests; mutation-checked (removing the pause fails
      `Aiming_FreezesTheKiller`).
- [x] 2026-10-20 — Phase 6: `GameState.IsVisible` (breadth-first through passages, cached,
      recomputed on movement) with 7 vision tests: own cell, radius 0, exact radius reach,
      wall blocking, an L-shaped bend, following the player, and the killer staying hidden
      until it enters the light.
- [x] 2026-10-20 — Phase 6: `RaylibRenderer` (fog, remembered cells, torch falloff, flicker,
      HUD, aim reticle, end banner), `RaylibLayout` for shared geometry, `Raylib-cs` 8.1.0.
- [x] 2026-10-20 — Phase 6: `Frontends/` (`RaylibFrontend`, `ConsoleFrontend`) and `Input/`
      (`GameCommand`, `GameCommands`), so both frontends share one key-to-command mapping.
      `Program` gained `--console` and falls back to the terminal when no window is available.
- [x] 2026-10-20 — Phase 6: `ConsoleRenderer` now hides unseen cells and dims remembered
      ones; with `VisionRadius = 99` it still draws the full, aligned maze.
- [x] 2026-10-20 — Play-test fix 3: barrier delay retuned from 15 s to 3 s (`ObstacleCost`
      25 → 5, one number), with the delay tests now reading the shipped default and
      `GameConfigTests.DefaultBarrierCost_BuysThreeSeconds` pinning the balance.

## Known Issues / Bugs

- **The Raylib window could not be checked pixel-by-pixel on the machine this was built on.**
  Window creation and the frame loop were verified (the process runs, 408 renderer frames
  without exceptions), but reading the framebuffer back returned black because no GUI session
  was available to the build environment. A human must look at the window once and confirm the
  fog, falloff and flicker read the way they should. Nothing in the code depends on that check
  being redone, but it has not been done.
- **Vision radius 3 hides most of a 15x15 maze.** That is the intended horror feel, but it is
  also the most likely first difficulty complaint. Change `GameConfig.VisionRadius` to 4 or 5
  if the window feels too blind; nothing else has to change.
- **Barriers cannot cause detours in a generated maze.** A perfect maze has exactly one
  route between two cells, so a barrier always costs delay rather than a redirect; the
  "take the long way round" behaviour needs a maze with loops. The steering logic is
  implemented and tested against a hand-built looped maze, so a braided generator would
  enable it. Phase 7 decision (see Open Questions).
- **Barrier balance is still unproven.** 3 s per barrier feels closer to right, but it needs a
  group play-test; `ObstacleCost` is the only number to touch.
- **No audio yet**, and Raylib audio adds a device dependency; deferred until the scope
  question is answered.
- **`ConsoleRenderer` memory is per renderer instance.** It remembers explored cells for the
  session; a second renderer over the same game starts blind again. That is presentation state,
  not a rule, and `GameState.IsVisible` remains the single source of truth.
- On the machine where the project was set up, `dotnet` is **not** on `PATH`. A .NET 8
  SDK (8.0.416) lives in `~/.dotnet`, so commands there are `~/.dotnet/dotnet build`
  and `~/.dotnet/dotnet test`.
- The repository path contains a space (`.../cs2 projecgt/mazegame`); quote paths in
  shell commands.
- `System.Random.Next(0)` returns 0 instead of throwing, so `SeededRandom.NextInt`
  validates the bound itself. Do not "simplify" that check away (see `DECISIONS.md`).
- `Cell` has no public constructor, so tests must obtain cells from
  `grid.GetCell(row, column)`. This is deliberate.
- `ConsoleFrontend` reads the keyboard directly; with input redirected it simply watches the
  game (the console UI cannot be driven by piped input).

## Open Questions

1. **Objectives — answered:** collect keys, exit unlocks when all are collected (3 by
   default). Defaults for `KeyCount` and their scaling with maze size are still tunable in
   `GameConfig`.
2. **Skill mechanic — answered:** limited placeable barriers (default 3), obstacle cost 5
   (a 3-second delay), no expiry. Still open: whether barriers should be more than a slowdown
   (see the known issue about perfect mazes).
3. **Braided mazes for Phase 7?** Loops would make barriers genuinely redirect the killer
   and remove "the killer camps the only corridor" tension. It conflicts with the graded
   perfect-maze requirement, so it can only be an *optional* extra generator behind
   `IMazeGenerator`, with the perfect one kept as the default. Needs a team decision.
4. **Version control:** the team chose not to initialise Git
   (`DECISIONS.md` 2026-10-20). The folder carries all project state.
5. **Theme content:** enemy and item names/art are undecided; shapes and text are placeholders.
6. **Audio:** Raylib audio is optional; confirm scope before Phase 7 because it adds a device
   dependency (and possibly asset files).
7. **Difficulty tuning:** enemy step 0.6 s, 3 keys, 3 barriers, 15x15, vision radius 3. Needs a
   play-test with the group; all of it lives in `GameConfig`.

## How to Build, Run, and Test

Run these from the repository root (the folder containing `MazeGame.sln`):

```bash
dotnet build                      # must report 0 Warning(s), 0 Error(s)
dotnet test                       # currently 198/198 passing
dotnet run --project src/MazeGame.UI              # Raylib window
dotnet run --project src/MazeGame.UI -- --console # terminal fallback
```

If `dotnet` is not on `PATH` (this is the case on the setup machine):

```bash
~/.dotnet/dotnet build
~/.dotnet/dotnet test
~/.dotnet/dotnet run --project src/MazeGame.UI
```

Controls: WASD or arrow keys to move, `B` or Space to start aiming a barrier (then WASD
moves the cursor, B/Space/Enter places it, Esc cancels), `Q` or Esc to quit. The same keys
work in the window and in the terminal.
