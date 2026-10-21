# ARCHITECTURE

This document explains which class does what, how the projects fit together,
and which third-party packages are in use. Update it whenever the structure changes.

## Layering

The game is split so that **all rules live in `MazeGame.Core`** and every other
project only interprets or presents them:

- `MazeGame.Core` — pure C# logic, no rendering, no input, no UI framework. It never
  calls `System.Random` or `DateTime.Now` directly; randomness and time are injected.
- `MazeGame.UI` — Raylib window (or the console fallback), keyboard input, drawing, frame
  timing. It talks to Core **only** through `GameState` (plus read-only model types), so the
  whole UI framework could be replaced without touching game logic.
- `MazeGame.Tests` — xUnit tests for Core **and the sprite compiler**. References both;
  nothing references the tests.
- `tools/AssetForge` — the sprite compiler: a console project, no packages, referencing Core
  only for `SeededRandom`. It is a build-time tool: nothing in the game references it, its output
  is committed, and it is never shipped.

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
  Game/                        GameConfig, GameStatus, GameState (rules, enemy AI, momentum),
                               FloorDefinition/FloorCatalog, Campaign (run sequence, hearts),
                               Card/CardEffect/CardCatalog, RunModifiers, Draft/DraftFactory
  Content/Generated/ (UI)      committed sprite atlases (*-tiles.png), font.png + manifest.txt
src/MazeGame.UI/
  Frontends/                   RaylibFrontend (window loop) + ConsoleFrontend (fallback loop)
  Input/                       GameCommand + GameCommands (one key-to-rule mapping)
  Rendering/                   IRenderer, RaylibRenderer (fog, vignette, HUD; the draft screen in
                               RaylibRenderer.Campaign.cs), ConsoleRenderer
  Rendering/Pixels/            PixelLayout/PixelView (canvas + integer scale), AtlasManifest/
                               AssetPaths/TileAtlas, TileMask, PixelRenderer, PixelFont/PixelCards
  Program.cs                   composition root: builds the game, picks a frontend
tools/AssetForge/
  PixelCanvas.cs               index canvas + integer primitives (Rect, Line, Ellipse, ...)
  Palette.cs                   the 16 palette roles, HSL derivation and the distance metric
  Png.cs                       dependency-free RGBA PNG encoder (stored deflate)
  Theme.cs                     Theme parameters (hues + structural knobs) + the three shipped themes
  TileGrammar.cs               the one place a TileFamily is named: floor/wall/face/prop dispatch
  CaveTiles.cs                 organic-cave grammar (boulders, moss), seeded per tile
  WrongOrganicTiles.cs         root/vein/membrane grammar for Rootbound
  WorkedStoneTiles.cs          course/grout/carved-band grammar for the drowned halls
  Glyphs.cs                    5x7 bitmap face, ASCII 32-126, plus the heart and pip icons
  FontAtlas.cs                 packs the glyphs into font.png (6x8 cells) for the manifest
  Autotiler.cs                 16 edge + 47 blob tiles from open-side masks
  Rig.cs                       the cast: explorer, killer, key, wisp, barrier, arch
  SpriteCompiler.cs            atlas layout, writing, manifest and the --check pass
  Program.cs                   CLI: --all / --theme <id> / --out <dir> / --check
tests/MazeGame.Tests/
  Abstractions/  Maze/         one test class per Core class, mirroring Core folders
  Pathfinding/  Game/          GameTests use hand-built mazes from TestDoubles/TestLevels
  Assets/                      pixel canvas, palette, PNG round-trip, compiler determinism
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
| `GameConfig` | Core / Game | Every tunable number (size, key count, obstacle cost 5, barrier budget 3, seal 10 s, tear-down 3 s, sealed cost 40, wisp count 2 / lifetime 6 s / stun 0 s / vision penalty 0, enemy step time, vision radius 3, dead-end braid 55%, extra loops 8, killer aura radius 2 / distance 6, starting hearts 3 / ceiling 3, invulnerability 1.5 s, respawn distance 8, respawn stun 1.5 s, player step 0 s = instant, sprint ×1.4 / 1.2 s stamina / 1.6 s refill, momentum ramp 0 / cap 0 / turn penalty 0 for 0 s, seed), init-only, with `Validate` enforcing the "maze is big enough for player + killer + exit + keys" rule and the heart ranges. A **record**, so a card's modifiers can compose over it with `with` and cannot silently drop a newly added value. | done (Phase 5–7, 9) |
| `GameStatus` | Core / Game | `InProgress` / `Won` / `Caught`. On one floor, `Won` means its exit was reached (in a campaign that is progress, not victory); `Caught` means the last heart was spent and ends the run. A game leaves `InProgress` once and never returns. | done (Phase 5–8) |
| `FloorDefinition` | Core / Game | One floor's data: number, display name, theme id, maze size, key count, killer step time, **player step time** and heart ceiling, with `Validate`. Everything needed to build a floor; no drawing knowledge (the theme id is a string). | done (Phase 8, 10) |
| `FloorCatalog` | Core / Game | The shipped three floors, in order, as a read-only snapshot; `ByNumber` looks one up. The content seam: a fourth floor is a new record here, not a new code path. | done (Phase 8) |
| `CampaignStatus` | Core / Game | `InProgress` / `Won` / `Lost` for the whole run, so `GameStatus.Won` can keep meaning "this floor" without overloading one enum. | done (Phase 8) |
| `Campaign` | Core / Game | One run: the floor list (snapshotted and validated), the current `GameState`, the cards held as `RunModifiers`, and the hearts carried between floors. Clearing a floor that is not the last opens a `Draft`, and `TryAdvanceFloor` refuses to advance until it is resolved (`TakeCard` or `SkipDraft`), then builds the next floor with the hearts left plus pending heals, out of the floor's ceiling **plus the run's card bonus**. Drafts are seeded from the run seed and the floor number. `Status` maps a caught floor to `Lost` at once and a cleared final floor to `Won`; `Modifiers`, `Draft`, `PendingHeals`, `HeartsOnNextFloor`, `FloorsCleared`, `IsOnLastFloor`, `IsFloorCleared` feed the UI. | done (Phase 8–9) |
| `Card` | Core / Game | One draftable bargain: id, name, `CardTier`, a one-sentence face that states both halves, a `CardEffect`, and the compatibility/short-run marks the draft guards read. Data only — it holds no behaviour. | done (Phase 9) |
| `CardTier` | Core / Game | Common / Uncommon / Rare / Curse. Weights the draft draw and colours the card; the planned floor-band gate waits for the twenty-floor run. | done (Phase 9) |
| `CardEffect` | Core / Game | The numbers one card contributes: additive deltas, two absolute overrides (seal and wisp lifetime), the multipliers on the killer's and the player's pace and on the stamina bar, the post-catch grace change, the four momentum numbers, plus the one-shot heals and the queued draft-size changes the campaign consumes. | done (Phase 9, 11) |
| `PixelCanvas` | AssetForge | A grid of palette indices with integer primitives (`Rect`, `Line`, `Ellipse`, `Speckle`, `Dither`, `Shade`, `Outline`, `Mirror`, `Stamp`) and `ToRgba`. The indexer throws outside the canvas; `Set` clips, because shapes overhang by design. | done (Phase 5, slice 1) |
| `Palette` | AssetForge | The 16 named roles, and `Derive(theme)`, which walks a fixed lightness ladder from two hues so contrast cannot drift. `DistanceTo` is the weighted RGB metric `--check` uses to reject a theme that is only a tint. | done (Phase 5, slice 1) |
| `Png` | AssetForge | Encodes RGBA to PNG with no packages: IHDR/IDAT/IEND, stored deflate blocks, CRC-32 and Adler-32. Byte-identical for identical pixels, which is what the determinism test rests on. | done (Phase 5, slice 1) |
| `Theme` / `ThemeCatalog` | AssetForge | A theme's two hues and six structural parameters, the three shipped themes, and `StructuralDifferences` so "a hue swap is not a theme" is machine-checkable. | done (Phase 5, slice 1) |
| `CaveTiles` | AssetForge | The organic-cave family: floor variants and the wall tile, painted from `(theme, variant)` and a seed derived from the tile's name — never from `string.GetHashCode`. | done (Phase 5, slice 1) |
| `Autotiler` | AssetForge | 16 edge tiles and all 47 blob tiles from open-side masks. Corner bits are canonicalised (a diagonal counts only when both edges are open), which is what makes 47, and the class count is asserted at startup so a bit-order slip fails loudly. | done (Phase 5) |
| `Rig` | AssetForge | The cast: explorer (4 facings x idle/walk0..2), killer (4 x 3), key and wisp (3 frames), barrier (fresh/cracked) and arch (locked/open). Facings come from east plus a mirror; every routine writes palette roles, never colours. | done (Phase 5) |
| `TileGrammar` | AssetForge | Chooses the drawing routines for a theme's `TileFamily` and nothing else: floor, solid wall, thin face and props. The only place a family is named, so a fourth family is a new file plus one record. | done (Phase 6 slice 2a) |
| `WrongOrganicTiles` / `WorkedStoneTiles` | AssetForge | The other two shape languages: matted roots, fibres, veins and sap for Rootbound; courses, grout, masonry joints and a carved band for the drowned halls. They share `PixelCanvas`, `Palette` roles and seeds with the cave — no drawing routine. | done (Phase 6 slice 2a) |
| `Glyphs` / `FontAtlas` | AssetForge | The hand-coded 5x7 face for ASCII 32-126 and the HUD icons, packed into `font.png` on 6x8 cells (5x7 plus a row and column of gap) with its own manifest section. A table typo fails loudly instead of drawing a hole. | done (Phase 6 slice 2a) |
| `SpriteCompiler` | AssetForge | Lays tiles 8 to a row, builds one atlas per theme (`*-tiles.png`) plus `font.png`, writes `manifest.txt` (every tile's name, position and size), and `Check` reports invalid themes, neighbours too close in colour or shape, non-deterministic compiles and drifted committed files. | done (Phase 5, 6) |
| `WallStrips` | AssetForge | The four thin wall faces per theme (`wall-north`/`-east`/`-south`/`-west`): a 5 px band on a transparent canvas, rock then lit rim then contact shadow, with each family's own texture. Drawn as whole-cell tiles because this maze stores walls *between* cells; corners overlap deliberately so a corner needs no special case. | done (Phase 6 slice 1-2a) |
| `CardCatalog` | Core / Game | The thirteen shipped cards, as a read-only list, with `ById`. The content seam: a fourteenth card is a record here — and, if it changes a rule, one validated `GameConfig` number (the momentum knobs are the worked example). | done (Phase 9, 11) |
| `RunModifiers` | Core / Game | The totals of the held cards, immutable (`With(card)`), composing over a floor's `GameConfig` with `Apply`: clamped (vision ≥ 1 once touched, budgets ≥ 0, both step times ≥ 0.05 s, ceiling ≥ carried hearts) and validated, so a card changes the floor's *inputs*, never its rules. `Describe()` renders the totals for the draft screen and console. | done (Phase 9) |
| `Draft` | Core / Game | The choice between two floors: the offers (read-only), whether it is resolved, which card was taken, and whether it was skipped. Mutation is internal to Core, so only `Campaign` can resolve it. | done (Phase 9) |
| `DraftFactory` | Core / Game | Builds the offers from `{run seed, floor number, held cards}` with a weighted, seeded draw. Guards: no duplicates unless stackable, no contradictory pair, at most two vision cards per run, and no card whose price is paid in a draft the run cannot reach. | done (Phase 9) |
| `StepRepeat` | Core / Game | Turns a held direction into one step or a repeating walk: the first frame of a press sends the direction once, later frames send nothing until the key has been down 0.15 s and the previous step has finished, a release resets the gate, and a change of direction counts as a fresh press. Input policy with no maze and no window, which is why it lives in Core: it is the only way to test "a tap is one tile" without opening a GLFW context. | done (Phase 4 fix) |
| `GameState` | Core / Game | The single public entry point for the UI. Owns maze, player, killer, exit, uncollected keys, barriers and their seals, the wisp, the visible-cell set, the killer's aura cells, the player's hearts, invulnerability and the killer's stun, the straight-step counter and turn-penalty window, and status. Commands: `TryMove`, `Tick`, and the aiming set `BeginBarrierPlacement` / `BeginLurePlacement` / `MoveAimCursor` / `ConfirmPlacement` / `CancelPlacement`; `BarrierSeals`, `Lure`, `IsVisible(Cell)`, `Hearts`/`MaxHearts`, `IsInvulnerable`, `IsEnemyStunned` feed the renderers, `EnemyGlow` says which cells are lit by the killer. Contains the enemy AI (two cached Dijkstra routes compared to pick the player or the wisp, step time scaled by cell cost, seal tear-down as its own action), the catch rule (`TakeHit`: one heart, respawn at the furthest reachable non-objective cell, stun, grace window) and the win/lose rules. Movement is timed: `TryMove` starts a step (the cell changes at once), one direction may be buffered while the step cools down, `Tick` spends it, and `SetSprinting`/`IsSprinting` run a stamina bar that drains only while a step is running and refuses to start while aiming or on an instant-step floor. `PreviousPlayer`/`PlayerStepFraction` are the step's clock (a walk-cycle input; the player's figure snaps to its cell) and `PreviousEnemy`/`EnemyStepFraction` are the killer's slide. | done (Phase 5–10) |
| `GameFactory` | Core / Game | Builds floors and runs: `CreateCampaign()` for the shipped three floors (passing the run seed so drafts reproduce), `CreateFloor(floor, hearts, modifiers)` for one floor, and `Create()` for a single game from the base config (used by tests/tools). Each floor gets a random source seeded from the run seed plus its number (`Func<int?, IRandom>`), goes through the shipped maze pipeline (`MazeGenerator` → `DeadEndBraider` → `BraidedMazeGenerator`), and is built from `GameConfig.ForFloor` with the run's cards applied over it. Validates its config in the constructor. Starting or restarting a run is one call. | done (Phase 6–9) |
| `IRenderer` | UI / Rendering | `Draw(GameState)`; draws a frame and never changes the game. | done (Phase 5) |
| `PixelLayout` | UI / Pixels | Canvas geometry and the one scaling rule: a cell is exactly one 16 px atlas tile, an 8 px margin, an 18 px header, and `Fit` returns an **integer** scale plus a centred offset (1 to 4, never a fraction). Pure, so it is tested without a window. | done (Phase 6 slice 1) |
| `PixelView` | UI / Pixels | The virtual canvas: a `RenderTexture2D` at atlas resolution with a point filter, `Begin`/`End` around a world pass, and `Present` which clears the letterbox, blits at `Fit`'s integer scale, and returns where it landed. | done (Phase 6 slice 1) |
| `AtlasManifest` / `ThemeAtlas` / `AtlasTile` | UI / Pixels | The contract with the sprite compiler: parses `manifest.txt` (theme lines, tile lines with an explicit width and height that defaults to 16x16) and validates it — counts, duplicate names, tiles outside the atlas — throwing `FormatException` with the line number. `Tile(name)` throws for unknown art rather than drawing a placeholder. | done (Phase 6 slice 1-2a) |
| `PixelFont` / `PixelCards` | UI / Pixels | The compiled face as a texture: text, centred text and icons at an integer scale (advance 6, line 8), plus the four campaign cards (floor intro, cleared, run summary, caught) built from captions, an auto-fitted heading, hearts and a rule. A glyph the sheet lacks draws as `?`. | done (Phase 6 slice 2a) |
| `AssetPaths` | UI / Pixels | Finds `Content/Generated` beside the executable, then up the tree in the repository layout, and names every path it tried when it cannot. | done (Phase 6 slice 1) |
| `TileAtlas` | UI / Pixels | One theme's PNG as a texture with a point filter, plus `Draw(name, x, y, tint)` by manifest rect. | done (Phase 6 slice 1) |
| `TileMask` | UI / Pixels | Which of a cell's eight neighbours are reachable (N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128), the canonical corner rule that reduces 256 masks to 47, the 4-bit edge mask, and the name of the thin face on a closed side. Duplicates the compiler's table on purpose; a test proves both agree for all 256 masks. | done (Phase 6 slice 1) |
| `PixelRenderer` | UI / Pixels | Draws a floor from the atlas into the canvas: floor variants by cell hash, thin wall faces (north and west ownership plus the outer rim, so a shared wall is painted once), keys/arch/barriers/wisp and both figures with walk frames from the step clocks, a per-cell multiplied lightmap (lantern falloff, remembered cells, killer tint, flicker on the draw's tint), then presents and draws the reticle as chrome. Takes the theme from `SetTheme`, so a floor with no committed atlas fails loudly. | done (Phase 6 slice 1) |
| `ConsoleRenderer` | UI / Rendering | ASCII view: maze walls, `@` player, `K` killer, `k` key, `E` exit, `#` sealed barrier, `=` spent barrier, `*` wisp, plus a HUD line starting with `hearts n/n`. Fog-aware: unseen cells are blank, remembered ones are `.`; entities only while visible. | done (Phase 6–7) |
| `RaylibRenderer` | UI / Rendering | Also `DrawOverlay(state)`: the damage flash, status pills, hints, loss card and grain, i.e. every frame's chrome, so the pixel renderer and the vector one share one HUD. Hearts (filled, dim, pulsing on the last) in the footer beside the hints, and a 0.35 s red wash when one is lost (`TrackDamage` compares the rule value each frame; no event stream yet). Keys are drawn as beacons (floor pool, light shaft, ring, bobbing core, orbiting sparks) **after** the lantern falloff, because a lighting pass over contents hides exactly the things the player must see. The rest of the window is drawn as a bioluminescent cave: rounded stone floors, rock walls in two passes (dark rock, then lit rim/moss/lichen), glowing mushrooms, a hooded lantern-bearer, a spindly killer, fungus keys, an arch exit, thorn barriers (a breathing green cage while sealed, a dim husk once spent), a cyan wisp with orbiting sparks, green/pink/cyan spores, background blooms, grain, vignette, glass HUD pills (keys, barriers, wisps, exit), a mode-coloured aim reticle and end card. Draws the explorer at its own cell (one press, one tile, so the grid stays countable) and the killer from its interpolated pair (after the cell passes, so it slides), and a sprint bar under the status pills that appears only once stamina has been spent. Also draws the title screen (`DrawTitle`), the campaign's floor intro, floor-cleared and run-summary cards, and the draft screen (a grid of card faces with wrapped text, tier colours, a selection ring and the run's totals — `RaylibRenderer.Campaign.cs`), and `Reset()` clears its explored-cell memory between floors. Reads visibility from Core. | done (Phase 6–9) |
| `RaylibLayout` | UI / Rendering | Window geometry shared by the frontend and the renderer: 40 px cells, margins, HUD/footer strips, `WindowSize(config)`, `CellX`/`CellY`, window title. | done (Phase 6) |
| `GameCommand` | UI / Input | Backend-independent player intentions (`MoveNorth`…`BeginAim`, `BeginLureAim`, `ConfirmAim`, `CancelAim`, `Quit`). | done (Phase 6) |
| `GameCommands` | UI / Input | Applies a command to `GameState`; the one place that knows direction keys move the aim cursor instead of the player while aiming. | done (Phase 6) |
| `RaylibFrontend` | UI / Frontends | Opens the window, owns the screen flow (title → floor intro → play → floor-cleared → **draft** → next floor → run summary / lost card), maps keys, brackets drawing, and starts a run by calling the injected `Func<Campaign>`. The play screen is drawn by `PixelRenderer` (chosen theme from `Campaign.CurrentFloor.ThemeId`) and the menus by `RaylibRenderer`. Walking asks Core's `StepRepeat` for this frame's direction (a tap is one tile, a hold repeats; the aim cursor stays press-based, where a hold would fly across the map) and samples Shift every frame for sprint; the draft takes A/D or arrows to choose, Enter/Space or a number key to take, S to skip; restarting is R/Enter after a run ends. Puts the floor number in the window title and returns `false` when no window is available. | done (Phase 6–9) |
| `ConsoleFrontend` | UI / Frontends | Terminal campaign loop with the same command mapping, an ASCII title, a floor heading, carried-hearts line, a printed draft prompt (number keys take, S skips; a piped run skips for the heart), and a run summary, plus the `--console`/fallback role. | done (Phase 6–9) |

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
3. The frontend reads its own keyboard API and translates input into one `GameCommand`.
   `GameCommands.Apply` performs it: normally `TryMove(direction)` or
   `BeginBarrierPlacement()` / `BeginLurePlacement()`; while `IsAiming` is true the same
   direction keys go to `MoveAimCursor`, B/Space/Enter confirm and Esc cancels. Walking is read
   through Core's `StepRepeat`: a fresh press is worth one direction, a hold repeats after 0.15 s
   once the previous step has finished, and sprint is a held Shift. The aim cursor stays
   press-based. No game rules live in the UI: `TryMove` decides the cadence and buffers at most
   one press.
4. The frontend measures real elapsed time and calls `GameState.Tick(deltaSeconds)`, which
   spends a buffered step, ages the sprint bar, and advances the killer at most one step. The two
   step fractions it updates drive the killer's slide and the player's walk-cycle frame; the
   explorer's *position* is its cell, never a fraction.
5. When a floor's exit is reached, `GameStatus.Won` makes the frontend show the floor-cleared
   card; Enter opens `Campaign.Draft`, whose offers were generated on the spot from the run seed
   and the floor number. Taking a card folds it into `Campaign.Modifiers`; skipping banks a heart.
   Only then does `Campaign.TryAdvanceFloor()` build the next floor — with the hearts left and the
   held cards applied over its config. On the last floor the run summary card appears instead, and
   there is no draft. A lost run shows the end card over the last frame; R or Enter starts a fresh
   campaign. Quitting returns the run so the
   outcome can be printed.
6. The play screen is drawn by `PixelRenderer.Draw(state)`: the world goes into `PixelView`'s
   render texture at 16 px per cell (floor, thin wall faces, cast, then the lightmap multiplied over
   it), the texture is blitted to the window at an integer scale, and `RaylibRenderer.DrawOverlay`
   draws the chrome around that letterbox. The title, floor intro, draft and run summary are still
   `RaylibRenderer` screens drawn over the same window. `ConsoleRenderer.Draw(state)` rebuilds the
   ASCII frame and writes it. Every renderer asks `GameState.IsVisible` what it may show, uses
   `EnemyGlow` to mark the killer's light, and keeps its own memory of explored cells.
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
stack. Core still has no packages at all. The test project references Core, the sprite compiler
*and* the UI: the UI's pure rendering helpers (manifest parsing, tile masks, canvas geometry) are the
contract with the committed atlases, and a drift between the game and the compiler has to be a red
test. None of those tests opens a window, so Raylib's native library is never called from them.

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
