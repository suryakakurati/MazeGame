# HANDOFF — read this first

If you are a new AI (or a teammate) picking this project up with no memory of the
previous session, read the files below **in this order** before touching code.

## Read order

1. `HANDOFF.md` — this file: conventions, gotchas, where to start.
2. `PROGRESS.md` — what works right now, what was in flight, the next 3-5 actions.
3. `DECISIONS.md` — why the code looks the way it does. Do not reverse a decision
   without adding a new entry.
4. `ARCHITECTURE.md` — projects, folders, class responsibilities, package versions.
5. `CONTRIBUTING.md` — style rules and step-by-step guides for extending the game.
6. `README.md` — user-facing description, build/run/test instructions.

Then run the build and the tests **before** changing anything:

```bash
dotnet build     # expect: 0 Warning(s), 0 Error(s)
dotnet test      # expect: all tests pass
```

If they are not green, fixing that is the first task, not the new feature.

## Project state at handoff (2026-10-20)

Phases 1–6 are complete: solution scaffold, three projects, shared build settings, style
rules, all documentation, both graded algorithms (`DisjointSet` with path compression and
union by size; `MinHeap<T>` with sift-up/sift-down), the maze pipeline (`IRandom`,
`Direction`/`Cell`/`Grid`, randomised-Kruskal `MazeGenerator`, `DijkstraPathfinder`), and
a **playable game** in a Raylib window: `GameConfig`, `GameStatus`, `GameState` (placement,
movement, keys, locked exit, barriers, timed killer AI, visibility/fog, win/lose),
`IRenderer` with `RaylibRenderer` and `ConsoleRenderer`, the `Frontends/` + `Input/` split,
`GameFactory` (builds each run), and `Program` as the composition root. The window opens on a
title screen and the end card offers `R / ENTER: DESCEND AGAIN`, which builds a fresh cave and
game in the same window; the console has the same title and `[R] descend again` prompt.

The maze the UI plays is shaped by two decorators over the graded perfect generator:
`DeadEndBraider` opens one wall at 55% of the dead ends (57 → 18.8 per 15x15 map, so a chase
rarely ends in a corner) and `BraidedMazeGenerator` removes `ExtraLoops` (8) more interior
walls, so a barrier can redirect the killer rather than only stall it. A barrier now **seals**
its cell for 10 s (the killer routes around it, and tears it down in 3 s only when it is the
only way), and a wisp **lure** (2 charges, 6 s) makes the killer chase whichever of the player
and the wisp is cheaper by Dijkstra. The killer carries a distance-gated aura (visible within 6
steps, radius 2), and the window is drawn as a bioluminescent cave in green/pink/cyan, with the
team's reference art in `references/image.png` as the mood board.

279 tests pass, including perfect-maze invariants over 50 seeds, shortest-path optimality
against an independent BFS, all Phase 5 rules, the seal and tear-down rules, the wisp's target
choice and lifetime, the dead-end braid, the fog and aura rules, the replay contract in
`GameFactory`, and a long random-play invariant test that now places both placeables and still
proves the killer always has a route.
Verified this session: build 0 warnings/0 errors, 279/279 tests, the wall-clock and detour
measurements in `PROGRESS.md`, 321 title/replay frames through the real renderer (both menu
states, a run played to `Caught`, then a fresh game drawn) with no exception at 1.76 ms/frame
for the title, and the window staying alive on the title with an empty log. Earlier: 136
gameplay frames with seals and a wisp (0.70–1.04 ms/frame draw submission, 2.3–3.5 ms including
the frame swap) and 482 console frames. The window's pixels could not be
captured on this machine (no GUI framebuffer available to the agent), so one human look at the
window is still owed (see `PROGRESS.md` → Known Issues).

Phase 7 (play-test tuning, optional audio, polish) is next; `PROGRESS.md` → **Next Steps** has
the ordering.

## Conventions in use

- Follow the brief: SOLID, composition over inheritance, no static mutable state, no
  god classes, no premature abstraction.
- One public type per file; the file name matches the type name; namespaces mirror
  folders (`MazeGame.Core.Maze`, `MazeGame.Core.Pathfinding`, ...).
- XML docs (`///`) on every public type and member. The build **fails** without them
  (CS1591 is an error in Core and UI).
- Warnings are errors. Suppress narrowly and with a comment if you truly must.
- Randomness only through `IRandom`; time only as a `double deltaSeconds` argument.
- Tunable numbers belong in `GameConfig`, never inline in logic.
- Validate public inputs and throw `ArgumentException` / `ArgumentOutOfRangeException`;
  never swallow exceptions.
- Tests: xUnit, descriptive names with underscores
  (`Find_AfterUnion_ReturnsSameRoot`), fixed seeds, fast, no I/O.

## Gotchas

- **`dotnet` may not be on `PATH`.** On the machine where Phase 1 was set up, the SDK
  was installed into `~/.dotnet`, so use `~/.dotnet/dotnet build`. Run
  `dotnet --version` first; you need an 8.0.x SDK.
- **The repository path contains a space** (`.../cs2 projecgt/mazegame`). Quote paths
  in shell commands and avoid unquoted `$(...)` expansions around it.
- **Tests are discovered per folder.** Add a test class under
  `tests/MazeGame.Tests/Maze/`, `Pathfinding/` or `Game/` to match the Core folder it
  covers. `dotnet test --filter FullyQualifiedName~DisjointSet` runs one class.
- **The UI depends on Raylib-cs 8.1.0**, which carries per-platform native binaries; a fresh
  restore downloads them. `RaylibFrontend.TryRun` returns `false` when no window can be
  opened and `Program` then runs `ConsoleFrontend`, so the game still works over SSH — keep
  that fallback alive. Do not add packages unless the task needs them; when you do, list the
  exact version in `ARCHITECTURE.md`.
- **The window loop owns `BeginDrawing`/`EndDrawing`.** `RaylibRenderer.Draw` only fills in a
  frame; calling Raylib drawing outside a bracket crashes. Anything that must run per frame
  belongs in `RaylibFrontend.RunLoop`, not in the renderer.
- **The art is one palette block plus procedural drawing.** Colours and the drawing helpers live
  in `RaylibRenderer`; the look is a bioluminescent cave (green/pink/cyan on near-black teal,
  rounded stone, glowing lichen, hooded explorer, spindly killer). Every detail comes from a
  hash of the cell (or of the mote/grain index and clock), so do not introduce `Random` or
  per-frame state for it, and do not load textures — the project has no asset pipeline.
- **Walls are drawn in two passes.** `DrawCellBase` paints floors and the dark rock of every
  wall; `DrawCellDetails` then paints the lit rim, moss and lichen. A wall is drawn by both cells
  that share it, so a single pass would let the neighbour's dark stroke cover the first cell's
  rim. Keep that split if you add wall decoration.
- **Cells are 40 px and the window is 656x760 for 15x15.** `RaylibLayout` owns this, and the
  characters are sized against it; changing `CellSize` means re-checking the figure sizes in
  `RaylibRenderer`.
- **Fog of war is a rule, not a drawing effect.** `GameState.IsVisible(cell)` is the only
  source of truth (own cell + `VisionRadius` steps through passages, cached, recomputed on
  movement). Renderers may decide how to darken, and each keeps its own explored-cell memory;
  never let a renderer invent visibility, and never put its memory in Core.
- **Game rules live in `GameState`, not in `Program`.** The loop only maps keys to
  `TryMove` / the aiming commands / `Tick` and draws. If you are tempted to add a
  rule to the renderer, add it to Core plus a test instead.
- **Aiming is a Core mode, not a UI mode.** `PlacementMode` (barrier or wisp) freezes the
  clock (`Tick` returns early) and `TryMove` is refused until the aim is confirmed or
  cancelled, so the pause cannot be bypassed by a different renderer. Both placeables share
  `AimCursor` / `MoveAimCursor` / `ConfirmPlacement` / `CancelPlacement`; only the
  `Begin*Placement` entry points differ, because what can be afforded differs. Do not add a
  second cursor or a second freeze for a third placeable.
- **A locked exit is walkable on purpose.** Blocking it could wall the player off in a
  corridor. Win = standing on the exit with zero keys left.
- **The killer starts on the furthest dead end.** A perfect maze is a tree, so a killer in
  a corridor would cut off everything behind it (the player cannot walk through it). The
  dead end is a leaf, so it never disconnects the maze. Do not "simplify" this to an
  opposite-corner rule; a test (`AWholeGame_CanBeWonOnAGeneratedMaze`) caught that bug.
- **`Tick` performs at most one killer step** and banks at most one step interval, so a
  long frame cannot teleport the killer. Tests advance time with repeated `Tick(1.0)`.
- **Both frontends share one key mapping.** Keys are translated into `GameCommand` values and
  applied by `GameCommands`; add a new control there once, and both the window and the
  terminal get it. Escape cancels while aiming and quits otherwise, in both frontends.
- **A barrier has two prices.** While sealed it is worth `SealedBarrierCost` (40) to the
  killer, so it detours; when only that cell connects the two of you it spends
  `BarrierBreakSeconds` (3) tearing the barrier apart, and the barrier is destroyed. Once the
  seal lapses the husk costs `ObstacleCost * EnemyStepSeconds` = 3 s (`5 * 0.6`). Every price
  is finite on purpose: that is the no-soft-lock invariant. If a barrier seems inert, check
  `GameState.CostOfEntering`, `SecondsUntilNextStep` and `GameStateSealTests` first.
- **Tearing a seal is a step of its own.** `AdvanceEnemy` calls `BreakSeal` instead of moving
  when the next cell is sealed, so the tear-down is visible as a pause followed by the barrier
  vanishing, rather than a slow walk through it.
- **Starting a run is `GameFactory.Create()`.** The factory owns the maze pipeline and gives
  each game its own random source, so a restart is the same call as the first descent. Do not
  assemble a maze in a frontend, and do not add restart state to a loop; call the factory.
- **The title screen is a renderer partial, and the frontend owns the menu.** `DrawTitle` takes
  the selected index and draws no game state; `RaylibFrontend` holds the selection and the
  screen state. Raylib's built-in font is ASCII only, so no decorative glyphs.
- **Reset the renderer between runs.** `RaylibRenderer` remembers explored cells and `Cell` is a
  value type, so cells of a new maze compare equal to the old ones: a run that starts without
  `renderer.Reset()` draws the previous cave's walls.
- **Shaping the maze is decorators, not an edit to `MazeGenerator`.** `MazeGenerator` must
  keep producing a perfect maze for the graded requirement; `DeadEndBraider` and
  `BraidedMazeGenerator` wrap it. The braid is tested by counting **dead ends** (it removes
  traps and can never create one), the loop decorator by counting passages
  (`cellCount - 1 + loops`). Do not move either into the Kruskal class.
- **The killer's aura is part of `IsVisible`, not the renderer.** Within `EnemyAuraDistance`
  (6) steps its `EnemyAuraRadius` (2) surroundings are visible, and `EnemyGlow` says which
  cells came from that light. Because the aura follows the killer, `AdvanceEnemy` recomputes
  visibility — if you add another moving light source, it needs the same treatment.
- **The wisp is a target, not a status effect.** The killer's target is chosen by comparing
  the cost of two `FindPath` results (`RouteCost`), ties to the player. Anything that changes
  where the killer walks must go through that comparison, and `ClearLure`/`BreakSeal` must
  invalidate the cached route — otherwise the killer keeps walking to a wisp that no longer
  exists.
- **The UI must not contain game rules.** If the UI needs data, add a read-only
  member to `GameState`; do not reach into maze internals.
- **Walls are changed only through `Grid`.** `Cell` is an immutable value type with no
  public constructor; `Grid.RemoveWall` updates the cell *and* its neighbour so a
  passage is never open from one side only. Read walls with `Cell.HasWall` / `Grid.HasWall`.
- **`Direction` values are bit flags (1, 2, 4, 8), not 0-3.** Never use
  `(int)direction` as an array index; use `Enum.GetValues<Direction>()`.
- **`IRandom`'s method is `NextInt`, not `Next`** (CA1716: `Next` is a reserved word in
  other .NET languages), and it rejects a bound of 0 or less because
  `Random.Next(0)` silently returns 0. Both points are in `DECISIONS.md`.
- **Maze sizes are validated by the `Grid` constructor only.** Do not add a second
  copy of the bounds check; build a `Grid` and let it throw.
- **`Cell` has no public constructor**, so tests cannot invent one: get cells from
  `grid.GetCell(row, column)` and compare those. `default(Cell)` is `(0, 0)` with no
  walls and is not a valid way to express a cell.
- **`IPathfinder` costs are "weight of entering the cell"**, start included in the path
  but not charged, weights must be at least 1, and an empty result means genuinely
  unreachable. A cost of 25 is a speed bump, never a wall — do not turn it into
  "impassable" without a `DECISIONS.md` entry, because the enemy must always have a route.
- **`obj/` and `bin/` are ignored** but may already exist locally. `dotnet clean` if a
  build looks stale.
- **Do not reorder folders or rename public APIs** without a `DECISIONS.md` entry and
  a `PROGRESS.md` note.

## End-of-session checklist

- [ ] `dotnet build` — 0 warnings, 0 errors.
- [ ] `dotnet test` — all green, new code covered by tests.
- [ ] XML docs on new public members; no magic numbers outside `GameConfig`.
- [ ] `PROGRESS.md` updated: Current Task, Next Steps, Completed, Known Issues.
- [ ] `DECISIONS.md` / `ARCHITECTURE.md` / `README.md` / `CONTRIBUTING.md` updated in
      the same change as the code they describe.
