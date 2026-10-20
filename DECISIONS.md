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
