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
                  Frontends/  Input/  Rendering/  Audio/
                  IRenderer ← PixelRenderer, ConsoleRenderer
                  GameCommand → GameCommands → GameState
                  GameState.DrainEvents → AudioDirector → AudioEngine → device
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
  Maze/                        disjoint set, grid/cell model, maze shape masks (rect/blob/ellipse),
                               maze generators (perfect + braid)
  Pathfinding/                 binary min-heap and Dijkstra (the enemy AI lives in GameState)
  Game/                        GameConfig, GameStatus, GameState (rules, enemy AI, momentum),
                               GameEvent (the moments the UI reacts to),
                               FloorDefinition/FloorCatalog, Campaign (run sequence, hearts),
                               Card/CardEffect/CardCatalog, RunModifiers, Draft/DraftFactory
  Content/Generated/ (UI)      committed sprite atlases (*-tiles.png), font.png + manifest.txt
src/MazeGame.UI/
  Frontends/                   RaylibFrontend (window loop) + ConsoleFrontend (fallback loop)
  Input/                       GameCommand + GameCommands (one key-to-rule mapping)
  Rendering/                   IRenderer, PixelRenderer (the world, from the atlases) and
                               RaylibRenderer (the chrome: title, HUD strips, hints, hearts, grain),
                               FloorStyle/FloorChrome/FloorEmblem
  Persistence/                 SaveStore and GardenProfile: the run and garden files
                               (per-floor accent, weather, light, fog and HUD identity),
                               ConsoleRenderer
  Rendering/Pixels/            PixelLayout/PixelCamera/PixelView (viewport + integer scale),
                               AtlasManifest/AssetPaths/TileAtlas, TileMask, PixelRenderer,
                               PixelFont/PixelCards
  Audio/                       Synth (oscillators, filters, reverb), SfxBank (22 recipes),
                               MusicTheme/MusicCatalog (20 pieces, 20 melodies, 10 styles), ScoreRenderer
                               (three stems per floor), MusicMix (state → intensity), AudioEngine
                               (mixer), AudioDirector (Raylib device, silent fallback)
  Program.cs                   composition root: builds the game, picks a frontend
tools/AssetForge/
  PixelCanvas.cs               index canvas + integer primitives (Rect, Line, Ellipse, ...)
  Palette.cs                   the 16 palette roles, HSL derivation and the distance metric
  Png.cs                       dependency-free RGBA PNG encoder (stored deflate)
  Theme.cs                     Theme parameters (hues, family, floor/wall motif, feature, weather)
                               + the twenty shipped themes
  TileGrammar.cs               dispatch from a theme's floor/wall motif to its drawing routine
  MotifTiles.cs                the nine new floor grammars, seven wall grammars, their faces,
                               eleven signature objects and the per-floor emblem
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
| `GameConfig` | Core / Game | Every tunable number (size, key count, **powerup charges**, **descent conditions and the muted powerup**, **free first catch, revival, catch cost, magnetic keys, blink refunds, veil/drag durations, lantern range**, enemy step time, vision radius 3, dead-end braid 55%, extra loops 8, killer aura radius 2 / distance 6, starting hearts 3 / ceiling 3, invulnerability 1.5 s, respawn distance 8, respawn stun 1.5 s, player step 0 s = instant, sprint ×1.4 / 1.2 s stamina / 1.6 s refill, momentum ramp 0 / cap 0 / turn penalty 0 for 0 s, seed), init-only, with `Validate` enforcing the "maze is big enough for player + killer + exit + keys" rule and the heart ranges. A **record**, so a card's modifiers can compose over it with `with` and cannot silently drop a newly added value. | done (Phase 5–7, 9) |
| `GameStatus` | Core / Game | `InProgress` / `Won` / `Caught`. On one floor, `Won` means its exit was reached (in a campaign that is progress, not victory); `Caught` means the last heart was spent and ends the run. A game leaves `InProgress` once and never returns. | done (Phase 5–8) |
| `FloorDefinition` | Core / Game | One floor's data: number, display name, theme id, maze size, key count, killer step time, **player step time** and heart ceiling, with `Validate`. Everything needed to build a floor; no drawing knowledge (the theme id is a string). | done (Phase 8, 10) |
| `FloorCatalog` | Core / Game | The shipped twenty floors, in order, as a read-only snapshot; `ByNumber` looks one up. The content seam: a floor is a record (name, theme, keys, pace, **hunter count**, **powerup kit**, and any of the optional gimmick knobs — vision, loops, braid, aura, respawn, grace), so a new one is data, not a code path. The hunt goes one hunter (floors 1–9), two (10–15), three (16–20). | done (Phase 8, Phase 9) |
| `CampaignStatus` | Core / Game | `InProgress` / `Won` / `Lost` for the whole run, so `GameStatus.Won` can keep meaning "this floor" without overloading one enum. | done (Phase 8) |
| `Campaign` | Core / Game | One run: the floor list (snapshotted and validated), the current `GameState`, the cards held as `RunModifiers`, the descent conditions chosen, and the hearts carried between floors. Clearing a floor that is not the last opens a `Draft`, and at floors 8 and 14 also a `ConditionOffer`; `TryAdvanceFloor` refuses to advance until both are resolved (`TakeCard`/`SkipDraft`, `TakeCondition`); `TryRerollDraft` spends 1, then 2, then 3 hearts on a
fresh seeded table without ever taking the last one, and `TryAdvanceFloor` clears that ladder with the
draft, then builds the next floor with the hearts left plus pending heals, out of the floor's ceiling **plus the run's card bonus and Mute's heart**. Drafts are seeded from the run seed and the floor number. `Status` maps a caught floor to `Lost` at once and a cleared final floor to `Won`; `Modifiers`, `Draft`, `PendingHeals`, `HeartsOnNextFloor`, `FloorsCleared`, `IsOnLastFloor`, `IsFloorCleared` feed the UI. | done (Phase 8–9) |
| `Card` | Core / Game | One draftable bargain: id, name, `CardTier`, a one-sentence face that states both halves, a `CardEffect`, and the compatibility/short-run marks the draft guards read. Data only — it holds no behaviour. | done (Phase 9) |
| `CardTier` | Core / Game | Common / Uncommon / Rare / Curse. Weights the draft draw by floor band and colours the card; every fifth floor's draft guarantees a rare when one is still eligible. | done (Phase 9) |
| `CardEffect` | Core / Game | The numbers one card contributes: additive deltas (including the five powerup charge counts), the multipliers on the killer's and the player's pace and on the stamina bar, the post-catch grace change, the four momentum numbers, the second-wave knobs (free first catch, magnetic keys, revival, blink refund, catch cost, veil/drag durations, lantern range), the catch-granted bloom glow and the passive bloom sense (tiles, not steps), plus the one-shot heals and the queued draft-size changes the campaign consumes. | done (Phase 9, 11) |
| `PixelCanvas` | AssetForge | A grid of palette indices with integer primitives (`Rect`, `Line`, `Ellipse`, `Speckle`, `Dither`, `Shade`, `Outline`, `Mirror`, `Stamp`) and `ToRgba`. The indexer throws outside the canvas; `Set` clips, because shapes overhang by design. | done (Phase 5, slice 1) |
| `Palette` | AssetForge | The 16 named roles, and `Derive(theme)`, which walks a fixed lightness ladder from two hues so contrast cannot drift. `DistanceTo` is the weighted RGB metric `--check` uses to reject a theme that is only a tint. | done (Phase 5, slice 1) |
| `Png` | AssetForge | Encodes RGBA to PNG with no packages: IHDR/IDAT/IEND, stored deflate blocks, CRC-32 and Adler-32. Byte-identical for identical pixels, which is what the determinism test rests on. | done (Phase 5, slice 1) |
| `Theme` / `ThemeCatalog` | AssetForge | A theme's two hues, its shape family, floor motif, wall motif, signature feature and weather, plus the older roundness/density knobs; the twenty shipped themes; and `StructuralDifferences` so "a hue swap is not a theme" is machine-checkable. | done (Phase 5, Phase 8) |
| `CaveTiles` | AssetForge | The organic-cave family: floor variants and the wall tile, painted from `(theme, variant)` and a seed derived from the tile's name — never from `string.GetHashCode`. | done (Phase 5, slice 1) |
| `Autotiler` | AssetForge | 16 edge tiles and all 47 blob tiles from open-side masks. Corner bits are canonicalised (a diagonal counts only when both edges are open), which is what makes 47, and the class count is asserted at startup so a bit-order slip fails loudly. | done (Phase 5) |
| `Rig` | AssetForge | The cast: explorer (4 facings x idle/walk0..2), killer (4 x 3), key and wisp (3 frames), barrier (fresh/cracked) and arch (locked/open). Facings come from east plus a mirror; every routine writes palette roles, never colours. | done (Phase 5) |
| `TileGrammar` | AssetForge | Chooses the drawing routines for a theme's floor motif, wall motif, thin face, two scenery props and its emblem; the family still supplies the three older bases (boulders, flagstones, roots). A new motif is a new drawing routine plus one record. | done (Phase 6, Phase 8) |
| `WrongOrganicTiles` / `WorkedStoneTiles` | AssetForge | The other two shape languages: matted roots, fibres, veins and sap for Rootbound; courses, grout, masonry joints and a carved band for the drowned halls. They share `PixelCanvas`, `Palette` roles and seeds with the cave — no drawing routine. | done (Phase 6 slice 2a) |
| `Glyphs` / `FontAtlas` | AssetForge | The hand-coded 5x7 face for ASCII 32-126 and the HUD icons, packed into `font.png` on 6x8 cells (5x7 plus a row and column of gap) with its own manifest section. A table typo fails loudly instead of drawing a hole. | done (Phase 6 slice 2a) |
| `SpriteCompiler` | AssetForge | Lays tiles 8 to a row, builds one atlas per theme (`*-tiles.png`) plus `font.png`, writes `manifest.txt` (every tile's name, position and size), and `Check` reports invalid themes, neighbours too close in colour or differing in fewer than six structural parameters, non-deterministic compiles and drifted committed files. | done (Phase 5, 6, 8) |
| `WallStrips` | AssetForge | The four thin wall faces per theme (`wall-north`/`-east`/`-south`/`-west`): a 5 px band on a transparent canvas, rock then lit rim then contact shadow, with the theme's wall motif as its texture (moss rock, cut block, woven root, ribbed, plate, crystal, silk, bone, grate or frost). A per-id maker's mark keeps even two floors that share a motif distinct. Drawn as whole-cell tiles because this maze stores walls *between* cells; corners overlap deliberately. | done (Phase 6, Phase 8) |
| `CardCatalog` | Core / Game | The thirty-nine shipped cards, as a read-only list, with `ById`. The content seam: another card is a record here — and, if it changes a rule, one validated `GameConfig` knob (the bloom-glow knobs are the worked example). Every tier is on the face, and `House Favour` is the only `SuperRare`. | done (Phase 9, 11) |
| `FloorFaces` / `FloorFace` | Core / Game | The per-run bend on an authored floor: authored (weighted double), quiet, greedy, haunted or starved, rolled from the run seed and floor number and applied by `Campaign` while it snapshots its floors. Floors below `FirstVariedFloor` never roll. A face changes a lever (hunters, keys, pace, vision, loops, braid, rule) and prepends its clause to the gimmick; it never changes the number, name or theme. | done (Phase 11) |
| `RelicCatalog` / `RelicKind` / `RelicFamily` / `RelicRarity` | Core / Game | The thirty-seven permanent finds an altar can hold, expressed as rare `Card`s with no drawback, plus `CardFor`/`KindOf`/`ById`, `FamilyOf` (Body, Light, Hunt, Garden, Rite — the colour both frontends draw), `RarityOf`/`WeightOf` (common, rare, legendary — six, three and one tickets) and a seed+floor+skip `Pick` that walks a weighted shuffle past the relics a run already holds. `GameState` places the altar; `Campaign.Sync` folds the relic into the run's cards so the existing card pipeline (clamping, saving, summary) needs no relic case. | done (Phase 11) |
| `Tutorial` / `TutorialStep` | Core / Game | The guided tutorial's pages as content: a caption, a heading, prose and the exact keys, with `Validate`. Both frontends draw the same list, so the window and the terminal cannot teach different controls. No rules change from it. | done (seventh pass) |
| `PracticeTutorial` | Core / Game | The tutorial's do-it floor: a 9x9 chamber whose player, arch, blooms and hunter are taken in reading order (a private `IRandom` that does not shuffle), with a hunter whose clock is ten minutes a step so nothing can catch the player. The four steps are derived from the floor (moved, picked one bloom, picked all, opened the arch), so one walk can pass two of them; `Update` advances and `Prompt` says what to do next. | done (eighth pass) |
| `RunModifiers` | Core / Game | The totals of the held cards, immutable (`With(card)`), composing over a floor's `GameConfig` with `Apply`: clamped (vision ≥ 1 once touched, budgets ≥ 0, both step times ≥ 0.05 s, ceiling ≥ carried hearts) and validated, so a card changes the floor's *inputs*, never its rules. `Describe()` renders the totals for the draft screen and console. | done (Phase 9) |
| `Draft` | Core / Game | The choice between two floors: the offers (read-only), whether it is resolved, which card was taken, and whether it was skipped. Mutation is internal to Core, so only `Campaign` can resolve it. | done (Phase 9) |
| `DraftFactory` | Core / Game | Builds the offers from `{run seed, floor number, held cards}` with a seeded draw weighted by tier **and floor band**, so rares become common later. Every fifth floor guarantees a rare when one is eligible. Guards: no duplicates unless stackable, no contradictory pair, at most two vision cards per run, and no card whose price is paid in a draft the run cannot reach. | done (Phase 9, 11) |
| `GameEvent` | Core / Game | The moments a frame cannot be inferred from state (step, key, heart lost / last heart, killer step, the five powerup events, wall sealed/opened/closed, exit moved, eclipse fell/lifted, floor cleared, run lost) plus `GameState.DrainEvents()`, a queue capped at 32 so a frontend that never drains cannot grow it with the run. The seam every sound hangs from; Core has no audio types. | done (Phase 7, 11) |
| `PowerupKind` / `PowerupCharges` | Core / Game | The five verbs and their per-floor charge counts. `ForFloor` is the teaching ladder (one new verb at a time), `Of`/`With`/`Adjust` compose a floor's kit, and `Validate` rejects a negative count. `FloorDefinition.Powerups` and `GameConfig.Powerups` carry the record. | done (Phase 11) |
| `DescentCondition` / `DescentConditionCatalog` / `ConditionOffer` | Core / Game | The eight run-scoped rules, their names and one-sentence descriptions, the seeded two-card offer for a gate, and the offer object only `Campaign` can resolve. Conditions ride `GameConfig.Conditions` into every later floor. | done (Phase 11) |
| `Synth` | UI / Audio | The DSP toolkit: sine (with a table for the offline score), saw, triangle, square, a seeded xorshift noise source, one-pole and state-variable band-pass filters, envelopes, soft clip, peak/RMS/seam measurements and a Schroeder reverb whose `Apply(..., circular: true)` runs around a loop point so a looping track has no tail to cut. No Raylib, so all of it is testable. | done (Phase 7) |
| `SfxBank` | UI / Audio | Seventeen one-shot recipes (three footstep variants, killer step, damage, last heart, heartbeat, key pickup, sonar ping, wall sealed/broken, soft light/light out, arch locked/unlocked, floor cleared, card hover/take, run lost, lunge tell, near miss), each built once into a mono buffer and normalised. 0.09–1.8 s, peak ≤ 0.9, no two identical; ~2 MB and 27 ms to build. | done (Phase 7) |
| `MusicTheme` / `MusicCatalog` | UI / Audio | A floor's piece as parameters (root, mode, **style**, tempo, **steps per bar**, bars, progression, **its own melody**, per-family mix, density, tension, reverb, band) and the twenty of them, in floor order with **atlas ids**, plus the scale/degree maths. Every floor has a distinct melody, and no two neighbouring floors share a style. A test keeps the list identical to `FloorCatalog`. | done (Phase 7, Phase 9) |
| `ScoreRenderer` | UI / Audio | Arranges one loop of three stems, style first: a drone's held fifth, a lullaby's music box, a procession's march, a chant's formant choir, a pulse's arpeggio and four-on-the-floor, a waltz's three beats, a nocturne's piano, a gamelan's gongs and interlock, a machine's 5/4 clanks, or a requiem's organ and bell. Meter comes from the theme, every note tail wraps, the reverb runs around the loop and the stems are smoothed and normalised together. 170–400 ms per floor (debug), rendered during the intro card. | done (Phase 7, Phase 9) |
| `MusicMix` | UI / Audio | Turns a floor into an intensity (`Calm` / `Tense` / `Chase`, with a stunned killer settling to calm) and an intensity into three stem gains; `ForDistance` is the threshold table and `Gains` the mix. Pure, so the reactive behaviour is tested without a device. | done (Phase 7) |
| `AudioEngine` | UI / Audio | The mixer: three stems with per-channel width, sixteen one-shot voices with linear-interpolated pitch and equal-power pan, a duck ramp, bar-aligned intensity adoption, and a fixed scratch buffer so a block never allocates. Renders interleaved 16-bit stereo; 0.006 ms per 2048-frame block. | done (Phase 7) |
| `AudioDirector` | UI / Audio | Opens the device, installs the stream callback (a Cdecl `UnmanagedCallersOnly` shim), arranges each floor's track, maps `GameEvent` to `SfxId`, follows the floor's intensity and plays the last-heart heartbeat. Returns `null` when there is no device, and every caller is `?.`, so a headless machine is silent rather than broken. | done (Phase 7) |
| `HunterView` | Core / Game | One hunter as a frame sees it: current cell, the cell it stepped from, how far through that step it is, and whether it is stunned. A value type on purpose, so reading `GameState.Enemies` every frame allocates nothing and no renderer reaches into the rule. | done (Phase 9) |
| `StepRepeat` | Core / Game | Turns a held direction into one step or a repeating walk: the first frame of a press sends the direction once, later frames send nothing until the key has been down 0.15 s and the previous step has finished, a release resets the gate, and a change of direction counts as a fresh press. Input policy with no maze and no window, which is why it lives in Core: it is the only way to test "a tap is one tile" without opening a GLFW context. | done (Phase 4 fix) |
| `GameState` | Core / Game | The single public entry point for the UI. Owns maze, player, hunters, exit, uncollected keys, the visible-cell set, the killer's aura cells, the player's hearts, invulnerability and the killer's stun, the straight-step counter and turn-penalty window, the five powerup charges and their active states, the run's descent conditions, the card-granted reprieves (a free catch, one revival, magnetic pickups), and status. Commands: `TryMove`, `Tick`, `TryBlink`, `TryVeil`, `TryDrag`, `TryThrowLantern` and `TryShove`; `PowerupsRemaining`, `IsVeiled`/`IsDragActive`/`IsLanternActive`, `LightOrigin`, `Conditions`/`HasCondition`, `IsEclipseDark`, `IsVisible(Cell)`, `Hearts`/`MaxHearts`, `IsInvulnerable`, `IsEnemyStunned` feed the renderers, `EnemyGlow` says which cells are lit by the killer. Contains the enemy AI (one cached Dijkstra route, the Pack shared clock, the Listener turn-based exchange and the Hunted speed edge), the condition timers (Regrowth seals an empty dead end, Fracture opens and closes a wall behind a connectivity check, Eclipse toggles the torch, Drift moves the exit after a key), the catch rule (`TakeHit`: one heart, respawn at the furthest reachable non-objective cell, stun, grace window) and the win/lose rules. Movement is timed: `TryMove` starts a step (the cell changes at once), one direction may be buffered while the step cools down, `Tick` spends it, and `SetSprinting`/`IsSprinting` run a stamina bar that drains only while a step is running and refuses to start after a turn or on an instant-step floor. `PreviousPlayer`/`PlayerStepFraction` are the step's clock (a walk-cycle input; the player's figure snaps to its cell) and `PreviousEnemy`/`EnemyStepFraction` are the killer's slide. `DrainEvents()` hands the UI the moments that happened (see `GameEvent`), which is how sound is triggered without polling. | done (Phase 5–11) |
| `GameFactory` | Core / Game | Builds floors and runs: `CreateCampaign()` for the shipped twenty floors (passing the run seed so drafts reproduce), `CreateFloor(floor, hearts, modifiers)` for one floor, and `Create()` for a single game from the base config (used by tests/tools). Each floor gets a random source seeded from the run seed plus its number (`Func<int?, IRandom>`), goes through the shipped maze pipeline (`MazeGenerator` → `DeadEndBraider` → `BraidedMazeGenerator`), and is built from `GameConfig.ForFloor` with the run's cards applied over it. Validates its config in the constructor. Starting or restarting a run is one call. | done (Phase 6–9) |
| `IRenderer` | UI / Rendering | `Draw(GameState)`; draws a frame and never changes the game. | done (Phase 5) |
| `PixelLayout` | UI / Pixels | The viewport, not the floor: a cell is exactly one 16 px atlas tile, the canvas holds `ViewCells(visionRadius)` cells (odd, 9–13, lit radius plus a ring) with an 8 px margin and an 18 px header, and `Fit` returns an **integer** scale plus a centred offset (1 to 6, never a fraction). Pure, so it is tested without a window. | done (Phase 6 camera pass) |
| `PixelCamera` | UI / Pixels | Two integers: the cell the viewport's top-left corner sits on. `For` centres the focus cell and clamps to the floor, or centres a floor smaller than the viewport; `Shows` answers culling. Snaps one cell at a time, like the figure it follows. | done (Phase 6 camera pass) |
| `PixelView` | UI / Pixels | The virtual canvas: a `RenderTexture2D` at atlas resolution with a point filter, `Begin`/`End` around a world pass, and `Present` which clears the letterbox, blits at `Fit`'s integer scale, and returns where it landed. | done (Phase 6 slice 1) |
| `AtlasManifest` / `ThemeAtlas` / `AtlasTile` | UI / Pixels | The contract with the sprite compiler: parses `manifest.txt` (theme lines, tile lines with an explicit width and height that defaults to 16x16) and validates it — counts, duplicate names, tiles outside the atlas — throwing `FormatException` with the line number. `Tile(name)` throws for unknown art rather than drawing a placeholder. | done (Phase 6 slice 1-2a) |
| `PixelFont` / `PixelCards` | UI / Pixels | The compiled face as a texture: text, centred text and icons at an integer scale (advance 6, line 8), plus **every screen outside the maze** — the title (wordmark, rule, menu) and the draft, as well as the four campaign cards (floor intro, cleared, run summary, caught) built from captions, an auto-fitted heading, hearts and a rule. The first three also draw the floor's mirrored emblem and a five-block palette strip, so a card identifies its floor without reading the name. A glyph the sheet lacks draws as `?`. The HUD and footer set their text through the same face, handed to `RaylibRenderer` by `PixelRenderer`. | done (Phase 6, Phase 8) |
| `AssetPaths` | UI / Pixels | Finds `Content/Generated` beside the executable, then up the tree in the repository layout, and names every path it tried when it cannot. | done (Phase 6 slice 1) |
| `TileAtlas` | UI / Pixels | One theme's PNG as a texture with a point filter, plus `Draw(name, x, y, tint)` by manifest rect. | done (Phase 6 slice 1) |
| `TileMask` | UI / Pixels | Which of a cell's eight neighbours are reachable (N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128), the canonical corner rule that reduces 256 masks to 47, the 4-bit edge mask, and the name of the thin face on a closed side. Duplicates the compiler's table on purpose; a test proves both agree for all 256 masks. | done (Phase 6 slice 1) |
| `PixelRenderer` | UI / Pixels | Draws a floor from the atlas into the canvas: floor variants by cell hash, thin wall faces (north and west ownership plus the outer rim, so a shared wall is painted once), the floor's signature object and emblem at their own rates, keys/arch and both figures with walk frames from the step clocks, a per-cell multiplied lightmap (lantern falloff, remembered cells, killer tint, flicker on the draw's tint and the floor's warm/cold cast), a 30-particle weather layer, then presents the canvas. Reads the floor's size from `GameState.Maze` (the gallery is 21x21, the warren 11x11), takes its style from `SetTheme` and its name/gimmick from `SetFloor`, so a floor with no committed atlas or style fails loudly. Draws **every** hunter from `GameState.Enemies`, each with its own facing and walk frame, tinted cold while stunned; a wall face whose far-side neighbour is unknown is drawn by the near side instead, so a corridor against unseen rock still has its wall. | done (Phase 6, Phase 8, Phase 9) |
| `ConsoleRenderer` | UI / Rendering | ASCII view: maze walls, `@` player, `K` killer, `k` key, `E` exit, and the legacy `#`/`=`/`*` symbols for any floor rule that still places them, plus a HUD line with hearts, keys, the five powerup charges, exit state and the active descent conditions. Fog-aware: unseen cells are blank, remembered ones are `.`; entities only while visible. | done (Phase 6–7) |
| `RaylibRenderer` | UI / Rendering | The chrome around the maze, not the maze: `DrawOverlay(state, FloorChrome)` paints the damage flash, the status pills, the hearts (filled, dim, pulsing on the last), the hints, the sprint bar and the grain; `DrawTitle` paints the title over the cave gradient, spores and vignette. `DrawOverlay` is called by `PixelRenderer` after the world, so there is one HUD rather than two that can drift. `Reset()` forgets the last heart count and hit time between runs. | done (Phase 6–10) |
| `RaylibLayout` | UI / Rendering | Window geometry shared by the frontend and the renderer: 40 px cells, margins, HUD/footer strips, `WindowSize(config)`, `CellX`/`CellY`, window title. | done (Phase 6) |
| `GameCommand` | UI / Input | Backend-independent player intentions (`MoveNorth`…`MoveEast`, `BeginAim`/`BeginLureAim` as the legacy placement set, `Blink`, `Veil`, `Drag`, `Lantern`, `Shove`, `Quit`). | done (Phase 6, 11) |
| `GameCommands` | UI / Input | Applies a command to `GameState`; the only mapping from the five powerup commands to their rules. | done (Phase 6, 11) |
| `SaveStore` / `GardenProfile` | UI / Persistence | `run.json` and `garden.json` under the per-user data folder (or `MAZEGAME_SAVE_DIR`). The run is a `RunSnapshot`, written at every floor and cleared when a run ends or is abandoned; the garden is a plain tally (`Blooms`, `DeepestFloor`, `Runs`) that one finished run adds to. Writes go through a temp file and a move, and an unreadable file is renamed `.bad` rather than crashing the frontend. | done (Phase 10) |
| `RaylibFrontend` | UI / Frontends | Opens the window, owns the screen flow (title → floor intro → play → floor-cleared → **draft** → next floor → run summary / lost card), maps keys, brackets drawing, and starts a run by calling the injected `Func<Campaign>`. The play screen is drawn by `PixelRenderer` (chosen theme from `Campaign.CurrentFloor.ThemeId`) and the menus by `RaylibRenderer`. Walking asks Core's `StepRepeat` for this frame's direction (a tap is one tile, a hold repeats) and samples Shift every frame for sprint; Space/V/T/L/F fire the five powerups; the draft takes A/D or arrows to choose, Enter/Space or a number key to take, S to skip; a gate takes A/D and Enter; restarting is R/Enter after a run ends. Puts the floor number in the window title and returns `false` when no window is available. | done (Phase 6–9) |
| `ConsoleFrontend` | UI / Frontends | Terminal campaign loop with the same command mapping, an ASCII title, a floor heading, carried-hearts line, a printed draft prompt (number keys take, S skips; a piped run skips for the heart), and a run summary, plus the `--console`/fallback role. | done (Phase 6–9) |

## Data flow of one frame (as implemented)

1. `Program` builds the config and a `GameFactory`, then runs
   `RaylibFrontend.TryRun(config, factory.CreateCampaign)` — or
   `ConsoleFrontend.Run(factory.CreateCampaign)` when `--console` was passed or no window could
   be opened. Each run starts with a fresh campaign, so a replay is the same call as the first
   descent and always starts on floor 1 with full hearts.
2. The window opens on the title screen (`RaylibRenderer.DrawTitle`). The frontend owns the
   selection; confirming `DESCEND` calls the factory, resets the renderer's explored cells, shows
   the floor intro card for 1.6 s (Enter/Space skips it) and switches to the game screen.
   `TUTORIAL` opens `Screen.Tutorial`, which pages through Core's `Tutorial` list and hands over
   to `Screen.Practice` (Core's `PracticeTutorial`); `CARDS` opens `Screen.Codex`, the readable
   catalog, which the pause menu opens too.
   `CONTINUE` appears only while `SaveStore.HasRun` is true and rebuilds the saved run through
   `Campaign.Restore`. `LEAVE` or Esc closes the window; Esc during a floor opens the pause card
   instead, and Esc on a card is always "back".
3. The frontend reads its own keyboard API and translates input into one `GameCommand`.
   `GameCommands.Apply` performs it: `TryMove(direction)` or one of
   `TryBlink`/`TryVeil`/`TryDrag`/`TryThrowLantern`/`TryShove`. Walking is read through Core's
   `StepRepeat`: a fresh press is worth one direction, a hold repeats after 0.15 s once the
   previous step has finished, and sprint is a held Shift. No game rules live in the UI:
   `TryMove` decides the cadence and buffers at most one press.
4. The frontend measures real elapsed time and calls `GameState.Tick(deltaSeconds)`, which
   spends a buffered step, ages the sprint bar, and advances the killer at most one step. The two
   step fractions it updates drive the killer's slide and the player's walk-cycle frame; the
   explorer's *position* is its cell, never a fraction. It then drains the floor's events and
   hands them to `AudioDirector.Handle`, and asks the audio layer to follow the floor
   (`Update`: intensity from the killer's distance, the last-heart heartbeat).
5. When a floor's exit is reached, `GameStatus.Won` makes the frontend show the floor-cleared
   card; Enter opens `Campaign.Draft`, whose offers were generated on the spot from the run seed
   and the floor number. Taking a card folds it into `Campaign.Modifiers`; skipping banks a heart.
   R buys a fresh table first (`Campaign.TryRerollDraft`, priced 1/2/3 hearts and reset with the
   draft). Only then does `Campaign.TryAdvanceFloor()` build the next floor — with the hearts left and the
   held cards applied over its config. On the last floor the run summary card appears instead, and
   there is no draft. A lost run shows the end card over the last frame; R or Enter starts a fresh
   campaign. Quitting returns the run so the
   outcome can be printed.
6. The play screen is drawn by `PixelRenderer.Draw(state)`: the world goes into `PixelView`'s
   render texture at 16 px per cell (floor, thin wall faces, signature object, cast, the lightmap
   multiplied over it, then the weather particles), the texture is blitted to the window at an
   integer scale, and `RaylibRenderer.DrawOverlay(state, FloorChrome)` draws the floor's own HUD
   around that letterbox, in the compiled face. The title, the draft, the floor intro, cleared and
   run-summary cards are `PixelCards`; no screen is drawn in a system typeface, and
   `RaylibRenderer.Text` throws when no font was supplied rather than falling back.
   `ConsoleRenderer.Draw(state)` rebuilds
   the ASCII frame and writes it. Every renderer asks `GameState.IsVisible` what it may show, uses
   `EnemyGlow` to mark the killer's light, and keeps its own memory of explored cells.
7. Audio runs on the device's clock, not the frame's. `AudioDirector.TryCreate()` opens the
   stream (or returns null), `PlayFloor` arranges the floor's stems during the intro card, and the
   device callback mixes them into the buffer it is handed — intensity applied on the next bar,
   the duck under a stinger, the queued one-shots. `AudioEngine` never touches Raylib and
   `AudioDirector` never touches a rule, which is what lets the mixer be tested on a machine with
   no sound card.
8. The window runs at `Raylib.SetTargetFPS(60)`; the console loop sleeps 8 ms per frame. The
   game clock never comes from the frame rate — always from measured time passed to `Tick`.

## Game rules (Phase 5–6)

- **Placement.** A seeded shuffle picks the player's cell; the killer takes the furthest
  dead end (a leaf, so it can never cut off an objective); the exit and the keys take the
  next distinct cells. `GameConfig.Validate` guarantees there are enough cells.
- **Movement.** Walls are the only thing that blocks the player. Stepping onto the
  killer loses; stepping onto the exit wins only with zero keys left; a locked exit can be
  walked over (blocking it could soft-lock a corridor).
- **Enemy.** Dijkstra route to the player, cached and invalidated when the player moves. It
  waits `EnemyStepSeconds` before each step, at most one step per `Tick`.
- **Maze shape.** The graded Kruskal generator builds a perfect maze; `DeadEndBraider` opens a
  second exit at 55% of its dead ends (so a chase rarely ends in a corner) and
  `BraidedMazeGenerator` removes `ExtraLoops` further walls, so a sprint has somewhere to turn.
- **Visibility.** `IsVisible(cell)` = the player's own cell plus every cell within
  `VisionRadius` steps through open passages, so walls block sight. Once the killer is within
  `EnemyAuraDistance` steps, the cells within `EnemyAuraRadius` of it are visible too (the
  killer's own light, `EnemyGlow`). Recomputed when the player *or the killer* moves;
  renderers only darken what it reports.
- **Hearts.** The player starts each floor with `StartingHearts` (3) out of `MaxHearts` (3). A
  catch (the killer steps onto the player, or the player walks onto the killer) spends one heart
  and does **not** end the floor: the killer is teleported to the furthest reachable cell, dead
  ends preferred and keys and the exit avoided while possible, then stunned for
  `RespawnStunSeconds`; the player is invulnerable for `InvulnerabilitySeconds` (the killer waits
  beside their cell instead of sharing it, and a step into it is refused). Keys and the layout
  survive, so damage costs tempo rather than progress. At zero hearts
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
passages, so the extra loops are counted rather than assumed.

`MazeShape` narrows the grid first (see `MazeOptions.Shape`): cells outside the outline are closed
before any wall moves, so `Grid.IsSolid` is the model's own answer to "is there floor here?" and
`RemoveWall` refuses a wall with rock on either side. The generator counts passages against
`OpenCellCount`, not `CellCount`, and every placement (player, hunters, exit, keys) skips rock.
`MazeShapeTests` proves a shaped maze is one connected piece with `openCount - 1` passages, and
`ShapedFloorTests` walks the shipped catalogue and proves every key and the exit are reachable.

`DeadEndBraider` is tested by counting dead ends rather than walls: zero left at 100%, fewer at a higher share, a perfect
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
