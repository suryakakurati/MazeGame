# DECISIONS

Append-only log of design decisions and the reasoning behind them. Add a new entry
at the bottom; never rewrite an old one. If a decision is reversed, add a new entry
that supersedes it and says why.

Format: date, decision, reason, consequences.

---

## 2026-10-20 — Three projects, folders `src/` and `tests/`

**Decision:** `MazeGame.Core`, `MazeGame.UI` and `MazeGame.Tests` live in
`src/MazeGame.Core`, `src/MazeGame.UI` and `tests/MazeGame.Tests`.

**Reason:** Keeps shipping code and test code visibly separate, and matches the
standard .NET layout so any contributor can navigate it. The solution file stays at
the root, so `dotnet build` / `dotnet test` need no arguments.

**Consequences:** Shell commands that target a project must include the `src/` or
`tests/` prefix (for example `dotnet run --project src/MazeGame.UI`).

## 2026-10-20 — Shared build settings in `Directory.Build.props`

**Decision:** `TargetFramework` (net8.0), `Nullable`, `ImplicitUsings`,
`TreatWarningsAsErrors` and the analyzer level are set once in
`Directory.Build.props` instead of in each `.csproj`.

**Reason:** The Definition of Done requires a warning-free build everywhere; a single
file makes that impossible to forget in a new project and removes duplicated settings.

**Consequences:** A new project added to the solution automatically inherits the same
rules and must be warning-free from day one.

## 2026-10-20 — Warnings are errors, nullable reference types are on

**Decision:** `TreatWarningsAsErrors=true` and `Nullable=enable` solution-wide.

**Reason:** The brief requires zero warnings; treating warnings as errors is the only
mechanical way to prevent regressions, and nullable annotations catch a whole class of
bugs at compile time.

**Consequences:** Contributors must fix warnings rather than ignore them. If a warning
must be suppressed, suppress it narrowly with a justification comment, not globally.

## 2026-10-20 — `AnalysisLevel=latest-recommended`, CA1707 disabled

**Decision:** Use Microsoft's recommended analyzer set. Disable CA1707 ("identifiers
should not contain underscores") in `Directory.Build.props`, and keep code-style IDE
rules as editor suggestions (`EnforceCodeStyleInBuild` is off).

**Reason:** Analyzers catch real defects (unused fields, wrong equality, bad string
formatting). CA1707 conflicts with the required test naming style
(`Find_AfterUnion_ReturnsSameRoot`). Enforcing style rules at build time would make
the build fail on formatting nits and slow the team down; style is still enforced in
the editor and in review.

**Consequences:** Naming conventions are machine-checked in the IDE, not at build
time. Analyzer warnings break the build and must be fixed.

## 2026-10-20 — XML documentation is compiler-enforced in Core and UI

**Decision:** `GenerateDocumentationFile=true` in `MazeGame.Core` and `MazeGame.UI`,
which turns "missing XML comment on public member" (CS1591) into a build error. The
test project is exempt.

**Reason:** The brief requires XML docs on every public class and method; making the
compiler enforce it is cheaper than reviewing for it. Tests are exempt because test
method names already state their intent.

**Consequences:** Any new public type or member needs a `///` summary or the build
fails. Internal types are not affected.

## 2026-10-20 — No DI container; composition happens in the UI entry point

**Decision:** Core depends on interfaces (`IRandom`, `IMazeGenerator`, `IPathfinder`,
...) and the UI entry point constructs the concrete implementations. No
Microsoft.Extensions.DependencyInjection or similar package.

**Reason:** Dependency inversion is achieved with plain constructor injection at a
single composition root. A container adds a dependency and indirection that a
five-class game does not need (the brief warns against premature abstraction).

**Consequences:** Adding a new implementation means editing the composition root only,
which is exactly the swap point the architecture is designed around.

## 2026-10-20 — Raylib-cs is deferred to Phase 6; the UI project is dependency-free

**Decision:** The UI project currently has no NuGet packages. Raylib-cs will be added
when the renderer is written.

**Reason:** Raylib-cs ships native binaries; adding it before there is anything to
render would make the build depend on native assets for no benefit, and could break
`dotnet build` for teammates on unsupported machines. The brief also says to add a
dependency only when it is actually needed.

**Consequences:** `ARCHITECTURE.md` must list Raylib-cs with its exact version when it
is added, and `README.md` must say how to run the graphical build. Any console
renderer added in Phase 5 stays usable through the `IRenderer` seam.

## 2026-10-20 — No `global.json` SDK pin

**Decision:** Do not commit a `global.json`.

**Reason:** It would force every teammate onto one SDK feature band. Any .NET 8 SDK
builds this solution (Phase 1 was verified with 8.0.416 on macOS arm64).

**Consequences:** The README states ".NET 8 SDK required" instead of an exact patch
version. Revisit if SDK differences ever break the build for someone.

---

## 2026-10-20 — Skill mechanic: limited placeable barriers

**Decision:** The skill-expression mechanic (Phase 7) is player-placed barriers with a
finite budget, chosen from three proposals (stamina sprint, placeable barriers, noise
distraction). Barriers are ordinary weighted obstacles: cost 25 by default, never
impassable.

**Reason:** It reuses machinery we must build anyway (weighted Dijkstra edges,
obstacle placement from Phase 5) instead of adding a second enemy AI mode, so the
feature is small, testable, and easy to explain. It also gives the player a real
tactical decision: spend a rare barrier to force the killer down a longer corridor.

**Consequences:** `GameConfig` needs a barrier budget (default 3) and the obstacle
cost; `GameState` needs to reject placement when the budget is exhausted or the tile
is already occupied. Obstacles must stay finite-cost, so a player can never brick the
killer's path — that invariant gets a test.

## 2026-10-20 — Objectives: collect all keys to unlock the exit

**Decision:** The objective model (Phase 5) is "collect N keys, then the exit opens"
(N defaults to 3 and lives in `GameConfig`). The exit is visible from the start but
cannot be used while keys remain.

**Reason:** It is the simplest objective model that still creates a route-planning
decision, and it maps cleanly onto the perfect maze: keys are placed on distinct cells
after generation, so every key is reachable by construction.

**Consequences:** `GameState` exposes key positions, collected state and a
`GameStatus` that distinguishes "exit locked" from "won". Keys must be placed without
landing on the player start, the exit, or each other, and that placement needs a
deterministic test.

## 2026-10-20 — No version control initialised

**Decision:** The team chose not to run `git init` at this point; the project folder
itself must remain self-contained.

**Reason:** The team manages the course submission and any remote repository outside
this workspace.

**Consequences:** There is no commit history to rely on. `PROGRESS.md` is therefore
the only account of what changed and why; keep it accurate. If version control is
added later, the existing `.gitignore` already excludes `bin/`, `obj/` and local SDK
installs.

---

## 2026-10-20 — `IRandom.NextInt`, not `IRandom.Next`

**Decision:** The random abstraction exposes `int NextInt(int maxExclusive)` and
`void Shuffle<T>(IList<T> items)`.

**Reason:** CA1716 (a real analyzer error here, not a style opinion) rejects `Next` on
an interface because it is a reserved keyword in other .NET languages, which would make
the interface awkward to implement outside C#. Renaming is a one-word change; a
suppression would have to be carried forever.

**Consequences:** Core code reads `_random.NextInt(n)`. Keep the name when adding new
random sources so they stay interchangeable.

## 2026-10-20 — `SeededRandom.NextInt` rejects a non-positive bound

**Decision:** `NextInt(0)` and `NextInt(-1)` throw `ArgumentOutOfRangeException` even
though `System.Random.Next(0)` quietly returns 0.

**Reason:** A zero bound means "pick one of nothing", which is always a caller bug. A
unit test caught the mismatch between the documented contract and the wrapped
behaviour; validating once at the boundary is cheaper than debugging a mystery zero in
maze generation later.

**Consequences:** Do not replace the explicit check with a bare delegation to
`Random.Next`. The contract is part of the interface: any new `IRandom` implementation
must reject non-positive bounds too.

## 2026-10-20 — Randomised Kruskal generates the maze

**Decision:** `MazeGenerator` uses randomised Kruskal: collect every interior wall,
shuffle the list, and remove a wall only when `DisjointSet.Union` reports the two cells
were in different sets.

**Reason:** It is the algorithm the brief describes ("remove the wall only if the two
cells are in different sets, then union them"), so the graded Union-Find is genuinely
load-bearing rather than bolted on. It also needs no recursion or explicit stack, so it
cannot blow the stack on a large grid, and every seed is reproducible.

**Consequences:** Memory is `O(cellCount)` for the candidate-wall list (roughly `2N`
entries) in addition to the grid, which is insignificant for a game-sized maze. A
recursive-backtracker or Prim generator can be added later as another `IMazeGenerator`
without touching the game loop.

## 2026-10-20 — Walls belong to the `Grid`, cells are immutable values

**Decision:** A `Cell` is an immutable `readonly record struct` holding its position and
a single byte of wall bits. It has no public constructor and no mutating member; walls
are changed only through `Grid.RemoveWall`, which updates the cell and its neighbour
together. Cell positions are created and validated by `Grid`.

**Reason:** A wall is shared by two cells, so the only way to guarantee the classic
"passage open from one side but not the other" bug cannot happen is to own both updates
in one method. Immutability means a `Cell` read by the renderer can never change under
it, and the byte-per-cell packing keeps a large grid cheap.

**Consequences:** Removing a wall allocates a new `Cell` value and swaps it into the
array (a struct copy, not a heap allocation). Code that wants to change a wall must go
through `Grid`; `Cell.HasWall` is read-only, which is what the UI needs for drawing.

## 2026-10-20 — `Direction` values are powers of two

**Decision:** `Direction` is a plain enum with `North = 1, East = 2, South = 4,
West = 8`, plus `DirectionExtensions.Opposite` and `Delta`.

**Reason:** The values double as the wall bit mask in `Cell`, so no second enum or
conversion table is needed. Helpers live in an extension class so the enum stays a
plain value type and the geometry is testable on its own.

**Consequences:** The four values are not a contiguous 0-3 range, so do **not** index
arrays with `(int)direction`. Use `Enum.GetValues<Direction>()` or explicit lookups,
and note that the values can be OR-ed into a mask internally. Anything outside the four
defined values is rejected by `Grid` with `ArgumentOutOfRangeException`.

## 2026-10-20 — Maze size validation lives only in `Grid`

**Decision:** `MazeOptions` is a dumb `readonly record struct` (rows, columns, nullable
seed) with no validation. `MazeGenerator.Generate` relies on the `Grid` constructor to
reject sizes below 1.

**Reason:** One authority for a rule beats two copies that can drift. The constructor
is reached by every path that can create a maze, so nothing can slip past it.

**Consequences:** A new generator added later gets the same validation for free by
building a `Grid`. If a non-grid maze representation ever appears, the validation must
move up into `MazeOptions` at that point.

---

## 2026-10-20 — `IPathfinder` takes a cost delegate, not an obstacle list

**Decision:** `IReadOnlyList<Cell> FindPath(Grid grid, Cell start, Cell goal, Func<Cell,
int> cost)`. The delegate returns the weight of *entering* a cell; the start cell's own
weight is not paid. The result includes both start and goal, and is an empty list when
no route exists.

**Reason:** Dijkstra needs weights, nothing else. Passing in a function keeps the
pathfinder from knowing whether a cell is expensive because of a barrier, a hazard or
slow terrain, so obstacles stay a `GameState` concept and Phase 5 needs no change here.
An `IWeightedGrid` interface would be a second representation of the same grid for no
benefit (the brief warns against premature abstraction), and a concrete obstacle class
does not exist yet.

**Consequences:** Callers must supply a positive finite weight. `GameState` will pass
`cell => IsBarrier(cell) ? config.ObstacleCost : 1`. Because walls alone decide
adjacency and every cost is finite, a path inside a connected maze always exists — the
enemy can never be blocked permanently.

## 2026-10-20 — Generic `MinHeap<T>` with an injected comparer

**Decision:** One generic binary min-heap (`MinHeap<T>`, optional `IComparer<T>`) instead
of an `int`-priority queue or a heap built into the pathfinder.

**Reason:** The heap is a graded data structure that will be presented, so it is worth
having as a stand-alone, testable class with its own tests. Generic + comparer keeps the
pathfinder's entry type (distance, cell index) a private detail, and the same class
becomes a max-heap with a different comparer, which a test exercises.

**Consequences:** Dijkstra wraps each pending route in a private `FrontierEntry` that
implements `IComparable`, ordered by distance and then by cell index so equal distances
give a deterministic path.

## 2026-10-20 — Dijkstra uses stale-entry deletion, not decrease-key

**Decision:** When a cheaper route is found, push a new entry; when extracting, skip any
entry whose stored distance is worse than the best known distance for that cell.

**Reason:** A true decrease-key needs a position index per item (an extra array plus
bookkeeping in the heap) for no asymptotic gain here. Lazy deletion keeps the code short
enough to present line by line, and the cost is only extra heap pushes — at most one per
relaxed edge.

**Consequences:** The heap can hold more entries than there are cells, which is expected
and not a bug. `FindPath` also stops as soon as the goal is extracted, because that is
the moment its distance is final.

## 2026-10-20 — Movement cost is the entered cell; walls are the only impassability

**Decision:** Relaxing an edge adds `cost(neighbour)`; the start cell is free. Adjacency
is decided solely by `Grid.HasWall`, and the pathfinder rejects weights below 1 with
`ArgumentException`.

**Reason:** "You pay for the room you step into" is the model the enemy needs, and one
consistent rule avoids off-by-one weighting between the enemy and the tests. Rejecting
zero/negative weights protects Dijkstra's correctness (a negative edge would make a cell
*cheaper* to reach by looping), and rejecting them loudly beats a wrong path that is hard
to debug.

**Consequences:** Tests assert path lengths in cells (`N` cells = `N - 1` paid steps).
Dijkstra also sums distances in `long` so an absurd weight cannot overflow into a
negative distance.

## 2026-10-20 — Dijkstra cost semantics verified against BFS

**Decision:** With uniform weights, `FindPath` must return exactly the route length a
breadth-first search finds, and a test asserts that on 10 generated mazes.

**Reason:** "A path exists and is valid" does not prove shortest-path correctness; an
independent, obviously-correct algorithm is a much stronger oracle than hand-written
expected lengths, and it costs a few lines in the test file.

**Consequences:** If a future optimiser (A*, early exit, heuristic) changes the
pathfinder, this test still holds for uniform weights and will catch regressions.

---

## 2026-10-20 — `GameState` is built over a `Grid`, not a maze generator

**Decision:** `GameState(GameConfig, Grid, IPathfinder, IRandom)` — the maze is generated
by the composition root and handed in.

**Reason:** The rules are about a maze that already exists, not about making one. Taking a
`Grid` lets tests use tiny hand-built mazes with known placement (a one-row corridor, a
maze with a loop) instead of hoping a generated maze happens to contain the shape a rule
test needs. The UI still gets a one-seed-reproducible game because it passes one
`SeededRandom` to both the generator and the game.

**Consequences:** `GameState` validates that the maze size matches the config, so the two
cannot disagree. A future "restart" is just another `GameState` over a fresh grid.

## 2026-10-20 — A locked exit can be walked over, it just does nothing

**Decision:** The player may step onto the exit before collecting every key; winning
requires standing on it with all keys collected.

**Reason:** Blocking the tile would be the intuitive "locked door", but the exit is a
single cell in a one-cell-wide corridor: refusing entry could wall the player off from keys
and make the game permanently unwinnable. Walking over it and getting nothing is the only
version that cannot soft-lock. (The end-of-game audit is a test.)

**Consequences:** The renderer/HUD must communicate "LOCKED" clearly, otherwise the exit
looks like an ordinary tile. `IsExitUnlocked` is exposed for exactly that.

## 2026-10-20 — The killer starts in the furthest dead end (bug fix)

**Decision:** The killer's start cell is the dead end (exactly one opening) with the
greatest maze distance from the player, falling back to the diagonally opposite corner if
a maze has no dead ends.

**Reason:** This started as "place the killer in the opposite corner", which broke the
game in a way the tests caught: a perfect maze is a tree, the killer's cell is then a cut
vertex, and because the player cannot walk through the killer, every key and the exit
behind it became unreachable. A dead end is a leaf, so removing it cannot disconnect the
rest of the maze, which guarantees every objective stays reachable at the start. Measuring
distance through the maze (rather than Manhattan) keeps the opening fair.

**Consequences:** `Constructor_LeavesEveryObjectiveReachableWithoutCrossingTheKiller`
asserts the property for several seeds, and `AWholeGame_CanBeWonOnAGeneratedMaze` is the
end-to-end regression test (it failed before the fix). A hand-built looped maze with no
dead ends exercises the fallback.

## 2026-10-20 — `Tick` moves the killer at most one step per call

**Decision:** `Tick(deltaSeconds)` banks at most one step interval of time and performs at
most one killer step.

**Reason:** Real frames are irregular; a dragged window or a suspended laptop can produce
a multi-second delta. Without the cap the killer would teleport across the maze the moment
the window regains focus, which reads as a bug to the player. Capping also removes any
catch-up loop, so one `Tick` can never stall the frame.

**Consequences:** Tests drive time with explicit `Tick` calls (e.g. `Tick(1.0)` three
times), not one huge delta. The elapsed-time accumulator is clamped, so a long pause costs
the player at most one step.

## 2026-10-20 — A barrier is placed on the player's cell, once per cell

**Decision:** `TryPlaceBarrier` succeeds only when the game is running, budget remains, and
the player's current cell has no barrier yet; a refused placement spends nothing.

**Reason:** Placing where you stand makes the tactical decision immediate and keeps the
API to one argument. One barrier per cell means "stacking" cannot be used to make a cell
effectively impassable (cost would still be finite, but the intent is a speed bump).
Refusing without charging keeps the budget honest and is easy to test.

**Consequences:** A barrier can end up on the exit (walk over it while locked, then press
B); that only makes the killer's final step expensive, which is harmless. The player
never pays the cost of their own barrier.

## 2026-10-20 — The console renderer is a debug view; fog belongs to Phase 6

**Decision:** `ConsoleRenderer` draws the entire maze with no fog of war, and visibility is
not modelled in Core yet.

**Reason:** Phase 5 is about rules; a full-maze view makes rule bugs obvious in one glance.
Fog needs its own Core query (`GameState.IsVisible`) and its own tests (radius 0, cells
outside the radius), so it is scheduled as Phase 6 work instead of being half-built here.

**Consequences:** `IRenderer.Draw(GameState)` is the only seam the renderer uses, so adding
fog changes the renderer plus one Core query, not the rules. `VisionRadius` already exists
in `GameConfig` for that phase.

## 2026-10-20 — Known limitation: a perfect maze has no detours

**Decision:** Keep the perfect-maze generator as the only generator for now; accept that a
barrier slows the killer rather than redirecting it.

**Reason:** A perfect maze is a tree, so there is exactly one route between any two cells;
no weighting can make Dijkstra choose another one. The barrier still has tactical value
(placing it on the corridor the killer must use buys time), but the "force it down a longer
corridor" fantasy needs loops. Braiding the maze would conflict with the graded perfect-maze
requirement, so it can only ever be an optional extra generator behind `IMazeGenerator`.

**Consequences:** The steering behaviour is implemented and tested against a hand-built
maze *with* a loop (`Barrier_SteersTheKillerOntoTheCheaperDetour`), so a braided generator
would light it up with no rule changes. The team must decide in Phase 7 whether to add one
(Open Question 3 in `PROGRESS.md`).

## 2026-10-20 — A barrier buys *time*, not just weight (play-test fix)

**Decision:** The killer waits `EnemyStepSeconds * ObstacleCost` before entering a
barriered cell (15 s with the defaults), and the delay is charged *before* the move.

**Reason:** The first hands-on session exposed that a barrier did nothing visible: cost
only influenced route choice, and a perfect maze offers one route, so the killer crossed at
its usual pace. A rule the player cannot feel is a broken feature. Charging the wait before
the step also makes the behaviour read correctly: the killer stalls at the barrier and then
forces through, instead of seeming to pass it instantly and then idle on the far side.

**Consequences:** Barrier strength now lives in the same number as its pathfinding weight,
so there is one knob to tune for balance (25 units = 15 s may prove too strong in a
group play-test). `GameState.SecondsUntilNextStep` reads the cost of the next route cell;
the whole rule is covered by `GameStateBarrierDelayTests`, and removing the delay fails five
of those tests.

## 2026-10-20 — Aiming a barrier pauses the game (play-test request)

**Decision:** `BeginBarrierPlacement` freezes the clock and puts a cursor on the player;
`MoveBarrierCursor` moves it with the same directions the player walks with (ignoring
walls); `ConfirmBarrierPlacement` drops the barrier and spends one; `CancelBarrierPlacement`
exits without spending. `TryPlaceBarrier` (place-at-your-feet) was removed.

**Reason:** The requested design: press B, take a moment to choose *where* the trap goes
anywhere on the map, then commit. The pause is a rule, not a UI state, because a UI-only
pause would still let the killer advance while the player is aiming — so `Tick` returns
early and `TryMove` is refused while aiming. Keeping the mode in Core is also what makes it
testable without a keyboard.

**Consequences:** This is a public API change (recorded here as required): the UI now drives
`BeginBarrierPlacement`/`MoveBarrierCursor`/`ConfirmBarrierPlacement`/`CancelBarrierPlacement`,
and `IsPlacingBarrier`/`BarrierCursor` are exposed for rendering. Aiming costs no game time,
which is generous to the player; the cursor ignores walls so any cell can be targeted, and a
placement on an already-barriered cell is refused without spending.

## 2026-10-20 — Barrier delay retuned to 3 seconds (supersedes the 15 s figure)

**Decision:** `ObstacleCost` defaults to 5 instead of 25, which makes one barrier a
3-second pause at the default 0.6 s step time (9 s for the three-barrier budget). The
`EnemyStepSeconds * ObstacleCost` rule and the "charged before the step" ordering are
unchanged.

**Reason:** The team played the build and 15 s per barrier was too long: it removed the
killer from the game for half a minute and made placing one feel like a free win rather
than a tactical delay. The delay was always meant to be one number, so the fix is one
number.

**Consequences:** Cost still serves double duty as Dijkstra weight and delay, so a barrier
still makes a detour attractive when a detour exists (5 units vs a 4-cell detour). The
delay tests now use the shipped default, and
`GameConfigTests.DefaultBarrierCost_BuysThreeSeconds` pins "default cost x default step =
3 s" so a future tuning change has to be deliberate. If 3 s proves too weak, change
`ObstacleCost` (8 gives 4.8 s) rather than adding a second knob.

## 2026-10-20 — Fog of war is a Core rule, computed by walking, not drawing

**Decision:** `GameState.IsVisible(Cell)` reports whether the player may see a cell: their
own cell plus every cell within `VisionRadius` steps through open passages. The set is
built with breadth-first search out from the player and cached; it is recomputed when the
player moves. Renderers call it and decide only how to darken the result.

**Reason:** Visibility decides what the player is *allowed to know*, which is a game rule,
not a drawing style — putting it in a renderer would let a second renderer leak information
and would make it untestable. Breadth-first search is the natural fit for "how far can I see
down the corridors": it visits cells in walking order, so the radius is an exact cut-off and
walls block sight for free (the search only crosses passages).

**Consequences:** The radius is measured in steps, not straight-line distance, so the light
follows the maze instead of shining through rock; that is why it needs no line-of-sight or
shadow-casting code. `VisionRadius` 0 means "only my own cell", which is the cheapest test
for the boundary. Cost is one O(cells) walk per player step, negligible at 15x15.

## 2026-10-20 — Raylib-cs 8.1.0 for the window, console kept as the fallback

**Decision:** `MazeGame.UI` references `Raylib-cs` 8.1.0 and draws the game in a window
through `RaylibRenderer` (behind the existing `IRenderer` seam). The console renderer is
kept and can be selected with `--console`; it is also used automatically when
`Raylib.IsWindowReady()` is false.

**Reason:** Raylib is a small binding with a native library shipped inside the NuGet package
(osx-arm64, osx-x64, linux-x64, win-x64/x86) and needs no asset pipeline: shapes, text and
colour are enough for this game. Keeping the ASCII view means the game is still playable over
SSH, in CI and on a machine where the native library will not load, and it keeps a debugging
view that shows the whole ruleset at once.

**Consequences:** The UI project now has its first native dependency, so a restore downloads
per-platform binaries and the game needs a display to show the window. The window loop owns
`BeginDrawing`/`EndDrawing`; the renderer only fills in a frame. Key handling lives in the
frontends and is translated into `GameCommand` values, so the two frontends cannot drift
apart in what a key means.

## 2026-10-20 — Renderers remember explored cells; Core only reports current visibility

**Decision:** Each renderer keeps its own set of cells the player has seen, so explored
geometry stays dim after the torch moves on. The game itself remembers nothing about
visibility.

**Reason:** Fog that erases everything behind you makes the map impossible to reason about
and hides the walls you already learned. Remembering is purely presentational: it cannot
change a rule, and Core must not accumulate per-frame UI state.

**Consequences:** The remembered set belongs to the renderer instance, so a second renderer
over the same game starts blind (documented in `PROGRESS.md` as a known limitation). Entities
are only drawn where `IsVisible` is true *now*; remembered cells show walls, floor and the
player's own barriers, never the killer, keys or exit.

## 2026-10-20 — Braiding is a decorator, not a change to the graded generator

**Decision:** `BraidedMazeGenerator` implements `IMazeGenerator` by running another generator
and then removing `GameConfig.ExtraLoops` (default 8) random interior walls. `MazeGenerator`
itself is untouched, and `ExtraLoops = 0` reproduces the perfect maze exactly.

**Reason:** The brief grades the Kruskal/Union-Find generator and its perfect-maze property
(connected, acyclic, `cellCount - 1` passages), so that code must keep behaving as documented.
Braiding is a separate, optional step whose only job is to add loops; expressing it as a
decorator keeps both contracts honest and makes the feature removable by one config value.
Removing `k` walls from a perfect maze always adds exactly `k` independent cycles, so the maze
stays fully connected and the loop count is provable, not approximate.

**Consequences:** A barrier can now make the killer take the long way round wherever a loop
gives it an alternative, which is the missing half of the skill mechanic; elsewhere it still
costs time. The extra passages are chosen from walls that are still standing and only on
interior edges (east/south sides are collected so each wall is a candidate once), which keeps
the outer boundary closed. Asking for more loops than there are interior walls removes every
remaining interior wall instead of throwing, so an over-tuned debug config is still playable.

## 2026-10-20 — The killer's aura is a visibility rule with a distance gate

**Decision:** `GameState.IsVisible` returns the player's torch range *plus* the cells within
`EnemyAuraRadius` (2) of the killer, but only when the killer is within `EnemyAuraDistance` (6)
walking steps of the player. `EnemyGlow` exposes the aura cells separately so a renderer can
tint them differently.

**Reason:** The request was to see the killer's surroundings even when the route between you is
dark, and only once it is close. Both halves are about what the player may know, so they belong
in the same rule as the torch rather than in a renderer: the distance gate is a tactical rule,
and putting it in the UI would let a second renderer cheat. Reusing the breadth-first walk that
already computes sight gives the killer's exact walking distance for free, and a second short
walk from the killer gives its aura.

**Consequences:** Visibility now changes when the killer moves as well as when the player moves,
so `AdvanceEnemy` recomputes it (one O(cells) walk per killer step — negligible at 15x15). The
aura ignores the wall between the two of you by design: it is the killer's own light, which is
exactly why it can be seen around a corner. `EnemyGlow` is presentation data only; nothing in
the rules branches on it except the renderer's violet halo and the dread lighting.

## 2026-10-20 — The atmosphere is code and palette, not art assets

**Decision:** The look is built from `references/image.png`'s palette (deep navy, blue-grey
stone, indigo shadow, violet) plus four procedural layers: a night gradient behind the maze, a
moonlit highlight on wall tops, a warm lantern with a radial glow, and drifting dust inside the
light. The killer adds a pulsing violet halo, and the lantern's flicker quickens and dims while
the aura is on screen. No image files are loaded by the game.

**Reason:** The reference is a cold blue-violet night scene with one light source. Reproducing
that mood with shapes keeps the project dependency-free beyond Raylib, keeps the build
reproducible on any machine, and lets every value be tuned in one file. Loading the reference
itself would have meant an asset pipeline, a licence question, and a window that looks like a
screenshot rather than a game.

**Consequences:** The palette block at the top of `RaylibRenderer` is the single place to change
the mood; the effects are deterministic functions of time (sines and a hashed index), so they
never allocate state or produce different results per frame. All of it is visual only: no rule,
no test and no Core type depends on it, which is also why the fog and aura rules stayed testable
while the presentation changed.

## 2026-10-20 — Bioluminescent cave art direction (supersedes the moonlit palette)

**Decision:** The whole window is drawn as an underground cave lit by living light: rounded
stone floor slabs, walls built from thick rounded rock strokes with a green rim, moss and
glowing lichen, mushrooms in some cells, spore motes, faint background blooms, film grain and a
vignette. The player is a hooded lantern-bearer, the killer a spindly rim-lit silhouette, keys
are glowing fungus, the exit is a stone arch and barriers are thorn barricades. The palette is
green, pink and cyan on near-black teal (replacing the blue-violet night palette).

**Reason:** The previous pass still read as "basic circles on a gradient". Making it feel like a
finished indie game means every element needs a silhouette and a light source: walls should look
like rock rather than boxes, collectibles like things growing out of the cave, and characters
like characters. Drawing it from primitives keeps the project free of an asset pipeline and
lets the whole look be tuned in one file, while the roundness and per-cell hashes make it feel
hand-placed rather than generated.

**Consequences:** Cells grew to 40 px (window 656x760) so the figures have room. Walls are drawn
in two passes — all rock first, then all lit rims/moss/lichen — because a shared wall is drawn
by both of its cells and the neighbour's dark stroke would otherwise cover the first cell's rim.
Every detail is derived from a hash of the cell (or the index and clock for motes and grain), so
there is no random state and the cave is identical between frames. Feeding the drawing calls
costs **0.7–1.0 ms per frame** on this machine (400 frames at 656x760, measured: 0.71 ms in
the final run, 1.03 ms before the moss pass), so the CPU-side budget is not a concern; the GPU
cost could not be measured here because the environment has no usable frame buffer.

## 2026-10-20 — Dead ends are braided by a second decorator, and the share is measured

**Decision:** `DeadEndBraider` wraps the generator and opens one extra wall at
`GameConfig.DeadEndBraidPercent` (55%) of the maze's dead ends. `BraidedMazeGenerator` still
runs afterwards for `ExtraLoops` (8) random loops. Both are decorators, so `MazeGenerator`
keeps its perfect-maze guarantee and either feature can be switched off from the config.

**Reason:** Play-testing found that being cornered was the default outcome, not a mistake.
Measured over 8 seeds of the 15x15 game, a perfect maze has 64.5 dead ends and the old
perfect + 8 loops configuration still had 57 — a quarter of the map is a trap. Scattering
random walls cannot fix that, because random walls do not systematically remove traps. Opening
one wall per dead end does: removing a wall can never create a new dead end, so the count can
only fall, and the dead ends that disappear are exactly the corners a chase ends in.

**Consequences:** The shipped maze now has 18.8 dead ends instead of 57, and the share of the
killer's route cells where a barrier can force a detour rose from 40% to 81%. Two neighbouring
dead ends can share the wall between them, so one opening braids two of them: the loop counts
*braided cells* rather than openings, skips a dead end a neighbour already fixed, and stops as
soon as the requested share is reached, so the maze never gains more holes than asked for. Ends
of a one-row maze are dead ends whose three walls are all on the boundary; those are skipped
rather than throwing, so a tiny debug maze degrades to "fewer loops".

## 2026-10-20 — A barrier is a seal first and a speed bump second (supersedes "a barrier only raises the cost")

**Decision:** Placing a barrier starts a `BarrierSealSeconds` (10) seal, recorded in
`BarrierSeals`. A sealed cell is priced at `SealedBarrierCost` (40) for the killer, so Dijkstra
routes around it while any alternative exists. When a seal is the only way to the player, the
killer spends `BarrierBreakSeconds` (3) tearing it apart; the barrier is then destroyed. Once
the seal lapses on its own, the husk stays on the map and costs `ObstacleCost` (5), exactly as
before.

**Reason:** The complaint was that barriers "do not flow into the gameplay". The old rule made
every barrier the same thing — a fixed pause in a corridor, a slightly worse weight in a loop —
so placement never changed the *shape* of the chase. A seal does: on 81% of the route cells it
makes the killer commit to another route, and the player can see that decision happen. A truly
impassable cell was rejected because it would let the player wall the killer in; keeping both
prices finite preserves the "the killer always has a route" invariant, and the tear-down is what
turns the rare true cut-off into three seconds of drama instead of a dead game.

**Consequences:** The seal clock runs on wall-clock time from placement (10 s is longer than the
killer's usual walk, so a seal put on its route is normally still holding when it arrives) and
freezes while the player is aiming, like every other clock. The tear-down is charged in *seconds*
rather than in steps, so it does not scale with `EnemyStepSeconds`. A sealed barrier is skipped
by the renderer's "spent" look: sealed is a breathing green cage, spent is the dim thorn husk, so
the two states read differently without text. The measured effect is that a detoured seal costs
the player the extra walk (~2 cells) while a broken one costs 3 s — a corridor seal is therefore
the stronger play, which is intended but worth a play-test.

## 2026-10-20 — The wisp is a Dijkstra target, not a scripted distraction

**Decision:** The player may carry `LureCount` (2) wisps, each lasting `LureSeconds` (6). Only
one glows at a time. The killer's target is chosen by comparing two `FindPath` results — to the
player and to the wisp — by `RouteCost`, with ties going to the player. The wisp is consumed when
the killer steps onto it, and it expires otherwise. A wisp may not be placed on the player's own
cell.

**Reason:** The request was a new power-up that "still uses Dijkstra". Reusing the search as the
targeting rule means there is no new AI state to get wrong: the killer simply takes the cheapest
route to whichever thing is cheaper, which is also why the wisp behaves sensibly in every
geometry (a wisp thrown between you and the killer is ignored because the player is closer, and
one thrown down a side branch pulls it away). A scripted "distracted for N seconds" flag would
have needed its own edge cases and could not be explained by the algorithm the course grades.

**Consequences:** The wisp is pure redirection: it never blocks, never hides the player and
cannot affect reachability, so the soft-lock invariants are untouched. It lights its own cell
and its neighbours through the same visibility rule that already handles the torch and the
killer's aura, which is why the player can watch it work from across the map. Two Dijkstra runs
happen only when the route is invalidated (player move, placement, consumption, expiry), not per
frame. Refusing a placement on the player's own cell avoids silently spending a charge on a
decoy that can never be cheaper.

## 2026-10-20 — One aiming mode for both placeables

**Decision:** `PlacementMode` (`None`, `Barrier`, `Lure`) plus a single `AimCursor`,
`MoveAimCursor`, `ConfirmPlacement` and `CancelPlacement` replace the barrier-specific
`BarrierCursor`/`MoveBarrierCursor`/`ConfirmBarrierPlacement`/`CancelBarrierPlacement`. Only the
`Begin*Placement` entry points are specific, because what can be afforded differs.

**Reason:** Aiming is a rule (the clock freezes, movement is blocked, the cursor ignores walls)
and that rule is identical for both items. Two parallel APIs would have duplicated the rule and
given the frontends two ways to get the freeze wrong; a shared mode also means the footer,
reticle and console hints switch on one value instead of two booleans that could both be true.

**Consequences:** `IsAiming` is the general check and `IsPlacingBarrier`/`IsPlacingLure` are
convenience predicates; the reticle is green for a barrier and cyan for a wisp, matching the
colours those objects glow with. All existing barrier tests were moved onto the shared names, and
the console and window frontends bind `B`/`Space` to a barrier and `R` to a wisp.

## 2026-10-20 — A `GameFactory` in Core, so "descend again" is a tested rule

**Decision:** `GameFactory(config, Func<IRandom>, IPathfinder)` builds one complete game per
`Create()` call: its own random source, the shipped maze pipeline (`MazeGenerator` →
`DeadEndBraider` → `BraidedMazeGenerator`), then `GameState`. The frontends receive
`Func<GameState>` and start a run by calling it; `Program` composes the factory once.

**Reason:** Until now the maze and the state were assembled once in `Program`, which was fine
while a session was exactly one game. A replay makes "start a game" a rule that both the window
and the console need, and duplicating a five-line pipeline in two event loops is how the two
frontends drift apart. Putting it in Core also makes the contract testable without a window: a
replay must be a *fresh, independent, fully restored* game, and that is now six assertions
rather than a manual check. Each call getting a new `IRandom` is what keeps a seeded config
reproducible and an unseeded one genuinely new.

**Consequences:** `GameConfig.Validate` runs in the factory constructor, so a bad config fails
before the title screen appears rather than at the first `Create()`. `Create()` is cheap
(milliseconds), so calling it again on death is a normal path, not a special mode. The factory
hard-codes the shipped decoration order; anything that changes the maze's shape belongs there
(and gets a test), not in `Program`. A `Seed` still means "this exact game", so a fixed-seed
build replays the same cave — documented behaviour, not a bug.

## 2026-10-20 — The title screen is a renderer partial plus a frontend screen, not a game mode

**Decision:** `RaylibRenderer.Title.cs` is a `partial` of `RaylibRenderer` exposing
`DrawTitle(int selectedItem)`; the menu selection lives in `RaylibFrontend`, which keeps a
two-state loop (title, game). `ConsoleFrontend` prints an ASCII title and waits for a key, but
skips the wait when input is redirected. Rendering a game and rendering the title are separate
calls, and the title is not a `GameState`.

**Reason:** A title screen has no rules: it must not pause, tick, or exist in Core, or every
rule test would need to know about a menu. Making it a partial of the cave renderer instead of
its own class means it reuses the palette, the vignette, the grain, `TextCentered` and the
spore hashing rather than keeping a second copy of the art direction that would drift. The
console gets its own cheap version because the same "you are about to enter a cave" beat matters
more than pixel parity.

**Consequences:** The renderer grew a public method that takes an index rather than a state,
which is the one place it is allowed to draw something other than the game — kept explicit in
the docs. Raylib's built-in font is ASCII-only, so the wordmark is spaced by hand and the menu
chevrons are drawn as triangles; do not add decorative glyphs like `·` or `›`, they render as
boxes. Because `Cell` is a value type, cells from a new maze compare equal to the old cave's
cells, so every new run calls `RaylibRenderer.Reset()` to clear the explored-cell memory; a
replay that skipped this would draw the previous cave's walls. The console title needs a key
that a piped run cannot send, so it is skipped under redirection — the same limitation the
console input loop already documented.

## 2026-10-20 — A catch costs a heart and resets the chase (roguelike Phase 1)

**Decision:** The player enters a floor with `StartingHearts` (3) out of `MaxHearts` (3). A catch
spends one heart; the killer is teleported to the furthest reachable cell (dead ends preferred,
keys/exit/wisp/barriers avoided while a legal alternative exists), stunned for
`RespawnStunSeconds` (1.5), and the player is invulnerable for `InvulnerabilitySeconds` (1.5).
Keys, barriers, the wisp and the layout are untouched. `GameStatus.Caught` now means "the last
heart was spent" and still ends the run; a catch with hearts left stays `InProgress`.

**Reason:** The request was a roguelike where a mistake is survivable, so damage has to cost
tempo rather than progress. Three discrete hearts are readable in a small HUD and make each
loss a story, unlike a hidden health number. The respawn is what makes one mistake cost
exactly one heart: without the teleport, a killer standing on the player would deal three hits
in three consecutive steps. Keeping the player's cell and their keys is what stops hearts from
feeling like a checkpoint restart.

**Consequences:** `GameState` gained `Hearts`, `MaxHearts`, `IsInvulnerable`,
`InvulnerabilitySecondsRemaining`, `IsEnemyStunned` and `EnemyStunSecondsRemaining`, and
`TakeHit` is the single place a heart is spent. Two small rules protect the fiction: the killer
waits beside the player while the grace window is open instead of sharing the cell, and walking
into it during that window is refused instead of moving through it. The stun is charged in real
time and freezes the step accumulator, so it is a usable window rather than a delayed step.

The existing chase tests were given one heart through `TestLevels` (and the lure helper) so they
keep testing "caught ends the game"; the three-heart economy has its own `GameStateHeartTests`.
The renderer infers the damage flash from a one-frame drop in `Hearts` because there is no event
stream yet — that is a known, documented shortcut until audio brings `GameEvent`. The numbers
are untuned guesses; a stationary player was caught every ~20 s in the measurements, which is
the intended pressure but needs a human play-test.

## 2026-10-20 — Lighting is painted under contents, not over them (key visibility bug)

**Decision:** `DrawCellDetails` now draws the lantern falloff after the walls and flora but
*before* the cell's contents, so keys, the exit, a wisp, the killer and the player are never
dimmed by the lightmap. The key was also rebuilt as a beacon: a magenta pool on the floor, a
six-band vertical shaft that spills past the cell edge, a pulsing ring, a bobbing bright core
with a white highlight, and two orbiting sparks.

**Reason:** A play-test reported keys as invisible. They were being drawn correctly and then
covered by a `Rgba(2, 8, 9, alpha)` rounded rectangle whose alpha reaches ~170 at the edge of
the torch, so a 7 px pink cap with a 50-alpha glow was multiplied down to a handful of levels
above a near-black floor. The falloff is a *lighting* layer, not a foreground layer: applying
it to entities means the game hides exactly the things the player must be able to react to. The
beacon addresses the second half of the problem — even undimmed, a 7 px mushroom is easy to
mistake for the ambient flora that grows in the same colours.

**Consequences:** Contents now render on top of the darkness, which also makes the killer and
exit readable at the edge of the torch; that is intentional (the killer is only drawn when the
aura rule already makes it visible). Ambient flora stays under the falloff so it still dims with
distance. The new beacon is presentation only — no rule, no test and no Core type changed, and
the 301 tests stay green. When the pixel renderer replaces this one (FUTURE.md Phase 6), the
same ordering rule applies: lightmap first, then sprites, then the HUD.

## 2026-10-20 — A `Campaign` above the floor, and floors as data (Phase 2)

**Decision:** A run is a `Campaign` that owns an ordered, snapshotted list of `FloorDefinition`
records, the current one-floor `GameState`, and the hearts carried between floors. `GameState`
is unchanged as a unit — it still knows nothing about floors. A floor's `GameStatus.Won` now
means "this floor's exit was reached", and the run's result lives in `CampaignStatus`
(`InProgress` / `Won` / `Lost`). `Campaign.TryAdvanceFloor()` builds the next floor with
`StartingHearts` equal to the hearts left and `MaxHearts` from the floor. The shipped catalog is
three floors (The Shallows, Rootbound, Drowned Halls) with 3/3/4 keys and killer steps
0.60/0.54/0.48 s. Frontends now take `Func<Campaign>`.

**Reason:** The alternative — teaching `GameState` about floor numbers — would have put the run's
state inside the object that 331 tests treat as "one floor", and every rule test would have had
to know about a campaign. A separate layer keeps the tested rules exactly as they were: the
campaign only sequences `GameState` instances and moves hearts between them. `GameStatus.Won`
also could not keep meaning "the run is over" and "this floor is done" at the same time; splitting
the enums makes the frontend's transitions explicit instead of inferred from a flag.

**Consequences:** `GameConfig.ForFloor` composes a floor over the run's shared rules, so the
per-floor knobs are only size, keys, killer pace and heart ceiling while vision, barriers, wisps,
braiding, aura and the seed are inherited. `GameFactory` gained `CreateCampaign()` and
`CreateFloor(floor, hearts)`, and its random factory changed from `Func<IRandom>` to
`Func<int?, IRandom>` (a public API change): each floor uses `seed + (number - 1) * 7919`, so a
seeded campaign is reproducible while its floors are different layouts. `Create()` remains for
tests and tools and builds a single game from the base config. The campaign list is snapshotted
and its floor numbers must increase, so a mutable list or a bad catalog fails at construction.
There is no save file yet — it is Phase 8, because it depends on the final floor and card data;
a three-floor run is short enough to finish in one sitting in the meantime. Floor themes are
names, accent colours, key counts and killer pace only; the per-floor cave look is deliberately
deferred to the sprite compiler (Phase 6) and palette work (Phase 8), so no renderer hacks should
be added for it now.

## 2026-10-20 — Cards change a floor's inputs, not its rules (Phase 3)

**Decision:** A card is data: `Card` (id, tier, face sentence, `CardEffect`) plus `CardCatalog`.
The run accumulates the held cards in `RunModifiers`, and `GameFactory.CreateFloor` applies those
totals over the floor's `GameConfig` before building the ordinary `GameState`. `GameState` gained
no knowledge of cards; the only rule additions are two tuning values (`LureStunSeconds`,
`LureVisionPenalty`) that default to zero, so the shipped game without cards is unchanged.
`GameConfig` became a **record** so the composition is a `with` expression rather than a
hand-written copy of every field.

**Reason:** The tempting alternative is a pile of `if (hasCard("wide-eyes"))` checks inside
`GameState`. That would put run-level state inside the object the rule tests treat as "one floor",
and every new card would touch the most-tested code in the project. Composing over the config
keeps all 415 tests meaningful: they still describe a floor played by the same rules, and a card
test only has to assert the numbers the floor was built from. Making `GameConfig` a record removes
a specific failure mode: a 25-field hand-written copy silently drops any value added later, which
is exactly how a tuning number gets lost.

**Consequences:** `RunModifiers.Apply` clamps and then validates: vision never drops below one step
once a card touched it, budgets and counts never go negative, the killer never gets faster than
0.05 s per step, and a heart ceiling never falls below the hearts being carried. A composition that
still cannot describe a playable floor (for example extra keys that do not fit the maze) throws
from `Apply` instead of producing a broken floor. Two catalog texts were adapted to what the rules
can honestly deliver: **Second Wind**'s vision cost is run-wide rather than "for the rest of the
floor" (the draft happens between floors, so "this floor" had no referent), and **Trophy
Hunter**'s heal is capped by the floor's ceiling rather than allowed one over it.

## 2026-10-20 — The draft is a gate in the run, not a screen in the frontend

**Decision:** `Campaign` owns the offers, the held cards and the pending heals.
Clearing a floor that is not the last opens `Campaign.Draft`, and `TryAdvanceFloor` returns
`false` until it is resolved with `TakeCard` or `SkipDraft`. Offers come from `DraftFactory`,
seeded by `{run seed, floor number}`, and `Draft`'s mutation methods are internal to Core.

**Reason:** If the frontend owned the draft, "take a card or skip" would be UI convention rather
than a rule, and any second frontend could forget it — the same reason hearts and floors already
live in `Campaign`. Keeping the mutation internal means a frontend can draw the offers but cannot
resolve them without the run's modifiers hearing about it. Deriving the offers from the seed rather
than storing them is what keeps a future save file small: `{seed, floorIndex, hearts, cardIds}` is
enough to reproduce the run, including what it offered.

**Consequences:** Existing campaign tests had to resolve the draft before advancing — a deliberate
behaviour change, not a test fixup. `TryAdvanceFloor` still refuses on the last floor (the run is
won). Skip heals exactly one heart, applied when the next floor is built and capped by that floor's
ceiling; a card's one-shot heal is banked the same way, while a per-clear heal is read from the
modifiers at advance time so that merely looking at a draft cannot change the run's health. The
draft's non-config effects (one-shot heals, queued draft-size changes) are applied at pick time by
`Campaign`, because they are events in the run rather than numbers on a floor.

## 2026-10-20 — A card is withheld when the run cannot pay for it

**Decision:** `Card.DraftsNeededAfterThisOne` says how many drafts must follow before the card can
be offered; `DraftFactory` withholds a card when there are fewer. `Lucky Find` needs two, so it
never appears in the shipped three-floor campaign.

**Reason:** The harness caught the alternative being wrong in a way tests had not: with three
floors, `Lucky Find` was taken at the first draft, its bonus made the second draft offer four
cards, and then the run ended — the price (a two-card draft afterwards) could never be paid. That
made it a pure upgrade in the one run the game actually ships, which the design rules forbid. The
rule is expressed as a number rather than a Lucky-Find special case so a future card paid for
later can declare its own requirement.

**Consequences:** One of nine catalogued cards is unreachable in the current run; it is covered by
tests and appears in any run of four or more floors. `DraftFactory` also treats a negative
remaining count as "last draft", so a miscomputed length can never open a hole. The other three
draft guards — no duplicates unless stackable, no contradictory pair, at most two vision-changing
cards per run — are enforced in the same place, with tests over a fake two-card catalog so each
rule is the only thing that can decide the outcome.

## 2026-10-20 — A wisp's stun and its vision cost are floor rules, and its light goes out

**Decision:** Two new `GameConfig` values back two cards: `LureStunSeconds` (reaching a wisp stuns
the killer for that long) and `LureVisionPenalty` (a burning wisp subtracts that many steps from
the player's vision). `GameState.EffectiveVisionRadius` applies the penalty, and `ClearLure` now
recomputes visibility.

**Reason:** Both cards describe a mechanical change the player can see in the next thirty seconds,
so they belong in the rules as numbers, not in the renderer as effects — a stun that only looked
like a stun would be a lie, and a vision penalty the fog ignored would not exist. The visibility
recompute in `ClearLure` was a real bug the new rule exposed: the wisp's own pool of light used to
stay in the visible set after it expired or was consumed, until the player's next move re-derived
it. The penalty made that stale state load-bearing, so it is fixed at the source.

**Consequences:** Both values default to zero, so no existing behaviour changed without a card. The
stun uses the same window as the post-catch stun, so the killer neither moves nor banks progress
towards its next step during it. The penalty can reduce vision to zero (the player's own cell only)
but never below it, and `RecomputeVisibility` reads the effective radius rather than the floor's
own number.

## 2026-10-20 — Rules move on the grid; the renderer interpolates (Phase 4)

**Decision:** A player step still changes `GameState.Player` the instant it is accepted. What the
step time gates is the *next* step: `TryMove` refuses while `PlayerStepSeconds` is cooling down and
stores at most one buffered direction, which `Tick` spends when the cooldown ends. The same
approach applies to the killer, whose step time already gated its movement. `GameState` then
publishes `PreviousPlayer`/`PlayerStepFraction` (and the killer pair), and `RaylibRenderer` draws
both figures between those cells.

**Reason:** The alternative — holding the cell change until the step completes — would create a
window in which the player is neither on the old cell nor the new one, and every rule that reads a
cell (key pickup, the arch, a catch, two path searches) would need a definition for it. That is a
lot of new state for a visual effect. Making the cell change immediate keeps the rule surface
exactly as it was, which is why all 415 earlier tests still describe the same game; the
interpolation reads a previous cell and a fraction and is deliberately one-way.

**Consequences:** A buffered step is one input, dropped if it points into a wall, and cleared when
the game ends inside `Tick` (a buffered step that wins the floor must not let the killer take
another step). An enemy respawn snaps instead of sliding — it is a throw, not a walk. Anyone adding
a new moving actor needs a previous-cell/fraction pair of its own; do not make the renderer track
positions itself.

## 2026-10-20 — Zero step time is the rule-test seam; shipped floors are timed

**Decision:** `GameConfig.PlayerStepSeconds` defaults to 0 (instant) and
`FloorDefinition.PlayerStepSeconds` defaults to 0.11 with the catalog shipping 0.11 / 0.10 / 0.09.
`GameConfig.ForFloor` copies the floor's value.

**Reason:** Every existing rule test steps the player and asserts a cell on the next line, and the
tools (`Create()`, hand-built test levels, the harness's walker) are written the same way. Making the
base config timed would have forced a tick into hundreds of assertions without testing anything
new, and would have made the tests describe a game the rules do not depend on. Putting the value on
the floor also matches the plan: the base player step is a per-floor number that cards may
multiply, not a run-wide constant.

**Consequences:** A floor of step 0 cannot sprint (sprinting would have nothing to shorten), which
is explicit in `IsSprinting`. The shipped ratio is about 5.5:1 against the killer's step, so the
descent tightens both sides together. `GameConfig.Validate` still rejects a negative or non-finite
step, so a bad config cannot make movement nonsensical.

## 2026-10-20 — Sprint is a request with a refill gate, not a speed toggle

**Decision:** The input calls `GameState.SetSprinting(bool)` every frame; `IsSprinting` grants it
only when the player asked, stamina is above zero, the floor has timed steps, and the run is live
and not aiming. Sprinting multiplies speed by `SprintMultiplier` (1.4) and drains
`SprintStaminaSeconds` (1.2); an empty bar must refill to 25% before sprint can be used again.

**Reason:** Sprint is meant to be a decision about *when* to spend a resource, not a movement mode
players hold permanently. Draining without a gate lets a player sprint one step at a time as the
bar trickles back, which is both ugly and strictly better than saving it. Making it a request keeps
the rule in one place: the frontend cannot grant itself a speed the game has not allowed.

**Consequences:** The HUD shows the bar only once stamina has been spent, so a player who never
sprints is not given another meter to read. Sprint currently costs only stamina; the plan's
diegetic cost (a footstep ring in the fog, the killer turning toward it) is not implemented and is
recorded as a known gap. The catalog's Sprint Lungs card now overlaps with the free sprint and
needs revisiting when the remaining cards ship.

## 2026-10-20 — Walking reads held keys; the aim cursor reads taps

**Decision:** In the window, direction keys are sampled with `IsKeyDown` while walking and with
`IsKeyPressed`/`IsKeyPressedRepeat` while aiming. Shift is sampled every frame as a sprint request.

**Reason:** A held key plus the game's own cooldown gives continuous walking without the frontend
knowing the cadence, and without the operating system's auto-repeat rate leaking into movement
speed. The aim cursor is the opposite: a cursor that keeps moving while a key is held flies across
the map, and aiming is a deliberate, precise act that pauses the clock.

**Consequences:** Pressing a direction faster than the cadence is harmless — the extra press is
buffered, then ignored. The console frontend cannot hold a key, so it stays one press per step and
has no sprint mapping; that is a limitation of terminals rather than a rule difference, and it is
listed in `PROGRESS.md`.

## 2026-10-20 — Movement cards are config knobs; Momentum earns one small rule

**Decision:** The movement cards ship as data wherever possible: `CardEffect.PlayerStepScale`,
`InvulnerabilitySeconds` and `SprintStaminaScale` are composed by `RunModifiers` and clamped in
`Apply`, and the catalog carries the values. Momentum needs behaviour, so it becomes four
`GameConfig` numbers (`MomentumRampPerStep`, `MomentumMaxBonus`, `TurnPenaltyScale`,
`TurnPenaltySeconds`) that are **all zero by default**, with one rule in `GameState`
(`TrackMomentum` plus the bonus in `NextPlayerStepSeconds`).

**Reason:** The architecture's promise is that a card changes a floor's inputs, never a branch in
`GameState`. A card that is a behavioural rule breaks that promise unless the behaviour is a
numbered property of the floor that happens to be off. Momentum is exactly that: with the zeros the
whole code path is skipped, so a run without the card is pixel- and millisecond-identical to
before, and the four values are one record in the catalog to retune. Light Feet, Hollow Bones and
Sprint Lungs needed no rule at all.

**Consequences:** Any future card of this kind follows the same shape: a validated `GameConfig`
number (or four), defaulting to "off", and a test that the defaults change nothing. The numbers
themselves are unplay-tested guesses and are recorded as such in `PROGRESS.md`.

## 2026-10-20 — A card may raise a floor's heart ceiling, so hearts are checked after composition

**Decision:** `Campaign.HeartsOnNextFloor` caps at `floor.MaxHearts + modifiers.MaxHearts`, and
`GameFactory.CreateFloor` builds the base config from `min(startingHearts, floor.MaxHearts)`, applies
the cards, and only then validates that `startingHearts <= config.MaxHearts` (throwing when nothing
could justify the carry).

**Reason:** Two separate caps ignored the run's cards. The campaign capped carried hearts at the
floor definition's ceiling, so a rising ceiling (Blood Pact) could never be filled and a banked
skip-heal into a four-heart floor vanished. The factory validated the floor definition's ceiling
*before* the cards were applied, so the extra heart would have thrown the moment it got through.
Both are the same mistake — validating a partly composed object — and both are now checked once,
against the object the floor is actually built from.

**Consequences:** A ceiling a card bought is usable (the run can start a floor at four hearts), and
an impossible carry still fails loudly instead of being clamped away silently. `Light Feet`'s -1
slot does not take a heart the player is holding: `Apply` floors the ceiling at `StartingHearts`,
which is intended but only bites once wounded.

## 2026-10-20 — The sprite compiler outputs committed PNGs, with `--check` as the brake

**Decision:** `tools/AssetForge` is a console project with no packages, referencing Core only for
`SeededRandom`. Tiles are compiled from palette **indices** on a `PixelCanvas`; the palette is
derived from two hues on a fixed lightness ladder (`Palette.Derive`); the PNG encoder is
hand-written (stored deflate, no compression); the atlases and a manifest are committed to
`src/MazeGame.UI/Content/Generated`. `--check` validates themes, neighbour palette distance and
structural difference, recompiles twice for determinism, and fails when a committed atlas or the
manifest no longer matches. The game does **not** load the atlases yet.

**Reason:** Index canvases keep a tile theme-blind, so twenty themes are twenty parameter blocks
and no drawing code branches on a theme id. Derived palettes make contrast a property of the code
rather than of an artist's luck, and the distance metric turns "each floor feels different" into
something a machine rejects. Committed output means a fresh clone needs no tool step and a diff is
visible in review; determinism is what makes "committed" safe. Stored deflate is a deliberate
trade: a few KB more per atlas in exchange for an encoder small enough to audit and output stable
enough to hash.

**Consequences:** Changing a tile routine, a theme or the palette is a two-step change — recompile
and commit the diff — and forgetting is a red test, not a silent drift. The atlases are inert until
the pixel-renderer phase loads them, so this slice cannot regress the game. Compression can be
added later, but it must be added with a one-shot re-commit of every atlas.

## 2026-10-20 — The explorer snaps to its tile; only the killer slides

**Decision:** `RaylibRenderer.DrawFigures` draws the player at `CellCenter(state.Player)` and the
lantern pool at the same point. The killer keeps `FigureCenter(PreviousEnemy, Enemy,
EnemyStepFraction)`. `PreviousPlayer`/`PlayerStepFraction` stay published, documented as the step's
clock for a walk-cycle animation rather than a position.

**Reason:** With a 0.11 s cadence and held keys, interpolating the player meant the figure was
between cells almost all the time and never visibly *on* a tile. Counting cells while being chased
is the core navigation skill, and the slide made it guesswork. The player's cell already changes the
instant a step is accepted, so drawing it there is the most honest picture of what the rules are
doing — one press, one tile. The killer is the opposite case: it is at a distance, the player never
counts its tiles, and a smooth drift makes it read as a creature instead of a marker.

**Consequences:** Any future renderer must snap the player too; a smooth camera or a scroll should
not reintroduce per-figure interpolation. The step fraction is still the right input for the walk
cycle (idle/walk0..2) once the pixel renderer animates from the atlas.

## 2026-10-20 — Autotiling and the rig are generated from data, not drawn by hand

**Decision:** `Autotiler` builds the 16 edge tiles and all 47 blob tiles from open-side masks, with
corner bits canonicalised (a diagonal matters only when both of its edges are open). `Rig` draws
the cast from `(theme, facing, frame)` with four facings coming from two drawings plus a mirror.
`SpriteCompiler` lays tiles eight to a row and lists every tile's name and position in the manifest.

**Reason:** A tile that is generated from the same mask the renderer will use to choose it cannot
drift from the map ("tile *n* provably matches mask *n*"), which is the property that makes an
autotiled map possible at all. Canonicalising corners is what keeps the blob set at 47 tiles instead
of 256, and asserting that count in the static constructor means a bit-order mistake fails at
startup rather than by rendering a wall with the wrong hole. The rig's facings are mirrored because
four hand-drawn directions would be four chances to disagree.

**Consequences:** Changing the tile shape or a pose means recompiling and committing the atlases;
`--check` fails otherwise. The bit order is fixed (N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128)
and documented on `Autotiler.BlobClass`; the renderer must use the same order.

## 2026-10-20 — Tap versus hold is a Core rule (`StepRepeat`), not a frontend detail

**Decision:** `MazeGame.Core.Game.StepRepeat` owns the input cadence. It is handed the direction
whose key is down (or null), whether the previous step is still running, and the frame's time, and
returns the direction to send this frame or null. The first frame of a press returns the direction;
later frames return nothing until the key has been down 0.15 s **and** no step is running; a
release clears it, so the next press is fresh. `RaylibFrontend.ReadWalk` maps the returned
`Direction` to a `GameCommand` and does nothing else.

**Reason:** The old code sent the held direction every frame, so a tap lasting a few frames left a
press in the state's one-slot buffer and could move a second tile after the player had let go —
exactly the "it moves more than one tile" complaint. Deciding it in Core costs nothing (the class has
no maze, no window and no dependency beyond `Direction`) and buys the only test that matters: nine
unit cases plus three that drive it against a real `GameState`, including "a slow tap while stepping
is worth exactly one tile". A private frontend class could not be tested without a GLFW context,
which is why the earlier version shipped with the bug.

**Consequences:** Any frontend that wants tap-to-step reads the gate rather than polling held keys;
a rerun at a different frame rate changes nothing, because repeats wait for the step clock instead of
the frame clock. Shift needs no branch anywhere: sprint shortens the step in Core, so the same repeat
simply lands sooner. The console frontend is untouched — a terminal press is one event, so it is
already one tile per press and has no hold to distinguish.

## 2026-10-20 — The floor is drawn on a virtual canvas at an integer scale

**Decision:** `PixelLayout` sizes a canvas from the floor (`16 px` per cell, an `8 px` margin, an
`18 px` header), `PixelView` owns a `RenderTexture2D` at that size with a point filter, and
`Present` blits it to the window at an **integer** scale (`Fit`, 1 to 4) with a centred offset and a
cleared letterbox. Nothing inside the game knows the window size.

**Reason:** Pixel art has one honest sampling rate. A fractional scale puts tile edges on half
pixels, and a sharpening filter afterwards only changes which kind of wrong it looks. Making the
canvas the unit of layout also means a resize is a scale change and nothing else — no coordinate in
the game moves. Sizing the canvas per floor rather than fixing 336x400 keeps a 15x15 floor from
carrying dead space on four sides while still fitting a 19x19 one, which is the size the design note
budgets for.

**Consequences:** Adding a screen means adding it to the canvas, not to the window. The vector chrome
(the status pills, the hints, the loss card) deliberately lives outside it, in the letterbox, so the
HUD keeps its designed type size while the tiles stay at whole-pixel scale; slice 2 of the phase
moves the chrome in once there is a pixel font.

## 2026-10-20 — Thin wall faces, because a wall here lives between two cells

**Decision:** `tools/AssetForge/WallStrips` compiles four 16x16 faces per theme (`wall-north`,
`-east`, `-south`, `-west`): a 5 px band on a transparent canvas, outer rock then a lit rim then a
contact shadow. `PixelRenderer` draws a cell's north and west faces, plus the south/east rim on the
last row and column, and the floor underneath. The older solid `wall` plus the 16 edge and 47 blob
tiles stay compiled and validated but unused.

**Reason:** The maze stores walls *between* cells — every cell is a room — while the solid autotile
set describes a cell that *is* wall (a full cell of rock with its open sides carved). Using it here
painted rock in the middle of every room, and using it on both sides of one wall painted two bands
16 px apart. The honest shape for this maze is a face along the closed edge, and ownership by
north/west paints every shared wall exactly once. The solid set is still the right art for the
floors whose chasm and veto cells arrive in Phase 8, so it is neither deleted nor reworked.

**Consequences:** Changing wall thickness is one constant (`WallStrips.Thickness`) plus a recompile
and a committed diff. A future "one cell of rock" floor must switch to the blob tiles by mask, which
`TileMask.BlobTile` already computes.

## 2026-10-20 — The tile masks exist twice, and a test keeps them identical

**Decision:** `MazeGame.UI.Rendering.Pixels.TileMask` reimplements the compiler's eight-neighbour
mask, canonical corner rule, 47-class table and 4-bit edge index, because the game must not
reference `tools/AssetForge`. `TileMaskTests` compares both tables for all 256 masks (and the
canonical set against `Autotiler.BlobMasks`), so a change to one that is not made to the other is a
red test.

**Reason:** The manifest is the only contract between the compiler and the game; the bit order is
part of it, and a mismatch cannot be seen in a PNG — it shows up as a wall with its hole on the
wrong side. Duplicating twenty lines of bit logic is cheaper than shipping the tool, and a test over
all 256 inputs is complete rather than sampled.

**Consequences:** The UI's pure rendering helpers are now covered by the test project, which means
`MazeGame.Tests` references `MazeGame.UI` (no test opens a window, so the native library is never
called). The compiler remains the source of truth: if the bit order ever changes, the test is what
says so.

## 2026-10-20 — A theme names a shape family; the families share no drawing code

**Decision:** `Theme.Family` is a `TileFamily` (`OrganicCave`, `WrongOrganic`, `WorkedStone`), and
`tools/AssetForge/TileGrammar` is the only place a family is named: it switches floor, solid wall,
wall face and props to `CaveTiles`, `WrongOrganicTiles` or `WorkedStoneTiles`. The three files share
seeds, the `PixelCanvas` primitives and the palette roles, but not one drawing routine.

**Reason:** The three floors were the same boulders, roots and joints in three hues — a player could
not tell Rootbound from The Shallows with the colour turned off. A theme is data, but colour is the
*last* thing that distinguishes a place; shape is the first. A family per place also keeps the
alternative honest: nine themes later, a new floor is either a new record in an existing family or a
new file, never an extra branch inside a shared routine.

**Consequences:** `Theme` grew the knobs a family reads (`CourseHeight`, `VeinDensity`) and
`--check` compares neighbouring themes on parameters; that only proves the *data* differs, so
`tests/.../ThemeFamilyTests` compares the compiled tiles on palette **indices** — two themes that
draw the same shape produce the same index grid whatever their hues, which makes "it is a recolour"
a red test rather than a matter of taste.

## 2026-10-20 — The font is its own atlas, and the campaign cards are set in it

**Decision:** `tools/AssetForge/Glyphs` holds a hand-coded 5x7 face for every printable ASCII
character; `FontAtlas` compiles it into `font.png` on 6x8 cells (5x7 plus a row and column of gap)
with the heart and pip icons in a final row, and appends its own `theme`/`tile` section to
`manifest.txt`. Tile lines now carry an explicit width and height, defaulting to 16x16 so the maze
atlases read the same as before. `PixelFont` blits text and icons at an integer scale;
`PixelCards` draws the floor intro, floor-cleared, run-summary and caught cards; the vector
implementations of those four are deleted.

**Reason:** The screens were the last place the game looked like a program with a window: a system
typeface over pixel art reads as a placeholder at any size, and a vector panel cannot be framed by
the 9-slice chrome the phase calls for. Compiling the type keeps the "no external assets" rule and
puts the text under the same `--check` as the tiles, so a missing glyph is a failing test rather
than a hole in a sentence.

**Consequences:** Text is crisp only at integer scales, which is the right constraint for pixel art
and the reason the cards pick 4 → 2 by measured width instead of scaling to fit. A character the
face does not have draws as `?` rather than throwing: the text comes from game data, and a stray
symbol in a floor's name must not end a run mid-frame. The draft screen is the last vector screen,
and the HUD still lives in the letterbox; both are slice 2b.

## 2026-10-20 — The canvas is a camera viewport, sized by vision

**Decision:** `PixelLayout` no longer draws the whole floor. The canvas is a viewport whose size
comes from the floor's vision radius — the lit radius plus one ring, rounded to an odd number and
clamped to 9–13 cells — and `PixelCamera` positions it so the player is the centre cell, clamped to
the floor so the frame never shows past the maze's edge. `PixelRenderer` culls every pass to the
viewport and rebuilds its one-texel-per-cell lightmap in viewport space; exploration is still
recorded across the whole maze, because a generous vision card can light cells further out than the
camera shows.

**Reason:** Drawing the whole 15x15 floor at 2x spent most of the screen on cells the lantern could
not reach, so the play area was small and the maze read as a diagram rather than a place. A viewport
makes the same floor bigger, and it makes the camera do the work that the fog used to do badly.

**Consequences:** A card that raises vision is a zoom out: the viewport grows and the render target is
rebuilt once, so the effect is "you can see further" rather than "the map got smaller". The cap at
thirteen cells is deliberate — a stacked vision run zooms out to a wide view without turning the maze
into a minimap, and the fog keeps the unseen edge black. The window is now sized from the viewport
(696x872 at 4x) instead of the floor, which is what makes one maze cell 64 screen pixels.

## 2026-10-20 — Walls get a guaranteed contrast, and the edge treatment is shared

**Decision:** The palette's lightness ladder moved the wall body from 0.29 to 0.37 and the lit rim
from 0.42 to 0.54, against a 0.20 floor and a 0.07 contact shadow. `WallStrips.Face` now paints the
rim and the shadow *after* the family's own texture, so no family can bury the two rows that say
"wall". `WallContrastTests` measures relative luminance and fails if the body is under 1.6x the
floor, the rim under 2.2x, or the shadow lighter than 0.7x.

**Reason:** A wall here is a five-pixel band painted over a floor tile, so it has almost no area to
make its case. With the old ladder the band sat barely above the floor and, in Rootbound, a family
had painted moss over its own shadow row — the band read as part of the floor, which is exactly what
the player reported. Contrast is not something to eyeball per theme: it is the one number that has
to hold for every palette the game will ever ship.

**Consequences:** Walls are noticeably brighter than floors in every theme, which costs a little of
the cave's gloom but buys legibility. The shared rim-and-shadow rows mean the two edge rows are no
longer a place for family detail; the three body rows still are, and the shape languages remain
distinct there (courses, fibres, boulders).

## 2026-10-20 — Twenty floors, each a gimmick; the rule knobs live on the floor

**Decision:** `FloorCatalog` holds twenty floors. A floor may carry a `Gimmick` sentence for its
card, and any of the optional knobs `VisionRadius`, `BarrierBudget`, `LureCount`, `LureSeconds`,
`ExtraLoops`, `DeadEndBraidPercent`, `EnemyAuraRadius`, `EnemyAuraDistance`, `RespawnDistance`,
`InvulnerabilitySeconds`; `GameConfig.ForFloor` folds them over the run's settings. Nothing in
`GameState` knows a floor by name.

**Reason:** Twenty floors that only differ in key count and killer pace are twenty difficulty steps,
not twenty places. The interesting differences — a floor where the lantern dies, one where barriers
do not exist, one where the killer arrives without its glow — are all *settings the rules already
understand*. Putting them on the floor record keeps the content seam where the project already said
it was: a floor is data, and a gimmick is data, not a subclass or a branch.

**Consequences:** A gimmick is only as interesting as the knobs allow; a floor that wanted new
mechanics would still need a rule. The tests now assert the *shape* of the descent rather than a
strictly falling pace — the two slow, lit floors are deliberate breathers — and require every floor
to carry a card line and a signature no other floor repeats.

## 2026-10-20 — A wall must beat its floor by a measured ratio, and floor 2's floor was the problem

**Decision:** `WallStrips.Face` paints the near-black outline and the lit rim *after* whichever family
drew the band, so no texture can bury the two rows that say "wall"; the palette's wall body is 0.40
and its rim 0.58 against a 0.20 floor. Rootbound's floor growth was moved from the wall's body colour
to moss-over-shadow. `WallContrastTests` measures relative luminance of the painted band against the
painted floor and fails under 1.6x body / 2.2x rim.

**Reason:** The player reported that floor 2's walls disappeared into its tiles. They were right, and
the cause was not the wall: the wrong-organic floor drew full-width fibres in `Wall` and knots in
`Wall`/`WallTop`, so the floor already contained wall-bright lines — band vs floor was 1.52x, the worst
of any theme. A wall here is a five-pixel band over a floor tile, so its whole claim to being an
obstacle is contrast; that is a number, not a vibe, and it belongs in a test.

**Consequences:** All twenty themes now sit between 1.96x and 7.30x (Rootbound 2.23x), and in a
rendered frame a walled cell's top rows read up to 2.9x its own floor against 0.88x for open cells.
The cost is that the root floors are no longer lit by bright fibres; their glow comes from sap, props
and the lantern instead. Two rows of every wall band are now shared by all families, so a new family
decorates the body rows and inherits a legible edge.

## 2026-10-20 — A floor publishes events; the UI turns them into sound

**Decision:** `GameState` keeps a small capped queue of `GameEvent` values and drains it on
request. The frontend drains it once a frame, feeds the events to the audio layer, and Core never
mentions audio, Raylib or a mixer.

**Reason:** The alternative — the UI diffing two frames to notice a key disappeared, or polling
`Hearts` — cannot distinguish "a heart was spent" from "the HUD was rebuilt", cannot see a seal
crack, and would need a second copy of the rules to guess. Events are also what a future VFX layer
wants, and they are cheap: a queue of at most 32 ids.

**Consequences:** The queue is capped, not unbounded, so a frontend that never drains (the console
view, a long test) keeps only the most recent moments. A new moment that needs a sound is one enum
value and one `Raise` call in Core, plus one line in the director's switch.

## 2026-10-20 — The score is twenty parameter sets, arranged from five motifs and mixed as stems

**Decision:** A floor's music is a `MusicTheme` record (root, mode, tempo, bars, progression,
instrument mix, density, tension, reverb, band), not a track. `ScoreRenderer` arranges one loop of
three stems — bed, rhythm, lead — from the floor's record plus five shared motifs, and
`AudioEngine` mixes the stems with gains. `MusicCatalog`'s ids are the atlas theme ids, so music
and tiles for a floor cannot drift apart.

**Reason:** Twenty tracks of authored audio are impossible without assets, and twenty unrelated
generated tracks would not sound like one descent. Generating *stems* is what lets the music react
without a second arrangement: `Calm` opens the bed, `Tense` adds the bass, `Chase` opens the motif
and drums, and the change is applied on the next bar line so it feels composed. It also makes the
loop seam a rendering problem (wrap the tails, run the reverb around the loop) rather than a
crossfade problem.

**Consequences:** The score is rendered once per floor on the main thread (45–150 ms, hidden by the
intro card), and the loops are 10–16 s, so repetition is audible on a long floor. Balance and motif
memorability are ears-only judgements; the tests can only prove the tracks are audible, in time,
inside the ceiling and seamless.

## 2026-10-20 — The device is fed by Raylib's stream callback, not a polling thread

**Decision:** `AudioDirector` installs `SetAudioStreamCallback` and mixes into the buffer Raylib
hands it, through a Cdecl `[UnmanagedCallersOnly]` shim into `AudioEngine.Render`. The mixer
allocates nothing per block (a fixed 4096-frame scratch buffer, split when a block is bigger), and
the one-shot bank is built before the stream starts.

**Reason:** The first implementation was the plan's ring-buffer pump thread calling
`UpdateAudioStream`. On this machine that logged `STREAM: Buffer not available for updating` on
almost every block: raylib's `UpdateAudioStream` *refuses* a write when its ring buffer is full
rather than blocking, so a pump either spins with warnings or has to poll `IsAudioStreamProcessed`
— which empties the buffer between polls, i.e. a gap every period. The callback is the only mode
that runs at the device's pace without either failure.

**Consequences:** The callback cannot close over an instance, so the live director is a single
static that `Dispose` clears; the callback must not throw (it is a native boundary), which is why
the engine's path is allocation-free and bounds-checked. `AllowUnsafeBlocks` is enabled for the UI
project, and only `AudioDirector` uses it. Measured: 441 frames per callback, 100% of real time,
0.006 ms of mixing per 2048-frame block.

## 2026-10-21 — One typeface on every screen, and the chrome renderer gets it by injection

**Decision:** The title, the draft and the HUD/footer inside the window are set in the compiled
5x7 face, like the four campaign cards. `RaylibRenderer` no longer has a proportional text path:
its `Text` takes an integer scale and throws when no font was supplied, and `PixelRenderer` hands
its `PixelFont` to the chrome renderer in its constructor. Truncation is arithmetic because the
face is fixed-advance, and a strip that is too narrow drops a whole scale rather than shaving a
point off.

**Reason:** The game's typography was the last thing that read as a program rather than an indie
game: a system face over pixel art looks like a placeholder at every size, and the HUD in
particular sat in the same frame as the tiles. It also removed a second text implementation, so
spacing, wrapping and ellipses have one answer.

**Consequences:** The chrome renderer must be given a font before its first frame — the throw is
deliberate, because a silent fallback is exactly the inconsistency this entry removes, and it
caught a real crash when the HUD moved to the face. Text is only crisp at integer scales, so
layouts measure and pick 1, 2, 3 or 4 instead of scaling to fit; the footer centres its hints in
the room the hearts leave, and the HUD name truncates at a glyph boundary so a long name cannot
run into the status chips.

## 2026-10-21 — The lantern spills onto the rock, and a wall is drawn by whoever can see it

**Decision:** `GameConfig.NearSightRadius` (default 2, clamped to the torch in force) adds the cells
within straight-line distance of the player to the visible set, ignoring walls. The pixel renderer
additionally draws a cell's south and east wall face when the neighbour on that side has never been
seen, instead of relying on that neighbour to draw its own north/west face.

**Reason:** Sight is a walking-distance flood fill, and wall faces are owned by the eastern/southern
cell. A wall you were standing against therefore belonged to a cell nobody had seen, so nothing
drew it: the corridor read as a hole with the wall floating at its end. Players described it as
"walls don't render because you can't reach the other side", which is exactly what it was.

**Consequences:** The visible set is no longer purely "what the corridor reaches", so the rule's doc
says what the spill is for: the rock around you, not the corridors beyond it. It is capped by the
torch, so a vision-1 floor gets one cell and a blind floor none, and `NearSightRadius = 0` restores
the old behaviour for a test or a card. The wall-ownership fallback stays even with the spill on,
because a corner diagonal can still be seen from one side only.

## 2026-10-21 — More hunters, not a faster hunter; the hunt is a list

**Decision:** `GameConfig.HunterCount` / `FloorDefinition.HunterCount` put one hunter on floors
1–9, two on 10–15 and three on 16–20. `GameState` holds a private list of `Hunter` objects, each
with its own cell, step clock, stun, cached route and route index. `Enemies` publishes a reused
array of `HunterView` snapshots; `Enemy`, `PreviousEnemy` and `EnemyStepFraction` remain as the
first hunter. A catch throws only the culprit and stuns every hunter; another hunter's cell costs
four extra steps to enter.

**Reason:** The old curve turned one dial, the killer's step time, and the result read as "the same
chase, faster". A second hunter changes the shape of the floor: a wisp only distracts one of them,
two corridors have to be read at once, and the player's route has to account for a pincer. It is the
largest difficulty step the game can take without a new rule, and it is visible on screen.

**Consequences:** Every rule that touched `_enemy` now loops, including visibility, the aim cursor,
the catch, respawn and the route cache. `IsEnemyStunned` means *all* hunters are held, which is what
makes a catch a reprieve; the music follows the nearest hunter for the same reason. The HUD keeps
its four chips and the footer grows one pip per hunter, because a fifth chip does not fit a 640 px
window. Hunters may still share a cell when the maze leaves no route around, but the four-step
surcharge makes it rare and `HunterView` keeps the renderer honest when it happens.

## 2026-10-21 — Twenty pieces, not twenty takes: a style and a melody per floor

**Decision:** `MusicTheme` carries a `MusicStyle` (drone, lullaby, procession, chant, pulse, waltz,
nocturne, gamelan, machine, requiem), its own `Melody`, and a `StepsPerBar` (16, 12 for 3/4, 20 for
5/4). `MusicCatalog` holds twenty hand-written pieces; the shared five-motif cycle and `MotifFor`
are gone. `ScoreRenderer` dispatches the bed, bass, rhythm, lead timbre, bells and counterline on
the style, so the same stems hold a gong cycle, a march and a 5/4 clank.

**Reason:** The parameters varied but the piece did not: one progression shape, one motif per five
floors, the same pad and the same drums meant every floor was recognisably the same track at a
slightly different pitch. That is precisely what "completely different music on every floor" must
not mean, and no amount of retuning the numbers fixes a shared arrangement.

**Consequences:** The catalog is content, so a new floor now needs a melody and a style as well as
numbers, and the tests enforce it: twenty distinct melodies, at least eight styles, no two adjacent
floors sharing one, and three meters in use. Render cost rose to 170–400 ms per track (debug,
measured), still once per floor during the intro card. Long loops were shortened (30 s → 15 s) and
the rhythm and lead get a 2 ms fade at the loop point, because a staccato lead or a drum could end
mid-hit and step at the seam; every track now seams under 0.003.

## 2026-10-21 — A floor's music is arranged off the game thread, and adopted between blocks

**Decision:** `AudioDirector.PlayFloor` publishes a `Task<(ScoreStems, double)>` and returns; the
first `Update` of the floor adopts the finished score, or drops it if the render faulted. The mixer
adopts a published score at the top of a block (`AdoptPublishedScore`) and keeps its current fade
position (`_appliedGains`) across the swap. The device callback clears its buffer instead of
returning early with stale memory.

**Reason:** The playtest reported "the audio randomly glitches out into static". Arranging a floor
is 200–400 ms of synthesis that allocates several megabytes of stems; doing it on the game thread
stopped the loop dead on the intro card and then handed the garbage collector a large object graph
to collect *while the previous track was playing*, which is exactly how an audio callback misses a
deadline. The stack of defences is now: no render on the game thread, no ring-buffer state touched
from the game thread, no allocation in the mix path, and a buffer that is always written.

**Consequences:** A floor's track starts a fraction of a second after play begins (measured 0.36 s)
rather than at the exact intro frame, and the previous floor's music keeps playing until then —
silence between floors would read as a bug. The render task is fire-and-forget by design, so a
failed render is a silent floor rather than a crashed run, which is also why `AdoptRenderedScore`
checks `IsCompletedSuccessfully` instead of awaiting.

## 2026-10-21 — A wall is the loudest value after the lantern, and remembered cells are readable

**Decision:** The palette ladder is floor 0.18, wall 0.50, rim 0.68, contact shadow 0.05; the band
is 6 px of a 16 px cell; remembered cells are lit at 0.55 with a brighter tint (152/167/172) instead
of 0.40 with 104/118/122. `WallStrips.RoleFor(offset)` states the band's cross-section once, so all
four tile families share it.

**Reason:** "The walls blend in too much into the actual path — it's more like clicking randomly."
The measured wall body was already 1.6x the floor, which passed the old test while still reading as
a lit line: five of sixteen pixels, two of them near-black by design, over a floor that the lightmap
also dimmed to 62 %. Contrast that only holds in the atlas is not contrast the player sees. Raising
the drawn body to 2.5–8.3x and giving the unlit memory a readable level fixes the *thing the player
navigates by*, rather than decorating it.

**Consequences:** Walls now dominate the frame, so a theme that wants a soft look has to earn it
with moss and texture inside the band, not by lowering its value. `WallContrastTests` holds three
thresholds plus a thickness invariant (two bands must leave at least 4 px of corridor floor), so
thickening the band cannot quietly wall the maze in. Every atlas had to be regenerated; `--check`
compares the committed PNGs, so a palette change without a recompile is a red test.

## 2026-10-21 — Holding a direction walks; the step, not a timer, is the cadence

**Decision:** `StepRepeat.Next(held, stepping, now, stepSeconds)` sends the direction on the frame it
is first held, then again on the frame the previous step finishes — no fixed hold delay. When the
floor has no step time at all it falls back to `MinRepeatSeconds` (0.05 s). The frontend passes
`GameState.LastPlayerStepSeconds`.

**Reason:** The old gate waited 0.15 s *plus* the step, so holding produced a stutter and the
fastest way across a room was a tap rhythm. The delay was there to stop a slow tap leaving a queued
second tile behind, but the `stepping` flag already does that: a repeat is only sent while the key
is still down, and a key released during the step is never sent.

**Consequences:** Sprint and momentum are now felt immediately, because the repeat follows the step
that actually happened rather than the floor's base time. A tap that is held longer than one step is
a hold by definition, which is what every grid roguelike does. The instant-step fallback keeps a
debug floor from walking one cell per frame.

## 2026-10-21 — The death card is the run's score, drawn where the campaign is in scope

**Decision:** `PixelCards.Caught(font, floorNumber, floorCount, floorName)` draws "YOU MADE IT TO /
FLOOR 7 OF 20" and the floor's name, and `RaylibFrontend.DrawFrame` draws it on `Screen.RunLost`.
`PixelRenderer.Draw` no longer draws the caught card itself.

**Reason:** A lost run's only number is how far it got, and the renderer is handed one floor's
`GameState`, not the run. The campaign knows `FloorNumber` and `FloorCount`; keeping the card in the
renderer would have meant pushing a run summary into a class that is otherwise a view of one maze.
The other campaign cards (intro, cleared, complete) were already drawn by the frontend for the same
reason, so this makes the four screens consistent rather than adding a special case.

**Consequences:** Any future frontend that draws a lost run must supply those three values; the
compiler enforces it. The renderer's contract narrows to "world + HUD", which is what its tests and
the render harness already exercise. The card still sits over the dimmed world, so the last frame
shows where the player was standing when the killer reached them.

## 2026-10-21 — The maze grows with the descent, with one deliberate exception

**Decision:** The shipped floors' sizes step up across the run (15x15 for the first arc, 17, 19,
21 at the Gallery, 11x11 for The Warren, then 19, 21, 23 and 25 at the bottom), and
`FloorCatalogTests.Shipped_TheMazeGrowsAsTheRunDescends` pins that exactly one floor shrinks.

**Reason:** After the Long Gallery the catalogue fell back to 15x15 for ten floors, so the descent
felt like it stopped growing. Size is a difficulty dial that costs no new rule: the same hunter pace
covers more ground, and a bigger cave makes each key a longer commitment. The Warren stays tiny
because "a maze that fits in your arms" is the floor's gimmick; a monotonic ladder would have
deleted the one floor whose identity is its size.

**Consequences:** The last floors are 25x25 (371 open cells on The Deep's blob), which is about 2.8
times the floor area of the first: the late floors take longer and are harder to cross in one chase,
so they are a balance risk that only play can settle. The camera already follows the player and the
renderer reads `GameState.Maze`'s dimensions every frame, so no drawing code changed. The test fails
if a future floor is smaller than its predecessor without being named the Warren, which is the
property a content edit could silently break.

## 2026-10-21 — Floors have outlines, and the rock is closed before generation

**Decision:** `MazeShape` grows a `Rect`, `Blob` or `Ellipse` and keeps only its largest connected
piece; `MazeOptions.Shape` carries it, `MazeGenerator` applies it to a fresh `Grid` before any wall
moves, and `Grid.IsSolid` makes the rock part of the model (`RemoveWall` refuses a wall with rock on
either side). `GameConfig.Shape` is the rule, `FloorDefinition.Shape` the per-floor content override,
and `GameState` places the player, hunters, exit and keys only on floor cells.

**Reason:** "The mazes don't need to be perfect grids, they can be different shapes to be
interesting." A rectangle with different wall noise still feels like the same place, and the shape is
free difficulty-of-perception: the silhouette of a floor tells you what kind of floor it is before
you read a card. Shaping *before* generation is the only order that is safe — masking afterwards
carves passages into rock and can cut the cave in two.

**Consequences:** The maze's walkable area is now smaller than `Rows * Columns` on eight floors, so
every size check that used the rectangle had to become shape-aware (`GameState` validates the open
cell count and throws with the count in the message). `ShapedFloorTests` builds all twenty shipped
floors and proves the exit and every key are reachable, which is the invariant this feature could
silently break.

## 2026-10-21 — Five verbs replace barriers and wisps, and charges refill per floor

**Decision:** The player's tools are `PowerupKind.Blink`, `Veil`, `Drag`, `Lantern` and `Shove`,
each with its own `PowerupCharges` count, refilled when a floor is built. A charge is spent only
when the verb succeeds. `FloorDefinition.Powerups` and `GameConfig.Powerups` carry the counts, and
`PowerupCharges.ForFloor` is the teaching ladder (Blink on 1, Veil on 2, Drag on 4, Lantern on 6,
Shove on 8, then two step-ups). Barriers and wisps remain in `GameState` as rules and tests but
have no input and no HUD chip; `BeginBarrierPlacement`/`BeginLurePlacement` are unreachable from
the frontends.

**Reason:** The playtest never used barriers or wisps, and both share the same flaw: they ask the
player to place something and hope. Replacing them with one-press verbs makes each tool a reaction
to what is already on screen, and giving each its own refilling charges removes the hoarding that
made the old shared budgets feel precious in the wrong way. Keeping the old aiming commands would
have left two invisible modes in the input layer; keeping the old rules costs nothing because a
future floor can pre-place a seal.

**Consequences:** Every verb is a rule in `GameState`, so the frontends only translate keys and
draw counts. `CardEffect` and `RunModifiers` grew five charge deltas (clamped at zero) so a card can
trade against a verb; the four cards built on the retired verbs were rewritten rather than left in
the pool as dead content. The three floor gimmicks about seals and moths became powerup overrides.
The HUD's `SEALS`/`WISPS` chips are gone, so any screenshot of the old kit is out of date.

## 2026-10-21 — Descent conditions are chosen at floor 8 and floor 14, and the gate is a hard stop

**Decision:** A run draws two `DescentCondition`s, one at the floor-8 gate and one at floor 14,
from `DescentConditionCatalog.Offer(runSeed, gateFloor, active)` — a seeded shuffle that excludes
what the run already carries. `Campaign.ConditionOffer` opens at a cleared floor, `TakeCondition`
resolves it, and `TryAdvanceFloor` refuses to descend until `_chosenGates` contains the next
floor's number. Conditions ride `GameConfig.Conditions` into every later floor. Regrowth and
Fracture mutate the maze on timers; Fracture's close step is checked by `ConnectivityPreserved`
and a candidate that would cut off the exit, a key or a hunter is put back.

**Reason:** Later floors were the same floor with more hunters. Two permanent rules drawn from
eight make the bottom of a run different between runs without adding a new card every floor, and a
gate the player can see is more legible than a random modifier appearing on the intro card. The
hard stop is deliberate: checking the chosen set rather than the live offer means a frontend that
never read the offer cannot skip the choice, and there is no skip option because the run has to
change.

**Consequences:** The gates are keyed to floor numbers 8 and 14, so a synthetic campaign that uses
those numbers also has to resolve the gate (the whole-run walk in `CampaignTests` does). Mute adds
a heart to the floor and removes one rotating tool, which is the one condition that touches the
heart ceiling and the kit at once; the campaign's `HeartsOnNextFloor` had to include Mute's heart
or the next floor would refuse the carried hearts. Regrowth can only seal a dead end holding
nothing the floor needs, so it cannot break reachability; Fracture's tests assert the wall events
and a path to the exit rather than a particular wall.

## 2026-10-21 — The card catalog grows to twenty-three, and rares become a guarantee

**Decision:** Ten rule-backed cards joined the catalog (Featherfall, Greedy Grasp, Bloom, Blink
Ledger, Ghost Veil, Long Drag, Wide Light, Glass Cannon, Debt of Sight, Slow Pursuit), each backed
by a `GameConfig`/`GameState` hook rather than an `if` in the draft. `DraftFactory` now weights
tiers by floor band (commons shrink, rares grow) and guarantees a rare in the draft after floors 5,
10, 15 and 20 when one is still eligible.

**Reason:** The first card pass was legible but mostly small numbers, and a playtest said the cards
were not fun. The second wave gives the player decisions that change a whole floor's plan — a free
first catch, a revival, a catch that costs two hearts, keys that fly to you, a veil that lasts
longer — while keeping the "both halves in one sentence" rule. The floor-band weights and the
milestone rare keep the late draft exciting even after the pool has thinned, which a flat
common-rare weighting stopped doing by floor 15.

**Consequences:** The catalog is content, not code: a new card is a record plus, if it needs a new
rule, one validated config knob and one test of each half. The milestone swap runs after the normal
draw and only replaces the last offer when no rare was drawn, so it cannot duplicate an offer and
stays seeded. Glass Cannon's two-heart catch is the one card that makes the heart economy harsher
than the shipped three hearts assume; it is a rare on purpose. The HUD's five powerup slots were
part of the same pass: the old one-line readout was too small to read at the minimum window, and a
player who uses the verbs needs their charges at a glance.

## 2026-10-21 — The HUD's second row is the powerup row

**Decision:** `RaylibLayout.HudHeight` grows from 60 to 76 px. The top row keeps the floor identity
and the key/exit chips; the second row holds five 58x22 slots, one per powerup, each with its key,
its remaining charges and an accent glow while that verb is active. A spent slot is dim, not
hidden. The console renderer prints the same five counts in its HUD line.

**Reason:** The first version drew the charges as a one-pixel-scale grey line at the bottom right
of the status strip. At the minimum window it competed with the floor gimmick and the condition
line, and a playtest could not find it — the player used the verbs but had no idea what was left.
A row of keyed slots is the smallest form that answers "what do I have, and what is on" without a
menu.

**Consequences:** `HudHeight` is derived into the window size (`RaylibLayout.WindowSize`) and both
renderers use the constant, so the extra 16 px moves the floor down rather than clipping it. The
draft totals also report the new numeric cards (catch cost, veil/drag durations, lantern range);
the boolean ones are left to the card faces.

## 2026-10-21 — Slower start, faster hunters, stronger speed cards

**Decision:** The player's cadence is a constant `FloorCatalog.PlayerCadenceSeconds` (0.26 s a
cell) on every floor. Hunters start at 1.20 s on floor 1 and ramp to 0.22 s on floor 20, so the
player starts with a clear speed advantage and the last floor's base hunter is faster than the base
player. Speed cards were strengthened to close that gap: Light Feet is now Common at 35% faster,
Glass Cannon is 40%, and Momentum caps at 35%. The Tide and The Vault keep their non-speed
gimmicks.

**Reason:** The previous flat 0.18 s pace made the player faster than every hunter forever, so
speed cards were optional and the hunt never pressured the build. The playtest asked for a slower
opening and a real ramp: enemies should accelerate, the player should start ahead, and a reliable
speed card — not a pick every floor — should be enough to stay ahead once the hunt catches up.

**Consequences:** Floor 1 is a teaching floor, not a chase; by the last arc the base hunter can
outrun an uncarded player, and one Light Feet (0.169 s) beats the last floor's 0.22 s hunter. The
catalog test now asserts the start advantage, the late hunter advantage, and that one speed card
outruns the final base hunter. Sprint remains a temporary, stamina-limited exception.

## 2026-10-21 — A room is carved from rock, and a floor without rock reserves its pocket first

**Decision:** `SecretRoomCarver` plans a site *before* generation and carves it *after*. On a floor
whose shape left a 3x3 pocket of solid rock (measured: floors 2, 7, 8, 12, 15 and 20) the room is
cut from that rock. On a floor with no rock — every rectangular floor, and two of the ellipses —
nine cells are closed before Kruskal runs, so the maze is generated *around* the pocket and the
room is reopened afterwards. Both paths satisfy the same guarantee: the road is the only thing that
changes, no passage among the original cells is removed, every original cell stays reachable, and
the room adds exactly one door.

**Reason:** The brief said "carve into rock, so nothing is rerouted". Measurement showed that rule
produces rooms on six of twenty floors only — about 0.6 per run, with half of all runs seeing none,
instead of the 2.1 the spec's 1%-to-20% curve promises. Reserving the pocket keeps the no-reroute
promise (Kruskal never considered those cells, so no corridor was ever there) and gives every floor
the chance the spec asked for. A seeded run now measures 2.10 rooms per twenty floors.

**Consequences:** A floor that rolls a room has a different maze from the same seed without one —
but the room's roll draws from its own salted stream, so floors without a room are byte-identical to
before. `Grid.CarveCell` is internal and refuses to carve floor, which is what stops the carve from
ever deleting a passage. The tests assert "original passages unchanged, plus thirteen" (twelve
internal edges and one door) rather than "one new passage".

## 2026-10-21 — A safe room pauses the world, and cannot spend the last heart

**Decision:** `GameState.IsInSafeRoom` is true while the player stands on one of the room's nine
cells. `Tick` then advances only the player's own clock (step cooldown, sprint refill, turn
penalty), resolves a buffered step and returns before seals, wisps, conditions and hunters. Every
room's options are built so that an unpayable one is simply absent: the casino offers a stake up to
`hearts - 1`, and the shop, confessional and toll are not offered at one heart. Hearts are the only
currency.

**Reason:** The ask was explicit — hunters do not move inside, and no purchase may take the last
heart. Building the options from the run's state (rather than validating a purchase after the fact)
is what makes "a safe room can never be the thing that kills you" a property instead of a check.

**Consequences:** A room is a place you walk into and can walk through without a keypress, and
looking is free: closing the card leaves the room unspent, so a player can read the odds and come
back. A resolved room is spent for that floor. Every room is a single interaction — the shop sells
one card per visit — because a shop that can be returned to twice is a shop that can be drained.

## 2026-10-21 — The pressure gate does not ship yet

**Decision:** `FloorRule` ships four rules — the Loom's thread gates, the Ossuary's sleeper, the Ash
Garden's burning lantern and the Deep's long door — and deliberately not the Vault's pressure gate.

**Reason:** A portcullis held open only while the player stands on its plate cannot be walked
through by the player holding it open. As specified it is either a door that never helps or a trap
that seals the player in. It needs a reward that can be taken from a distance, which is exactly what
the rooms in §21 are only now able to provide; the pairing is a better design than either half.

**Consequences:** The Vault keeps its rectangle and its key count as its identity for now. The enum
has no `PressureGate` value, so nothing can ship it accidentally; the deferral is recorded in the
enum's own documentation.

## 2026-10-21 — The first three floors always hold a safe room

**Decision:** `SecretRoomCarver.ChancePercent` returns 100 for floors 1–3 and the floor's own number
in percent afterwards, unchanged. Measured over two hundred walked runs, a run now averages **4.55
rooms** instead of 2.10, and the opening floors always have one.

**Reason:** A playtest never found a safe room at all, and the numbers explain why: one percent per
floor gives the opening floors a 1–4% chance, so a player meets the mechanic only on the floors
where they already have the most to learn and the least attention to spare. A mechanic is not in the
game until it is met. Guaranteeing the teaching arc costs three rooms, keeps the late-run rarity, and
moves the average to roughly one room every four floors.

**Consequences:** `TheChance_IsTheFloorNumberPercent_AfterTheTeachingFloors` now measures floors 4–20
only, and a new test asserts every seed rolls a room on floors 1–3. The Cartographer's "room chance
of the next two" reads the same curve, so its promise stays true. The room's own salted stream is
untouched: a floor that rolls one still leaves every other floor's maze byte-identical.

## 2026-10-21 — A key in the code is a bloom on the floor

**Decision:** Every string the player reads says bloom: the HUD chip is `BLOOMS`, the console prints
`blooms`, the title promises "pick the blooms", and the cards and descent conditions say bloom. The
mechanic keeps its old names — `Keys`, `KeyCount`, `KeysCollected`, `GameEvent.KeyPicked`, the
`key-0..2` atlas ids — and `RunModifiers.Describe` is the one place that translates between them.

**Reason:** The art was already a flower, and a game about a garden that calls its light "keys" reads
as a placeholder. The rename is a text change; renaming the mechanic would touch twenty tests, the
campaign builder and the sprite manifest for nothing the player can see. Splitting the two words is
the cheap half of the work, and writing the split down is what stops the next reader from "fixing"
a `key` that is a flower.

**Consequences:** A test sweeps the player-facing strings (`Describe`, every card face, every
condition description) for "key", so the rename cannot rot back one sentence at a time. The atlas
ids stay `key-N` until the compiler is next run; the manifest is committed and `--check` ties the two
together.

## 2026-10-21 — The story is one authored page per floor, and the garden resets with the run

**Decision:** `StoryCatalog` holds exactly one page per shipped floor, in run order: a title and one
to three lines, plus a two-line Echo script and a one-line hint. `StoryPage` validates the count and
length; a test asserts the catalog and `FloorCatalog` line up one-to-one. `Campaign.CurrentPage` is
the page of the floor being played, `CurrentMood` adds one line when the run is on its last heart or
carrying a descent condition, and `GardenCount` is `BloomsPicked + FloorsCleared` — one mark per
bloom and one per page, reset with the run.

**Reason:** The floors are fixed and a run is one sitting, so the descent can be a book with fixed
chapters instead of a grammar that has to be recombined and tested for contradictions. Twenty pages
is under a thousand words and cannot contradict itself on a replay, because it never changes. A
garden that resets keeps the promise that there is no meta progression: the run is the artifact.

**Consequences:** Words are content, like `FloorCatalog`: re-tuning a floor cannot move its prose,
because the two are separate tables kept in step by a test. A page is built once and cached, so
`Page(n)` is the same object every time. The page is drawn on the existing cleared card rather than
a new screen; the only new surface is `PixelCards.Echo`.

## 2026-10-21 — The Echo is walked onto, freezes the floor, and comes once per floor

**Decision:** One Echo per shipped floor, placed from the cells the arch, the blooms and the hunters
did not take, requiring a detour of at least two steps from the shortest start-to-arch route and
preferring a dead end. Walking onto it sets `IsEchoOpen`; `Tick` returns immediately while that is
true, so hunters, seals, conditions and the player's own step clock all stop; the step clock is
zeroed on opening so the player is standing rather than mid-stride behind the card. `CloseEcho`
spends it, and stepping onto it again does nothing. `GameConfig.EchoEnabled` defaults to false and
`GameFactory` switches it on, so hand-built test floors keep their plain step-and-tick behaviour.

**Reason:** The powerup kit's lesson was that anything requiring the player to place or aim goes
unused, and an interaction behind a keypress is one half the players never find, so the interaction
is the step itself. The detour requirement is what keeps it from being a cutscene on the critical
path, and the dead-end preference is what makes its light worth following. Freezing everything is
the only honest way to put text on the screen during a chase: a paragraph read while a hunter closes
is a death the player did not choose.

**Consequences:** The Echo's cell is drawn with four pulsing corner dots in the floor's accent and
a pale bloom, and its pool of light is added to `_visible` one ring past the torch, so it can be seen
from the corridor before the detour is committed to. Because it is a landmark rather than a pickup,
it stays on its cell after it is read, greyed out. A floor too small to hold one is allowed to have
none — the property is "never on the critical path", not "always present" — but a test asserts all
twenty shipped floors have one.

## 2026-10-21 — Rooms roll at ten percent, and cards buy the rest (supersedes the fifty percent)

**Decision:** `SecretRoomCarver.RoomChancePercent` is 10 after the guaranteed teaching floor, and
`ChancePercent`/`Rolls`/`Plan` take a `bonusPercent` that the run's `RoomChancePercent` supplies.
Threshold Sense and Latchkey each add ten points (non-stackable, so a run tops out at 30%). A
`FloorDefinition.ForceRoom` flag skips the roll entirely, and only `Campaign` sets it, for the one
floor a House Favour promised.

**Reason:** Half the floors holding a room made the secret room part of the schedule rather than a
find, and it left no design space for a card to change the rate. Ten percent is a real find; the two
cards are the player's way to opt into more rooms, and a forced room is the only way a card can
promise one without the dice being able to break the promise.

**Consequences:** A twenty-floor run averages about three rooms instead of ten; the
`ARun_MeetsRoomsThroughoutTheDescent` range was retuned, and a factory test finds a floor whose
ten percent roll misses and proves a 100-point card turns it into a room. The side effect to watch
is that the Cartographer/Stall/Confessional are rarer, which is why the room cards state their rate
in the same sentence as their price.

## 2026-10-21 — A hunter only knows the player inside its aura

**Decision:** `GameState.RefreshHunterSenses` gives a hunter the player's exact cell only within
`HunterPerceptionSteps` (6, or a wider floor/card aura). Outside it the hunter's route targets
`KnownPlayer`, the cell it last saw them on; on arriving with the player gone it clears the memory
and takes one random open step (`WanderRoute`), preferring not to double back. `IsPlayerSensed`
exposes the rule. The lamp and a wisp remain deliberate, visible targets at any distance.

**Reason:** Before this, every hunter ran a perfect shortest path to the player from any distance,
so the aura was only a rendering flourish: the player could see nothing and the hunt still came
straight through the rock. The user asked for the aura to be the contract, and a remembered cell
plus a search is the smallest model that keeps hunters dangerous without letting them cheat.

**Consequences:** `HunterPerceptionSteps` is a floor of six even when a floor's `EnemyAuraDistance`
is lower, because several rule tests and the stalking floors set the aura to zero for visibility
reasons; the shipped default is also six, so the two read as one number. Wander steps consume the
injected random, so a seeded run searches the same way twice. A hunter that is out of sight now
takes longer to arrive, which is the point; the campaign is a little more forgiving and needs a
play-test before the next balance pass.

## 2026-10-21 — Healing cards pay two hearts, and the speed cards were halved

**Decision:** Every `HealNow` and `HealOnFloorClear` card restores 2 (Blood Pact, Second Wind,
Trophy Hunter). Glass Cannon is 20% faster (was 40%), Light Feet 17.5% (was 35%) and Momentum's
ramp/cap are 0.03/0.175 (was 0.06/0.35), with their prices unchanged.

**Reason:** Skipping a draft already heals one heart for free, so a card that healed one and carried
a drawback was strictly worse than not picking a card. On the other side, the three player speed
cards were the strongest numbers in the catalog; halving the buff keeps the trade real without
deleting the cards.

**Consequences:** The faces say the new numbers (`nearly 18 percent`, because a decimal point in a
face breaks the one-sentence test), and the RunModifiers/CampaignDraft tests pin the totals. A
future tiering pass should look at Light Feet first: it is still a 17.5% speed gain for a heart slot.

## 2026-10-21 — The shop shows the card's face, shove reads four neighbours, and the lantern is stronger

**Decision:** `RoomOption` carries an optional `Preview`, and a Stall fills it with the card's
`FaceText`; both frontends draw it under the selected row. `TryShove` checks the faced direction
first and then the other three neighbours, pushing the hunter directly away from the player.
`LanternSeconds` is 8 (was 5), the default `LanternRange` is 5 (was 4), and a throw stops short of a
hunter instead of being refused when the furthest reachable cell is occupied. The Listener skips
`CreditListenerStep` while `IsInSafeRoom`.

**Reason:** A shop that asks for a heart for a name the player has not memorised is a hidden
choice; the face is the honest version. Shove only working on the faced cell made the melee answer
unusable when a hunter arrived from the side, which is when it exists. Five seconds and four cells
made the lantern a flicker rather than a tool, and blocking the whole throw on a hunter at the end
of the corridor wasted the charge. The Listener inside the safe room broke the room's one promise.

**Consequences:** The room card layout gained a preview band under the list (wrapped at one pixel
per glyph, capped to the space above the footer), and the console prints the same text indented.
Shove still prefers the faced hunter when two are adjacent, so a deliberate aim is not lost. The
lantern test that asserted a 5 s return now asserts 8 s, and the throw test pins the stop-short cell.

## 2026-10-21 — A hunter will not enter the safe room

**Decision:** `GameState.CostOfEntering` prices a room cell at 500 normal steps, and
`WanderRoute` skips the room's nine cells. A hunter may still stand on the door cell outside the
room, but never on the room's own floor.

**Reason:** The room's promise is that nothing outside it moves while the player is inside, and
that promise is empty if a hunter can already be standing in the doorway when the player walks in.
A hallway-style test found exactly that: a searcher that had lost the player wandered through the
one door and caught the player on the threshold. The room is a dead end behind a single door, so no
route to the player ever needs to cross it; a large finite price keeps it out of every chase without
making the cell genuinely impassable, which would risk a route the pathfinder cannot build.

**Consequences:** The room's only door is the choke point the hunt can still use, so entering with a
hunter on the threshold costs a heart rather than trapping the player. `TheHunt_NeverWandersIntoTheRoom`
sweeps 20 s of chase around the door and asserts the rule; the Listener's safe-room test would fail
without it, because the old wander could put a hunter on the doorway before the player stepped in.

## 2026-10-22 — Escape pauses, the draft can be rerolled, catches deepen, and the flowers answer a catch

**Decision:** Escape opens a pause card (`CONTINUE` / `QUIT RUN`) instead of leaving the run; only
the title's `LEAVE` closes the window. `GameConfig.ForFloor` adds one heart to `CatchCost` for every
five floors below the first. `Campaign.TryRerollDraft` buys a fresh seeded table for
`1 + rerolls` hearts, never spending the last one, and the ladder dies with the draft. The title
gains a `TUTORIAL` entry backed by Core's `Tutorial`/`TutorialStep`. `CardTier` gains `SuperRare`
for `House Favour` alone. `Ashen Bloom` grants `BloomGlowRadius` 1 and the wave around it adds
thirteen more trades. The casino's card draws a turning wheel.

**Reason:** Escape was the game's quit key while it was also the player's "stop for a second" key,
so one slip ended a run that could be half an hour old; a pause is the behaviour players already
expect, and the run should only end from a menu the player chose. A flat one-heart catch made the
late floors easier than the early ones once a build came online, so the price now climbs with the
descent while cards (and their surcharges) stay the build lever. A draft of three is a choice only
if it is sometimes the wrong three: hearts are already the run's currency and rerolling spends them
on a second look rather than adding a new resource. Nothing in the game explained itself, and the
key list was only in the README; a page-by-page guide in Core keeps both frontends teaching the
same keys. The casino promise was the rarest trade in the catalog and read like a common card, and
the requested flower reveal is a rule the player can plan around — a catch already costs a heart,
so it should also tell them where the nearest bloom is.

**Consequences:** The window freezes by simply not calling `Tick` on the pause screen; the console
restarts its stopwatch on resume, or the pause would be charged to the hunters as walking time.
`TryRerollDraft` draws with a salt mixed from the reroll ordinal, so a replay reproduces the ladder.
The bloom glow is applied in `RecomputeVisibility`, which is why it reveals flowers through walls
without revealing the corridor and why a renderer only has to brighten a bloom the rules already
made visible; `Ashen Bloom` stacks to three copies, one step each. `Tutorial` is content, so it is
covered by its own tests rather than by the rules' tests, and `CardCatalogTests` pins that
`House Favour` is the only `SuperRare`.

## 2026-10-22 — The catalog is readable, the tutorial is walked, and the fourth wave is priced in the catalog's units

**Decision:** The card catalog gets a screen (`PixelCards.Codex`, `Screen.Codex`) reachable from the
title's `CARDS` entry and from the pause menu, and a terminal equivalent (`PrintCodex`) printed from
the title or a pause. The tutorial gains a practice floor (`PracticeTutorial` in Core) that lays the
player, arch, blooms and hunter out in reading order through a private non-shuffling `IRandom`, puts
the hunter on a ten-minute clock, and derives four steps from the floor itself. Ashen Bloom's price
falls from two killer-aura steps to one; Coiled Spring pays `EnemyStepScale` 0.94 instead of a heart
on the catch; Second Skin gains `InvulnerabilitySeconds` 1; Patient Hunter pays a step of torch
instead of drag's hangover. `ConsoleRenderer` lists only the verbs with charges left.

**Reason:** A card is only a choice if the player can read it before it is offered, and a draft of
three was the only place the game explained its own catalog — a rare could be met for the first time
on the card that takes a heart for it. The tutorial explained the loop but never asked for it, so
"pick the blooms, open the arch" stayed a sentence; a corridor where nothing can reach the player is
the cheapest place to spend a first mistake. The balance changes come from comparing each price with
the currency the rest of the catalog charges in: a price paid only when the player chooses to use an
optional verb (drag's hangover) is not a price, a whole heart is too much for a sprint burst once a
catch costs four, and a card that is strictly worse than another card in its own tier is not a
trade at all. The glow's two-step sight price was the harshest in the catalog for a two-second,
one-step reveal.

**Consequences:** The codex draws from `CardCatalog.All`, so it cannot fall behind the catalog, and it
adds no rules — the pause still freezes the floor while it is open. `PracticeTutorial` is the one
floor in the game that is not generated; that is why its random source does not shuffle, and why it
is documented as such. Its steps are derived, not scripted, so a player who runs ahead is never told
to do what they have already done. The re-prices are pinned by a test that names the reasoning, so a
future edit has to argue with it rather than drift.

## 2026-10-22 — The bloom glow lights every flower, one tile each

**Decision:** `Ashen Bloom`'s glow lights **every bloom on the floor**, not the blooms near the
player: a catch makes each flower visible for two seconds on its own tile, and nothing else. The
knob is renamed `GameConfig.BloomGlowReach` (and `CardEffect.BloomGlowReach`), where 0 is dark,
1 is every bloom's own tile, and each step above one lights that many cells around every glowing
bloom. The card still grants reach 1 and still stacks to three copies.

**Reason:** "One tile" was being read as one tile of radius around the player, which made the card
a small puddle of sight near someone who had just been thrown clear of the hunter. The reveal is
meant to be a scent across the whole floor: after a catch you learn where the flowers *are*, which
is a decision you can act on, while the corridor they sit in stays dark so the map is not given
away. That also makes the upgrade meaningful in the same direction: more reach means a halo around
each flower, not more flowers in the list, which had already included all of them.

**Consequences:** The reveal runs per bloom (`DistancesFrom(maze, bloom, reach - 1)`), so a floor
with many flowers and two copies costs a handful of small searches in `RecomputeVisibility`, never a
full map reveal. A revealed flower is an ordinary visible cell to the renderer, so it enters the
explored memory like anything else the torch has touched and stays marked after the glow fades,
which is the behaviour the point of the card wants. `GameStateBloomGlowTests` now asserts every
bloom is lit, that the empty cell beside a lit flower is not, and that a second copy is the one that
lights the neighbours.

## 2026-10-22 — The practice floor is a room, because the window draws floors through a viewport

**Decision:** `PracticeTutorial` builds a 9x9 chamber with a lantern radius of six, instead of the
first pass's 1x6 corridor. The lesson itself is unchanged: the player, the arch, the two blooms and
the hunter are still taken in reading order, still along the top row, and the four steps are still
derived from the floor.

**Reason:** The window does not draw a floor edge to edge. It draws a viewport sized by the lantern
(`PixelLayout.ViewCells`, 13 cells at radius six) and centres a smaller floor inside it, so a
one-row lesson painted six cells of a 169-cell canvas: a thin lit line inside a black frame, which
reads as a broken display rather than as a place. The practice floor is the one floor that is
content rather than generation, so its shape is the tutorial's problem to get right: it has to fill
the frame the way a real floor does before it can teach anything.

**Consequences:** The chamber is a plain open room with the grid's own boundary as its rock, built by
removing the interior walls of a fresh `Grid` — no generator, no shape, no placement roll. Its first
frame lights twenty-eight cells instead of six, so the arch and both blooms are visible from the
first tap. `PracticeTutorialTests.ThePracticeFloor_OpensOnALitRoomRatherThanABlankCanvas` pins both
halves: the room is nine cells across and covers more than half the viewport, and at least twenty
cells are lit before the player moves.
