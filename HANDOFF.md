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
and `Program` as the composition root.

198 tests pass, including perfect-maze invariants over 50 seeds, shortest-path optimality
against an independent BFS, all Phase 5 rules, the barrier delay, the aiming pause, the fog
rule, and a long random-play invariant test.
Verified this session: build 0 warnings/0 errors, 198/198 tests, the console view renders a
correct fogged frame, the Raylib renderer ran 408 frames through every visual state without
an exception, and the window build stayed alive with an empty log. The window's pixels could
not be captured on this machine (no GUI framebuffer available to the agent), so one human
look at the window is still owed (see `PROGRESS.md` → Known Issues).

Phase 7 (difficulty tuning, optional audio, polish) is next; `PROGRESS.md` → **Next Steps**
has the ordering.

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
- **Fog of war is a rule, not a drawing effect.** `GameState.IsVisible(cell)` is the only
  source of truth (own cell + `VisionRadius` steps through passages, cached, recomputed on
  movement). Renderers may decide how to darken, and each keeps its own explored-cell memory;
  never let a renderer invent visibility, and never put its memory in Core.
- **Game rules live in `GameState`, not in `Program`.** The loop only maps keys to
  `TryMove` / the barrier-aiming commands / `Tick` and draws. If you are tempted to add a
  rule to the renderer, add it to Core plus a test instead.
- **Aiming a barrier is a Core mode, not a UI mode.** `BeginBarrierPlacement` freezes the
  clock (`Tick` returns early) and `TryMove` is refused until the aim is confirmed or
  cancelled, so the pause cannot be bypassed by a different renderer. `TryPlaceBarrier`
  no longer exists; the flow is begin → move cursor → confirm/cancel.
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
- **A barrier buys time: `ObstacleCost * EnemyStepSeconds` = 3 s by default**
  (`5 * 0.6`). The delay is charged before the killer steps into the cell, which is why it
  appears to stall at the barrier. It still never blocks. If a barrier seems to do nothing,
  check `GameState.SecondsUntilNextStep` and `GameStateBarrierDelayTests` first.
- **Barriers do not create detours in a generated maze** — a perfect maze has one route
  between any two cells, so they add delay rather than a redirect. The steering logic is
  real and tested; it needs a looped maze (hand-built in tests, or an optional braided
  generator). See the last entries in `DECISIONS.md`.
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
