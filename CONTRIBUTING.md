# Contributing

Thanks for working on the group project. This file explains how we write code, how to
run the tests, and how to add features without breaking the architecture.

If you are picking the project up cold, read [`HANDOFF.md`](HANDOFF.md) first.

## Before you start

```bash
dotnet build     # must be 0 warnings, 0 errors
dotnet test      # must be green
```

Never start a feature on top of a red build. If it is already red, fix that first and
note it in `PROGRESS.md`.

## Ground rules

1. **Never leave the repository uncompilable.** If you must stop mid-feature, make it
   build, and record exactly where you stopped in `PROGRESS.md` → *Current Task*.
2. **One step at a time.** Implement, build, test, update the tracking files, then
   continue.
3. **Update documentation in the same change** as the code it describes: `README.md`
   for user-visible behaviour, `ARCHITECTURE.md` for structure, `DECISIONS.md` for
   judgement calls, `PROGRESS.md` always.
4. **Do not change a public API or reorganise working code** without an entry in
   `DECISIONS.md` and a note in `PROGRESS.md`.
5. **Core must not reference UI.** Game rules in the UI project are always a bug.

## Coding conventions

- Standard C# style (enforced by `.editorconfig`): `PascalCase` types/methods,
  `camelCase` locals and parameters, `_camelCase` private fields, `I` prefix for
  interfaces, `T` prefix for generic parameters.
- One public type per file, file name = type name, namespace = folder path
  (`src/MazeGame.Core/Maze/MazeGenerator.cs` → `MazeGame.Core.Maze`).
- `///` XML docs on **every public type and member**, explaining what it does and why
  it exists. The compiler treats missing docs as errors in Core and UI.
- Keep methods short (aim under ~30 lines) and do one thing. Prefer early returns over
  deep nesting.
- Comments explain *why*, not *what*. Non-obvious algorithm steps deserve a comment;
  `i++` does not.
- No magic numbers: every tunable goes into `GameConfig` with a documented default.
- No dead code, no commented-out code, no duplicated logic. Refactor when you see it.
- Prefer `readonly` fields and immutable data. Expose the smallest public surface that
  the UI actually needs.
- Validate arguments at public boundaries and throw
  `ArgumentException` / `ArgumentOutOfRangeException`. Never swallow exceptions.
- Randomness goes through `IRandom`; time arrives as a `double deltaSeconds`
  parameter. Never call `Random`, `DateTime.Now` or `Stopwatch` inside Core logic.

## How to run the tests

```bash
dotnet test                                     # everything
dotnet test --filter FullyQualifiedName~DisjointSet   # one class
dotnet test --filter DisplayName~Union          # one test name fragment
```

Test rules:

- Every Core feature ships with tests in the same change.
- Cover the normal case, edge cases (1x1 maze, single element, empty path) and
  invariants (e.g. every generated maze is connected and has exactly `N-1` passages
  across many seeds).
- Tests must be deterministic and fast: fixed seeds, no file or network access, no
  `Thread.Sleep`.
- Name tests so the failure explains itself:
  `Find_AfterUnion_ReturnsSameRoot`, `Generate_WithSameSeed_ProducesIdenticalMaze`.
- `Cell` has no public constructor by design, so build test mazes with `new Grid(...)`
  plus `RemoveWall(...)` and take cells from `grid.GetCell(...)`. A `default(Cell)` is
  not a valid cell.
- For shortest-path tests, prefer an independent oracle (breadth-first search when all
  weights are 1) over hand-computed expected lengths; see `DijkstraPathfinderTests`.
- Prefer the hand-built mazes in `TestDoubles/TestLevels.cs` (a 1x6 corridor and a 2x3
  maze with a loop) when a rule test needs known placement; `FixedRandom`'s no-op shuffle
  makes placement row-major and therefore predictable.

## How to extend the game

The design is additive on purpose: new content is a new class plus one registration
point. The recipes below describe the intended seams; names in *italics* are
introduced by the phase in brackets. Update this file when the real API lands.

### Add a new objective type (beyond keys)

There is exactly one objective today — collect the keys — so there is no `IObjective`
interface yet. That is deliberate: the brief says not to add an abstraction before a
second implementation exists. When you add one (a torch, a fuse, a medkit):

1. Introduce `IObjective` then, not before, with the behaviour both kinds share
   (symbol, collected flag, what happens on step).
2. Keep the data in `GameState`: a set of cells like `Keys`, plus the count in
   `GameConfig` (never a literal).
3. Handle collection in `TryMove` next to the existing `_keys.Remove(...)`, and add a
   test that stepping on it twice counts once and that it cannot spawn on the player,
   killer or exit.
4. Place it in the constructor from the same shuffled cell list, and extend
   `GameConfig.Validate`'s capacity rule so a maze must be big enough for everything.
5. Draw it in both renderers (`RaylibRenderer` shape, `ConsoleRenderer` symbol); the UI must
   not change the rules.

### Add a third placeable (the barrier/wisp pattern)

Barriers and wisps both go through one aiming mode, one cursor and one clock freeze. A new
placeable (a decoy, a smoke bomb, a trap) should reuse all three:

1. Add a value to `PlacementMode` and a `BeginXPlacement()` that calls the shared
   `BeginAim(mode)` — that is where "game over", "already aiming" and "nothing left to
   spend" live, so a new item cannot forget one of them.
2. Add the budget and, if it is temporary, the lifetime to `GameConfig`, and validate them
   like `LureCount` / `LureSeconds`.
3. Handle it in `ConfirmPlacement` (the dispatch), spending the charge only on success and
   refusing with a rule that is worth explaining in a doc comment (the wisp refuses the
   player's own cell because it could never be cheaper).
4. If it changes **where** the killer walks, do it through `EnemyRouteToTarget`'s comparison
   of two `FindPath` costs and invalidate the cached route whenever it appears, expires or is
   consumed. If it changes **what a cell costs**, put it in `CostOfEntering` and keep the
   weight finite — that is the no-soft-lock invariant.
5. Expose it read-only on `GameState` (a cell, a set, a remaining time) and draw it in both
   renderers plus a HUD pill; the UI must not branch on anything else.
6. Add the same three tests every placeable has: the budget is spent exactly once, the effect
   happens (and stops) at the documented time, and a long random-play game still ends with the
   killer able to reach the player.

### Add a new enemy type

The killer is a single timed Dijkstra chase inside `GameState` (`AdvanceEnemy`, plus the
cached route). A second enemy is the point at which the behaviour should be extracted:

1. Create `IEnemyBehavior` with "given the maze, my cell, the player's cell and elapsed
   time, where do I step?". The current killer becomes the first implementation.
2. Reuse `IPathfinder` for movement; never write a second pathfinder. If a different
   enemy needs different edge weights, pass a different cost function.
3. Move the speed numbers into `GameConfig` and give each enemy its own field.
4. `GameState` keeps a list of enemies and advances them in `Tick`, ending the game when
   any of them shares the player's cell.
5. Test with a hand-built maze from `TestLevels`: the enemy moves toward the player, never
   crosses a wall, and always has a route (that invariant is already tested for one
   killer and must hold for every enemy).

### Add a new tile type (weighted terrain)

Cells currently carry only walls; there are no tile kinds. When a second terrain type is
needed:

1. Store the extra information in `GameState` (a set of cells, like `_barriers`), not in
   `Cell` — cells are immutable and generated once.
2. Extend the single cost function `GameState.CostOfEntering`; every weight must stay
   finite and at least 1 so a route always exists.
3. Add a `GameConfig` value for the cost if it is tunable, and validate it like
   `ObstacleCost`.
4. Draw it in both renderers; the UI reads state only.
5. Test the cost through `DijkstraPathfinder` *and* through a game: a cheap-looking detour
   must be chosen when it really is cheaper.

### Add a new weighted hazard (or change the obstacle cost)

Weights are supplied to `IPathfinder` as a function, so a new hazard is a `GameState`
concept plus a number in `GameConfig`:

1. Track the hazard in `GameState` (a set of cells, a tile kind, or whatever it is).
2. Extend the cost function passed to `FindPath`: `cell => state.CostOf(cell)`.
3. Keep every weight finite and at least 1. A hazard can make the enemy detour, never
   trap it — that invariant has tests and comes from the brief.
4. Add a test that the enemy still reaches the player with hazards everywhere.

### Add or replace the maze generator

Generation is behind `IMazeGenerator`, so a different algorithm (recursive backtracker,
Prim, rooms-and-corridors) is a new class, not an edit to the existing one:

1. Create `MazeGame.Core/Maze/MyGenerator.cs` implementing `IMazeGenerator`.
2. Take an `IRandom` in the constructor and default to it for every random choice —
   never call `System.Random` or `Random.Shared` in Core.
3. Build a `Grid` first: its constructor is the single place that validates rows and
   columns, and it gives you `GetIndex(row, column)` for flat structures such as
   `DisjointSet`.
4. Change walls only with `Grid.RemoveWall`, which updates both cells that share the
   wall. Never write into the cell array directly.
5. Keep the result a *perfect* maze: connected, acyclic, `cellCount - 1` passages. Add
   the generator to the invariant tests over 50 seeds (connectivity by BFS, passage
   count, closed outer boundary, seed reproducibility).
6. If you want loops or different topology, do **not** change the generator: wrap it in a
   decorator like `BraidedMazeGenerator` (removes standing interior walls, adding loops) or
   `DeadEndBraider` (opens one wall at a share of the dead ends). Both only remove walls after
   generation, so the perfect-maze guarantee stays intact and a config value turns each off.
7. Swap the one construction line in the composition root, and note the change in
   `DECISIONS.md`.

### Add another renderer or frontend

`MazeGame.UI` has two renderers behind `IRenderer` (`RaylibRenderer`, `ConsoleRenderer`) and
two frontends (`RaylibFrontend`, `ConsoleFrontend`), each owning its own event loop. To add
a third (a replay viewer, another engine, a web canvas):

1. Write a class implementing `IRenderer.Draw(GameState)`; never change the state there.
2. Ask `GameState.IsVisible(cell)` for what may be shown, and keep your own explored-cell
   memory if explored geometry should stay dim. Never invent visibility in a renderer:
   what the player may know is a rule, and it has tests.
3. For input, translate keys into `GameCommand` values and let `GameCommands.Apply` perform
   them — that is the one place where "direction keys move the aim cursor while aiming"
   lives. Adding a control there gives it to every frontend at once.
4. Either add a frontend that owns its loop and calls `GameState.Tick(measuredDeltaSeconds)`
   plus `renderer.Draw(state)`, or wire the renderer into an existing frontend. Never derive
   elapsed time from an assumed frame length. Start each run with the injected
   `Func<GameState>` (in production, `GameFactory.Create`) so a replay keeps working.
5. Bracket drawing the way the engine demands it (Raylib needs `BeginDrawing`/`EndDrawing`,
   which the frontend supplies; the renderer only fills in a frame).
6. Keep the console frontend working: it is the fallback when no window can be opened, and
   `Program` relies on that.
7. Update `ARCHITECTURE.md` (dependency table, package version) and `DECISIONS.md` (why the
   framework changed).

### Add a screen (title, pause, settings)

The window already has two screens — title and game — with the state owned by
`RaylibFrontend`. A new screen follows the same shape:

1. Draw it from the renderer as its own method that takes only what it needs to draw (like
   `DrawTitle(int)`); a screen that needs no rules must not take a `GameState`.
2. Keep the selection and the transitions in the frontend, not in the renderer and never in
   Core: a menu is not a game rule, and tests should not have to know it exists.
3. If it starts a run, call the injected game factory and `RaylibRenderer.Reset()`; if it
   returns to a running game, leave the state untouched.
4. Give the console a minimal version if it fits (a printed heading and a key prompt), and
   skip key prompts when `Console.IsInputRedirected` — a piped run can never answer one.
5. Verify by driving the renderer directly for a few frames in both states, as `PROGRESS.md`
   describes; this environment cannot show pixels to the agent.

### Add or change a tunable

1. Put the number in `GameConfig` with an XML-documented default and a comment on the
   effect of changing it.
2. Update `ARCHITECTURE.md` if the config section grows a new concept.
3. Add a test if the value changes behaviour (for example obstacle cost must stay
   finite).

## Tracking files cheat sheet

| File | Update when |
| --- | --- |
| `PROGRESS.md` | Every change: Current Status, Current Task, Next Steps, Completed, Known Issues |
| `DECISIONS.md` | You make a judgement call others could disagree with (append only) |
| `ARCHITECTURE.md` | A class, folder, dependency or package is added/removed |
| `HANDOFF.md` | Conventions or gotchas change |
| `README.md` | User-visible behaviour, controls, build steps change |
| `CONTRIBUTING.md` | The way we work or the extension steps change |
