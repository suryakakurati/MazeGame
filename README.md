# MazeGame — COP3503H Group Project (Group 5)

A horror-themed maze game written in C# on .NET 8. The player wakes up in a
randomly generated maze, must collect the objectives, and reach the exit before the
thing that hunts them catches up.

This is a course project, so the graded data structures and algorithms
(Disjoint Set / Union-Find for maze generation, Dijkstra with a hand-written binary
min-heap for the enemy) are implemented from scratch in this repository. No
graph/pathfinding libraries are used.

> **Status (2026-10-20):** Course Phases 1–6 complete, and roguelike **Phases 1–4, 7 and the
> content of Phase 8** are in — the
> game is **playable in a Raylib window** as a **twenty-floor campaign with a card draft and timed
> steps**. Each floor is a braided maze **drawn from the compiled pixel-art atlases** on a virtual
> canvas at an integer scale, with fog of war, a per-cell lantern lightmap and a killer that carries
> its own light: pick the blooms, open the arch, and descend.
> **You start with three hearts**: a catch costs one on floors 1–5 and one more for every five
> floors below, throws the killer to a distant dead end and
> stuns it, and **hearts carry between floors and have no ceiling**, so healing past three is real
> progress; only running out ends the run. Walking
> is a cadence (a constant 0.26 s per cell; enemies start at 1.20 s and ramp to 0.22 s, so the
> player starts ahead and has to draft speed to stay ahead; only cards and sprint make you faster),
> one press is buffered, **Shift sprints** on a stamina bar. The explorer snaps to its tile (one
> press, one tile) while the killer slides between cells. The hunt itself escalates: **one hunter
> for nine floors, two from floor 10, three from floor 16**, each with its own route and clock, and
> by the bottom the base hunter is quicker than the base player — speed cards close the gap. All twenty floors
> differ in **shape, weather, light and music**, not only in
> colour: twelve floor motifs, ten wall motifs, eleven signature objects, twelve kinds of drifting
> weather, a per-floor HUD and card emblem, and a gimmick (a tide, three locks, a closed lantern, a killer
> without its glow). Every floor refills **five one-key powerups** — Blink, Veil, Drag, Lantern and
> Shove — whose charges are shown as five slots in the HUD, and at floors 8 and 14 the run stops at
> a gate to take one of two **descent conditions** that bends the rest of the descent. Each floor
> has an intro card, a floor-cleared card, **a draft of three cards between floors — take one or
> skip for a heart** — from a catalog of thirty-nine, including four movement cards, four powerup
> cards, ten second-wave cards that change catches, blooms, durations and revivals, three room
> cards that raise the secret-room roll or buy one promised casino, and a fourth wave led by
> **Ashen Bloom** (a catch lights every bloom on the floor for two seconds, each flower shown only
> on its own tile; every extra copy lights a step around each flower). Rarity is on every card's face, and **House Favour** is the catalog's one
> **super-rare**: the card that promises a casino. The shop shows every card's face before it takes a
> heart, and a reroll buys a fresh table for hearts (1, then 2, and so on, per floor).
> **The title screen also offers a Tutorial**: twelve pages that walk through walking, the blooms,
> the arch, the hunters, hearts and every ability, key by key, before the first descent, then hand
> over to a **practice floor** — a nine-by-nine chamber whose hunter never moves, where the player
> walks, takes two blooms and opens the arch with nothing at stake. ESC now opens a **pause menu**
> (continue, the cards, save and quit, or abandon the run) instead of abandoning it, and a **card catalog** on the
> title and in that menu lists every card with its rarity and its trade.
> A run summary follows (835 unit tests; the sprite compiler has produced 2,264 validated pixel-art tiles across
> twenty themes, and the game draws the floor from them).
> **Late floors have a rule, not just a dial**: floor 14 keeps one hunter asleep until you come
> close, 18 carries you between two thread gates, 19's lantern burns down a step every forty
> seconds (each bloom restores it) and 20's arch needs five continuous seconds of standing.
> **A floor can hide a safe room** — a 3x3 chamber with one door, drawn in warm cream stone no floor
> uses so it reads through the fog. The first floor always has one, so the mechanic is taught rather
> than hoped for; every floor after it rolls a ten percent chance, and a run averages about three,
> with room cards raising the rate. Its entrance is lit within three steps, so a door in the rock is
> a find rather than a guess. Standing inside pauses the hunt, and the hunt will not follow you in, while
> the room sells a hearts-and-cards trade for one visit. Cards may now stack up to three copies, and the
> Confessional and the Mirror are the only ways to take one back.
> **Two runs are not the same descent.** Every floor hides an **altar** off the shortest route,
> stocked with one of twenty-six permanent **relics** — a heart on every clear, a faster stride, a
> third bloom on every floor, a resurrection, a magnet for flowers, an extra blink, a longer
> sprint — that the run keeps from the moment it is touched. Touching one wakes anything asleep,
> so the reward is paid for on the walk back, and the **relic card** freezes the floor to say what
> you just gained. From the fourth floor down, each floor also wears a **face** rolled from the
> run's seed: **quiet** (one hunter fewer, a step more light), **greedy** (one more bloom, and one
> more hunter looking for it), **haunted** (a step less light, and the walls grow more ways
> through) or **starved** (one bloom fewer, and a hungrier hunt). The first three floors never
> change, so the lesson is the same every time; everything below them is a different campaign from
> one run to the next. **RUN DETAILS** in the pause menu lists every card and relic the run is
> carrying, with the floor, hearts, blooms and garden, so the pile can be read as a build.
> **It is a story as well as a descent.** The keys were always flowers, so they are called
> **blooms**: the HUD says `BLOOMS`, and the arch is a knot of roots that wants its light back.
> Every floor hides one **Echo** — a pale standing stone in a dead end, off the shortest route,
> with its own faint ring of cold light. Walking onto it freezes the floor, restores a heart and
> plays three lines of the gardener's voice, the last of which teaches that floor's rule. Each cleared floor turns a page of the
> **herbarium** (a title and up to three lines), and a strip of blooms under the page grows with the
> run: one mark for every bloom picked, one for every page turned. The garden is the run's only
> score, it is shown once, and nothing about it survives the run.
> **It has a score and effects, and no audio files**: every sound is synthesised from code — 22
> one-shots and twenty floor pieces, each with its own melody, key, meter and style, mixed as stems
> so the chase arrives on the next bar. The styles are different kinds of music (drone, lullaby,
> procession, chant, pulse, waltz, nocturne, gamelan, machine, requiem), and some floors are in
> 3/4 or 5/4, so no two floors sound like the same track at a new pitch. The phased plan to a full roguelike is in [`FUTURE.md`](FUTURE.md);
> current state in [`PROGRESS.md`](PROGRESS.md).

## Requirements

- .NET 8 SDK (any 8.0.x)
- macOS, Linux, or Windows
- A display for the game window; the `--console` version needs only a terminal
- A sound card for the music and effects; without one the game is silent, never broken (the
  mixer and the banks are still tested, and `--console` never opens an audio device)

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
Every screen is drawn in the compiled pixel font: the title, the floor intro, the cleared and
run-summary cards (each with the floor's emblem and palette strip), the draft, and the HUD and
footer inside the window. Nothing falls back to the system typeface, so the type is as crisp as
the tiles under it at any window size.

`dotnet run --project src/MazeGame.UI` opens the title screen; `--console` plays the same
campaign in the terminal, and the console view is also used automatically when no window can be
opened (over SSH, for example). A run is twenty floors, each with its own theme and gimmick, a
five-verb powerup kit with charges that refill each floor, a vision radius of 3 cells and a key
count per floor; the killer starts at 0.6 s per step and speeds up each floor. All of that is
tunable in `GameConfig` and `FloorCatalog`. Every run generates new caves (`Seed = null`); set a
seed to replay the exact same campaign.

The first restore downloads the Raylib native library for your platform (see
`ARCHITECTURE.md` for the exact package version).

## Controls

| Key | Action |
| --- | --- |
| W / S or arrows (title) | Select `DESCEND`, `CONTINUE`, `TUTORIAL`, `CARDS` or `LEAVE` |
| Enter or Space (title) | Confirm the highlighted entry |
| R (title) | Continue the saved run, when there is one |
| Esc (tutorial) | Back to the title |
| Enter / A / D / arrows (tutorial) | Turn the page |
| P (tutorial) | Practise the loop on the teaching floor |
| C (title or pause) | Open the card catalog: every card, its rarity and its trade |
| W A S D / arrow keys | Walk: a tap is one tile, a hold keeps walking at the cadence |
| Shift (hold) | Sprint, while the stamina bar lasts |
| Space | Blink through the wall you face (the breach stays open) |
| V | Veil: stand still for half a second to go unseen for 3 seconds |
| T | Drag: slow the hunt to 40% for 3 seconds |
| L | Lantern: throw your light up to 5 cells ahead for 8 seconds |
| F | Shove: push any adjacent hunter two cells away, whichever way you face |
| E (in a safe room) | Open the room's card; A/D choose, Enter takes, Esc leaves |
| A / D, arrows (draft) | Move between the cards |
| Enter, Space or 1–4 (draft) | Take the highlighted card |
| S (draft) | Skip: take a heart instead |
| R (draft) | Reroll the table: 1 heart, then 2, then 3, resetting each floor |
| Esc | Back one step: pause in a floor, resume from the pause, close any card; it never ends a run or the session |
| Enter (pause) | Take the highlighted entry: continue, run details, the cards, save and quit to the title, or abandon the run |
| W/S (run details) | Scroll through every card and relic the run is carrying |
| R or Enter (after a run) | Descend again, on a new maze |
| Q (title) | Quit |

## How to play

The game opens on a title screen over the cave: `BIOLUMINESCENT / DEPTHS`, with `DESCEND`,
`CONTINUE` (only while a run is saved), `TUTORIAL`, `CARDS` and `LEAVE`. The garden line under the
rule is the running tally of every bloom your finished runs have grown. The tutorial is a guided read rather than a lecture: one idea per
page, the exact keys for it, and a page count so the player knows how much explaining is left; its
last page (or `P` on any page) opens the practice floor, where the same loop is walked once on a
nine-by-nine chamber nothing can reach them on. `CARDS` is the whole catalog with its rarities, and the pause
menu opens it mid-run; `RUN DETAILS` beside it lists what this run actually holds, cards and relics
together. Starting a run (or pressing R after a run ends) builds a brand-new
campaign: floor 1, three hearts, every budget full. A floor intro card names the floor (The Shallows, Rootbound,
Drowned Halls) before play begins.

A run is twenty floors. Clearing a floor's arch shows a floor-cleared card; Enter opens the
**draft**: three cards, each naming its own price in the same sentence as its benefit ("Vision
+2, but the killer's light reaches 3 steps further"). Take one with Enter or its number, or skip
with S for one heart, or press R to reroll the table for hearts: the first reroll on a floor costs
one, the second two, and so on, and the ladder resets on the next floor. Whatever you take applies
to every floor from then on, and the draft screen shows the run's totals underneath the title.
Rares grow more likely as you descend, and every
fifth floor's draft guarantees one, so the late run is where the build-defining cards live. Then
the next floor begins with the hearts you have left. At floors 8 and 14 the run stops at a **gate**: two descent conditions, one taken for the
rest of the run, so the late floors are not the same floor with more hunters. The twentieth arch
wins the run and shows a summary; there is no draft after it. Losing all three hearts ends the
run whichever floor you are on.

**Every floor holds something that is not the objective.** Besides its blooms and its arch, each
floor places one **curiosity** off the shortest route — a drowned bell, a sealed reliquary or a
gardener's shrine — and says so in the HUD. A bell pays a heart and wakes every hunter; a reliquary
costs a heart (never the last one) for two blooms and a charge of some verb; a shrine gives a step
of sight for the rest of the floor and a bloom. The kind and the place come from the run's seed,
so a replay finds the same things, and a different seed finds different ones. Which curiosity a
floor holds, which rooms its ten percent rolls, which cards the draft offers and what the maze
does with its walls are what make two runs down the same twenty floors different journeys.

**The run is saved as you descend.** Every floor you reach writes the run — its seed, floor,
hearts, cards, conditions and garden — to `%APPDATA%/MazeGame` (on macOS,
`~/Library/Application Support/MazeGame`), and `CONTINUE` on the title rebuilds that floor from the
seed and drops you at its start. A crash costs at most the floor you were on. `SAVE & QUIT` on the
pause card writes the run there and then — with the hearts and blooms earned on the floor — and
returns to the title with `CONTINUE` waiting; `ABANDON RUN` clears the save on purpose. Finishing
or losing a run clears it too and adds the run's blooms to the garden, which is kept in
`garden.json` next to it. Set `MAZEGAME_SAVE_DIR` to keep those files somewhere else.

Two floors are built around a rule rather than a dial. **The Drowned Halls (floor 3)** run on a
tide: twelve seconds of dry hall, two of warning, then eight seconds of water that takes a step of
the torch and hurries the hunt by a third, over and over. **The Gilded Cage (floor 6)** bars its
arch with three gilded locks placed near, middle and far from where you start, so the floor is a
route to plan rather than a fifth bloom to find. The Hush (floor 7) is still the floor where the
torch reaches one step and nothing else matters.

Not every floor is a rectangle: eight of the twenty are carved as a blob or an oval, with the cells
outside the outline left as solid rock, so the cave itself changes shape as you descend. Walls are
drawn as a thick band of rock with a lit rim and a black foot against a darker floor, and cells you
have already seen stay readable once the lantern has moved on.

The window is an underground descent where each floor is a different place. The Shallows are
wet boulders and drifting motes; Rootbound is matted root and spore; Drowned Halls are laid
flagstones under falling water; The Salt Choir is tessellated stone, ribbed walls and dust; The
Ember Vault is a grate over embers. Later floors add bone, crystal, silk, riveted plate, mire,
ripples, ash and rain, and each carries one large signature object — a pool, a nest, a rune, a
stopped clock, a mirror, a bone pile, a beating heart — that is scattered a few times per maze.
The HUD names the floor and its gimmick, and the cards carry its emblem and palette.

You are a small hooded figure carrying a lantern; the hunters are tall, spindly silhouettes with
pink rim light and two pale eyes (two of them on floor 10, three from floor 16); blooms are magenta beacons — a pool of light on the floor, a shaft
rising out of the cell, a pulsing ring and glowing fungus at the core, so they read at the edge of
the torch; the exit is a stone arch (cyan once open).
The terminal view uses `@` for you, `K` for the killer, `k` for a bloom, `e` for the Echo and `E` for
the exit.

Walking is timed rather than instant: a step takes about a tenth of a second, a press during that
window is remembered for the next step, and holding a direction keeps walking at that cadence — the
repeat starts on the frame the previous step ends, so a hold is one continuous walk rather than a
tap rhythm.
Hold Shift to sprint for 40% more speed while the stamina bar lasts (it only drains while you are
actually moving, so holding Shift on the spot costs nothing); when it runs dry you have to wait for
a quarter of it to come back. Sprint is for breaking line of sight, not for going fast.

1. Collect all three blooms — the exit stays `LOCKED` until the last one is picked up.
2. Reach the exit. You start with three hearts, shown beside the control hints at the bottom:
   a catch costs one heart on floors 1–5 and one more for every five floors below, throws the
   killer to a far dead end, stuns it for 1.5 seconds and
   gives you 1.5 seconds of grace, and **keeps your keys**. Hearts have no ceiling:
   an Echo, a room or a card can push you past three, so only the catch that empties the last
   heart ends the run. The screen washes red when you lose a heart, and the last heart pulses.
3. Each floor refills five powerups, one key each, and the HUD shows how many charges are
   left. **Space** blinks through the wall you face and leaves the breach open, so you make a
   shortcut the hunters can use too. **V** veils you after half a second of standing still, for
   three seconds of being unseen, but any step breaks it. **T** drags the hunt down to 40% for
   three seconds, then leaves it 10% faster for five. **L** throws your light up to five cells
   ahead for eight seconds and the hunters walk toward it while your own cell goes dark. **F**
   shoves any hunter next to you two cells away, whichever way you face, and stuns it; every
   other hunter is given its next step at once for the noise.
4. A powerup is spent only when it succeeds, so blinking into rock or shoving empty air costs
   nothing. Using the right verb at the right moment is the skill; hoarding charges just wastes
   them, because every floor refills the kit.

The maze is hidden by fog of war: you see your own cell and 3 steps down the corridors from
it, and walls block the light. Cells you have already seen stay dimly drawn so you can
remember the layout, but the killer, blooms and the arch are only shown while you can actually see
them.

The killer carries its own violet light. Once it is within 6 walking steps of you, the cells
around it light up — even when the corridor between you is pitch black — so you get a warning
glow before it arrives. The maze is also braided: about 55% of its dead ends are opened up, so
there are roughly 19 dead ends instead of 57 on a 15x15 map, and eight extra loops run across
it. That is what stops a chase from ending in a corner and gives a sprint somewhere to go.

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

The killer moves toward the player along the shortest path. Every passable cell costs `1`, so
the route is the shortest walk through the corridors the player can see. Blink leaves a real
breach in the maze, so a shortcut you make is one the hunters can use too.

A hunter only knows the player's exact cell inside its aura (six steps); outside it the route
goes to the cell it last saw them on, and once it arrives with the player gone it wanders one
open step at a time instead of walking a perfect path through rock it cannot see through.
`GameState.IsPlayerSensed` is the flag; the aura is a two-way rule.

Dijkstra runs on a hand-written binary min-heap: each cell is extracted once in order
of increasing distance, and neighbours are relaxed through cell walls only, so a path
can never cut through a wall. The path is recomputed whenever the player moves or the
obstacles change.

The pathfinder takes the weight of entering a cell as a function, so it knows nothing about
the maze's contents; the game passes `1` for every cell:

```csharp
var pathfinder = new DijkstraPathfinder();
IReadOnlyList<Cell> route = pathfinder.FindPath(grid, playerCell, exitCell, _ => 1);
// route[0] == playerCell, route[^1] == exitCell, adjacent steps never cross a wall
```

### The player's skill mechanic (five verbs)

The player's tools are not a shared resource: each verb has its own charges, refilled when a
floor starts, and a charge is spent only when the verb succeeds. `PowerupCharges.ForFloor`
teaches one verb at a time (Blink on floor 1, Veil on floor 2, Drag on floor 4, Lantern on floor
6, Shove on floor 8) and then deepens the counts.

- **Blink** removes the wall the player faces and steps one cell through it. The breach is a
  real change to the maze for the rest of the floor, so it is both a shortcut and a hole the
  hunters can use.
- **Veil** channels for 0.6 s while the player stands still, then hides them for 3 s. Hunters
  keep walking to the last cell they saw; any step, or being caught, breaks it.
- **Drag** scales the hunters' clock to 40% for 3 s, then to 110% for 5 s. Both the individual
  and Pack clocks read the same multiplier, so they cannot disagree about what time it is.
- **Lantern** throws the light source up to five cells down a corridor and burns for 8 s. While
  it burns, the player's own cell is dark and `LightOrigin` is the lantern, so the hunters follow
  the light rather than the player. The throw stops short of a hunter rather than landing on one.
- **Shove** moves any adjacent hunter two cells away from the player (stopping at a wall, rock or
  another hunter) and stuns it for 2 s; every other hunter immediately gets its next step for the
  noise. Facing is a preference, not a requirement.

### Presentation (fog, killer light, bioluminescent cave)

What the player may see is a rule, so `GameState.IsVisible(cell)` answers it: a
breadth-first walk out from the player through open passages, cut off at `VisionRadius`.
Walls therefore block sight, and the radius is a lantern range down corridors rather than a
circle drawn through rock.

The killer has a second light. Once it is within `EnemyAuraDistance` (6) steps, the cells
within `EnemyAuraRadius` (2) of it become visible too, and `GameState.EnemyGlow` marks which
ones were lit that way so the renderer can glow them pink. Its own light ignores the wall
between you, which is exactly why it can be seen around a corner.

The world is drawn from the committed pixel-art atlases: one 16x16 tile per cell, thin wall faces
along closed edges, and a 16 px-per-cell virtual canvas blitted to the window at an integer scale.
Each floor's atlas is drawn from its own motif (boulders, flagstones, roots, ripples, drift,
mosaic, bones, shards, web, plates, mire, grate) and wall grammar (moss rock, cut block, woven
root, ribbed, stacked plate, crystal, silk, bone, grate, frost), so the surface announces the place
before the palette does. A per-cell lightmap carries the lantern falloff, remembered cells and the
killer's cold tint; a weather layer of 30 tiny pixel particles (motes, bubbles, spores, embers,
drips, dust, moths, petals, ash, rain, static, fireflies) drifts above it, and the light itself is
warmed or cooled per floor. Characters have silhouettes rather than marker dots — a hooded
explorer with a warm lantern, a spindly killer with a pink rim and eyes. The HUD is a themed strip
with the floor's emblem, number, name and gimmick, compact status chips, a pink warning when the
killer is close, and the heart row in the footer. Walls are drawn in two passes (all rock, then all
lit edges) so a shared wall keeps the rim on both sides.

The renderers only decide how to express all this; they never decide what may be seen. Each keeps
its own set of explored cells, so the map does not blink in and out as you walk, and
`ConsoleRenderer` follows the same visibility rule in ASCII, which keeps the terminal fallback a
fair way to play rather than a map cheat.

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
phases — hearts, a campaign, a card draft and smooth timed movement (all four done), followed by a
generated pixel-art renderer, synthesised audio, twenty themes (done), and polish.

## Contributing

Read [`HANDOFF.md`](HANDOFF.md) first, then [`CONTRIBUTING.md`](CONTRIBUTING.md).
Every change must leave `dotnet build` warning-free and `dotnet test` green.

## Team

COP3503H — Group 5. Contributors are listed in the course submission; add your name
here when you make your first commit.
