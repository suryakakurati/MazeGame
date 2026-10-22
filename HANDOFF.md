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

## Project state at handoff (2026-10-22)

**The latest pass (pause, tutorial, rerolls and a fourth card wave) is in:** Escape now opens a
pause card (`Screen.Paused`, `PixelCards.Pause`, `ConsoleFrontend.PauseMenu`) with `CONTINUE` and
`QUIT RUN` instead of ending the run; the console restarts its `Stopwatch` on resume so a pause is
not charged to the hunters. `GameConfig.ForFloor` adds one heart to `CatchCost` for every five
floors below the first, so a catch costs 1/2/3/4 across the run. The casino's room card draws a
turning roulette wheel from primitives (`PixelCards.DrawRoulette`, driven by `GetTime()`). The draft
can be rerolled for hearts (`Campaign.DraftRerollCost` 1 then 2 then 3, `TryRerollDraft`, with
`TryAdvanceFloor`/`TryReincarnate` clearing the ladder) and the last heart is never a price. The
catalog is **thirty-nine cards**: `CardTier.SuperRare` exists for House Favour alone, and the fourth
wave adds fourteen trades led by **Ashen Bloom** — a catch lights every bloom within 1 step for 2
seconds through `GameConfig.BloomGlowRadius`/`BloomGlowSeconds`, `GameState.IsBloomGlowActive` and
`RecomputeVisibility`, with each copy adding a step of reach. The title's second entry opens a
**guided Tutorial** (`Tutorial`, `TutorialStep` in Core, `PixelCards.DrawTutorial` in the window,
`ConsoleFrontend.PrintTutorial` in the terminal): twelve pages covering the floor, the hunters,
hearts, every ability and the pause menu. **The suite is 868 tests, all green.**

**The pass before that (the ten-item polish list) is in:** rooms roll ten percent after floor 1 and
their door is lit within three steps (`GameState.IsSafeRoomDoorInSight`); Threshold Sense and
Latchkey add ten points to that roll; House Favour forces a casino on the next floor and pays 66%
on its first bet; player speed cards were halved and every heal card restores two hearts; the
Listener no longer ticks inside a safe room; a hunter also refuses to enter the room at all
(`CostOfEntering` prices its floor out of every route, and a wander skips it); the Stall previews
its cards (`RoomOption.Preview`); Shove reads all four neighbours; the lantern burns 8 s at range 5
and stops short of a hunter; and hunters only know the player inside a six-step aura
(`GameState.IsPlayerSensed`), wandering otherwise. **The suite is 853 tests, all green.**

**The pass before that (five powerup verbs and descent conditions) is in:** the player's tools are
Blink (Space), Veil (V), Drag (T), Lantern (L) and Shove (F), each with per-floor charges that
refill when a floor is built (`PowerupKind`, `PowerupCharges`, `GameState.TryBlink` and friends,
`GameCommands`). They replaced barriers and wisps, which survive as floor rules and tests but no
longer have an input. Floors 8 and 14 are gates: `Campaign.ConditionOffer` opens, `TakeCondition`
resolves it, and `TryAdvanceFloor` refuses to descend until it is; the eight rules live in
`DescentCondition` and `DescentConditionCatalog` and reach later floors through
`GameConfig.Conditions`. Regrowth and Fracture mutate the maze on timers behind a connectivity
check; Eclipse, Listener, Pack, Hunted, Mute and Drift are rule flags. The four cards built on
barriers/wisps were rewritten around charge deltas (`CardEffect`/`RunModifiers`), and The Gilded
Cage, The Foundry and The Moth Garden became powerup gimmicks. **The card system was then
overhauled**: the catalog is twenty-six cards, with ten rule-backed additions (free first catch,
revival, two-heart catches, magnetic keys, blink refunds, longer veil/drag/lantern,
extra hearts for vision), floor-banded draft weights and a guaranteed rare every fifth floor. The
HUD now shows the five powerup charges as keyed slots (`RaylibLayout.HudHeight` is 76).

**The pass before that (the score on the death screen, and a growing maze) is in:** a lost run now
ends on `PixelCards.Caught(font, floorNumber, floorCount, floorName)`, which prints "YOU MADE IT TO /
FLOOR 7 OF 20" and the floor's name. The card is drawn by `RaylibFrontend.DrawFrame` on
`Screen.RunLost` instead of by `PixelRenderer.Draw`, because only the campaign knows the run's
length; the renderer still draws the world and HUD under it. The shipped floors now grow across
the run — 15x15 for the first arc, 17, 19, 21 (The Long Gallery), the deliberately tiny 11x11
Warren, then 19, 21, 23 and 25 at the bottom — and
`FloorCatalogTests.Shipped_TheMazeGrowsAsTheRunDescends` pins that the Warren is the only shrink.
The suite is **703 tests**, all green, and `ShapedFloorTests` rebuilds every floor at its new size
and proves the exit and keys stay reachable.

**The pass before that (a playtest fix) is in:** the music no longer glitches — `AudioDirector.PlayFloor`
starts a worker instead of synthesising on the game thread, `Update` adopts the finished score
without blocking (measured 2.4 ms to arrange, 0.87 ms slowest frame), and the device callback
clears its buffer when there is no director. Walls are readable: the palette ladder moved to floor
0.18 / wall 0.50 / rim 0.68 / shadow 0.05, the band is 6 px (`WallStrips.Thickness`, with
`WallStrips.RoleFor` stating the cross-section once), and remembered cells are lit at 0.55 instead
of 0.40 — `WallContrastTests` now demands a 2.2x drawn-band ratio and a floor left between two
bands. Holding a direction walks as soon as the step ends, because `StepRepeat.Next` takes the
last step's duration (`GameState.LastPlayerStepSeconds`) and no longer waits out an extra 0.15 s;
a tap is still one tile. Finally, floors have outlines: `MazeShape` grows a `Blob` or an `Ellipse`,
`Grid` marks the rest solid (`IsSolid`, `OpenCellCount`, `RemoveWall` refuses rock), the generator
only opens walls between floor cells, and `GameState` places everything on floor cells. Eight
floors use a shape; `MazeShapeTests` and `ShapedFloorTests` prove every key and the exit stay
reachable on the real catalogue.

**The pass before that (the hunt escalates, and so does the music) is in:** floors 1–9 carry one hunter,
10–15 two and 16–20 three, each with its own cell, route, clock and stun (`GameState.Enemies`,
`HunterView`; a catch throws only the culprit and stuns them all, and the footer grows one pip per
hunter). The player's cadence is a constant 0.26 s a cell on every floor — cards and sprint are the
only speed-ups — while hunters start at 1.20 s and ramp to 0.22 s, so the player starts faster and
must draft speed to stay ahead. `FloorCatalogTests` scores a floor as hunter-steps per second, so
difficulty is a product of enemy pace and hunter count. `GameConfig.NearSightRadius` (default
2, clamped by the torch) lights the cells around the player through walls, and the pixel renderer
draws a south/east wall face itself when the neighbour is unknown; that pair is the fix for the
"wall with a gap in it" look. The score is twenty pieces now: `MusicTheme` carries a `MusicStyle`,
its own `Melody` and a `StepsPerBar`, and `ScoreRenderer` arranges each style with its own
instruments, rhythm and counterline. The five-motif cycle is gone.

**The pass before that (twenty distinct floors, sound, one typeface) is in:** every floor is drawn as its
own place rather than a recolour — `Theme` carries a `FloorMotif`, a `WallMotif`, a `FeatureKind`
and an `AmbientKind`, `MotifTiles` draws the new grammars, and every floor's atlas holds its own
signature object and carved emblem with a maker's mark. `--check` requires six structural
differences between neighbouring floors. `FloorStyle` adds a per-floor accent, one of twelve
weather behaviours, a light cast and a fog density, so the lantern, the motes and the letterbox all
belong to the floor. All twenty tracks are synthesised from twenty parameter sets with five shared
motifs, and a test refuses two identical sets. Every screen — title, HUD, footer, draft and the
campaign cards — is set in the compiled 5x7 face, and no UI file calls `Raylib.DrawText`.

**The pass before that (camera, wall contrast, a resizable window) is in:** the play screen no longer
draws the whole floor. `PixelLayout` sizes a viewport from the floor's vision radius (the lit radius
plus one ring, odd, 9–13 cells) and `PixelCamera` follows the player, clamped to the maze, so a maze
cell is 64 screen px instead of 32 and the fog edge is off screen. A card that raises vision zooms
the camera out by rebuilding the render target. Walls were flattening into the floor, so the palette
ladder lifted the wall body to 0.37 and the rim to 0.54 against a 0.20 floor, `WallStrips.Face`
paints the rim and the near-black shadow after each family's texture, and `WallContrastTests` holds
the ratios. The window opens at 696x872 and is resizable/maximisable (`Fit` re-derives the integer
scale every frame, up to 6x).

**The previous pass (theme families, the font, the campaign cards) is in:** the three floors were no
longer recolours. A theme now names a `TileFamily` — `OrganicCave` (boulders, moss), `WrongOrganic`
(matted roots, veins, membrane) and `WorkedStone` (brick courses, grout, carved bands) — and
`TileGrammar` dispatches floor, wall, wall face and props to `CaveTiles`, `WrongOrganicTiles` or
`WorkedStoneTiles`, three files that share no drawing code. `tests/.../ThemeFamilyTests` compares the
themes on palette *indices*, so "same shapes in a new hue" is a red test. The compiler also emits
`font.png` — a 5x7 bitmap face for every printable character plus the heart and pip icons, in its
own atlas and its own manifest section — and `PixelFont`/`PixelCards` draw the floor intro,
floor-cleared, run-summary and caught cards in it instead of the system typeface. `RaylibRenderer`
no longer draws those four cards, and the same face now sets the title, the draft and the HUD:
`PixelRenderer` hands its font to the chrome renderer in its constructor, which is the only place
that wiring can go — the chrome's `Text` throws without a font.

Phases 1–6 are complete, including the pixel UI chrome (the draft screen, the HUD and the footer);
solution scaffold, the three game/test projects (plus `tools/AssetForge`,
a non-shipped console tool), shared build settings, style
rules, all documentation, both graded algorithms (`DisjointSet` with path compression and
union by size; `MinHeap<T>` with sift-up/sift-down), the maze pipeline (`IRandom`,
`Direction`/`Cell`/`Grid`, randomised-Kruskal `MazeGenerator`, `DijkstraPathfinder`), and
a **playable game** in a Raylib window: `GameConfig`, `GameStatus`, `GameState` (placement,
movement, keys, locked exit, barriers, timed killer AI, visibility/fog, win/lose),
`IRenderer` with `RaylibRenderer` and `ConsoleRenderer`, the `Frontends/` + `Input/` split,
`GameFactory` (builds each run), and `Program` as the composition root. The window opens on a
title screen and the end card offers `R / ENTER: DESCEND AGAIN`, which builds a fresh cave and
game in the same window; the console has the same title and `[R] descend again` prompt.

**Roguelike Phase 1 (hearts) is also in:** the player starts with three hearts (hearts have no
ceiling since the Echo and rooms can heal), a catch spends one and throws the killer to the
furthest reachable cell (dead ends preferred) where it is stunned for 1.5 s, while the player gets
1.5 s of invulnerability; keys, barriers, the wisp and the layout are kept, and only the catch that
empties the last heart ends the run. The window shows the hearts in the footer with
a red damage flash, and the console header prints `hearts n/n`. `FUTURE.md` holds the nine-phase
plan for the rest of the roguelike (campaign, cards, movement, generated pixel art, synthesised
audio, themes); `PROGRESS.md` says which phase is next. The canvas has one hard-won rendering
rule: the lantern falloff is **lighting**, painted before the cell contents, never after — drawing
it last made keys invisible even though the rules said they were in view.

**Phase 2 (floors and campaign) is in too:** `Campaign` sequences the shipped twenty-floor
`FloorCatalog` (The Shallows, Rootbound, Drowned Halls — 3/3/4 keys, killer 0.60/0.54/0.48 s),
owns the current one-floor `GameState`, and carries hearts between floors via each floor's
`StartingHearts`. `GameStatus.Won` means "this floor's exit"; the run's result is
`CampaignStatus` (`InProgress`/`Won`/`Lost`), and `TryAdvanceFloor` is the only way forward. The
window has floor intro/cleared/summary cards and the console prints floor headings; both
frontends take `Func<Campaign>`. `GameFactory.CreateFloor` derives a floor's seed as
`seed + (number - 1) * 7919`, so a seeded run reproduces exactly with different caves per floor.

**Phase 3 (the card draft) is in as well:** a cleared floor that is not the last opens three
seeded offers, and `Campaign.TryAdvanceFloor` refuses to advance until one is taken or the draft
is skipped for a heart. `Card` records carry a `CardEffect` of numbers; `RunModifiers` composes
the held cards over each floor's `GameConfig` and clamps them, so **no card is an `if` inside
`GameState`** — the rules are untouched, only the floor's inputs change. `GameConfig` is now a
record, so that composition is a `with` expression and cannot forget a field. The window has a
card grid (tier colour, wrapped face sentence, the run's totals, `next floor: n hearts`); the
console prints the offers. Two floor rules were added for two cards (`LureStunSeconds`,
`LureVisionPenalty`), and a wisp's own light now goes out when it expires or is consumed.

**Phase 4 (movement feel) is in as well:** stepping is timed. `GameConfig.PlayerStepSeconds`
gates the *next* step, not the cell change, so every rule still sees one cell at a time; the base
config keeps 0 (instant) and each shipped floor sets 0.11 / 0.10 / 0.09 s against the killer's
0.60 / 0.54 / 0.48. One direction pressed during a step is buffered and spent when it ends. The
window reads **walking through Core's `StepRepeat`** (a tap is one tile, a hold repeats; the aim
cursor stays tap-based) and **Shift as sprint**: a
stamina bar, 1.4× speed, a 25% refill gate, refused while aiming or on an instant floor. The
killer slides between cells using `PreviousEnemy`/`EnemyStepFraction`; the **explorer snaps to its
own cell** (see the 2026-10-20 pass below), and `PlayerStepFraction` is now a walk-cycle clock, not
a position.

**This pass (snapped movement, the rest of Phase 5) is in:** the window draws the explorer on its
own cell, so one press is one tile — the slide made the grid uncountable while running. The
compiler now produces the full tileset and cast: `Autotiler` builds the 16 edge and 47 blob wall
tiles from open-side masks (corner bits canonicalised; the 47 is asserted at startup), `Rig`
draws the explorer's 4x4 poses, the killer's 4x3, the key and wisp (3 frames each), the barrier
(fresh/cracked) and the arch (locked/open). Each theme's atlas is 105/104/106 tiles at 128x208–224
with every tile named in `manifest.txt`. **Phase 5's exit is met**; the font atlas, pictograms, UI
chrome and the two extra families are deliberately left for the phases whose screens and floors
need them.

**The previous pass (cards stacking, movement cards, sprite compiler slice 1) is in too:** two caps that
ignored the run's cards are fixed — `Campaign.HeartsOnNextFloor` caps at
`floor.MaxHearts + modifiers.MaxHearts`, and `GameFactory.CreateFloor` validates the carried
hearts against the *composed* floor (a card may raise the ceiling; a carry no card justifies still
throws). `CardEffect` gained `PlayerStepScale`, `InvulnerabilitySeconds`, `SprintStaminaScale` and
the momentum knobs, and `GameState` grew the momentum rule (straight steps ramp the step time,
a turn costs 15% for a 2 s window). The catalog is now **13 cards**, including Light Feet,
Momentum, Sprint Lungs and Hollow Bones. `tools/AssetForge` is a new console project: the sprite
compiler's first slice. It writes three theme atlases and a manifest to
`src/MazeGame.UI/Content/Generated`, and `--check` fails on an invalid theme, two neighbouring
themes that are too close in colour or shape, a non-deterministic compile, or a committed atlas
that drifted. **Nothing in the game reads the atlases yet.**

The maze the UI plays is shaped by two decorators over the graded perfect generator:
`DeadEndBraider` opens one wall at 55% of the dead ends (57 → 18.8 per 15x15 map, so a chase
rarely ends in a corner) and `BraidedMazeGenerator` removes `ExtraLoops` (8) more interior
walls, so a barrier can redirect the killer rather than only stall it. A barrier now **seals**
its cell for 10 s (the killer routes around it, and tears it down in 3 s only when it is the
only way), and a wisp **lure** (2 charges, 6 s) makes the killer chase whichever of the player
and the wisp is cheaper by Dijkstra. The killer carries a distance-gated aura (visible within 6
steps, radius 2), and the window is drawn as a bioluminescent cave in green/pink/cyan, with the
team's reference art in `references/image.png` as the mood board. 528 tests pass, including
perfect-maze invariants over 50 seeds, shortest-path optimality against an independent BFS, all
Phase 5 rules, the seal and tear-down rules, the wisp's target
choice and lifetime, the dead-end braid, the fog and aura rules, the replay contract in
`GameFactory`, the heart/catch/respawn rules in `GameStateHeartTests`, the campaign's floor
sequence and carried hearts, the floor catalog's difficulty arc, **both halves of every card**,
the modifier clamps, the draft's determinism and its four guards, the campaign's draft gate,
**timed steps, the one-input buffer, sprint and its stamina gate**, the momentum ramp and turn
penalty, the heart-ceiling composition (`HeartsOnNextFloor` and the factory's composed check), the
sprite compiler's canvas, palette, PNG round-trip, atlas determinism and `--check` drift detection,
and a long random-play invariant test that places both placeables and still proves the killer
always has a route.
Verified this session: build 0 warnings/0 errors, **574/574** tests, `tools/AssetForge -- --check`
→ `CHECK_OK themes=3 tiles=327` (the four thin wall faces per theme are new), and the real pixel
renderer in a real window: 200 draws after a 70-step walk at **1.03 ms median**, 1.34 ms mean and
3.28 ms p95, the canvas 62% painted with 592 distinct colours at 2x, **0** lit pixels in the 136
unexplored cells, **0** in the letterbox, the explorer's cell at 598/765, all three themes loading
and drawing, and the aim reticle at 690/765. Screenshots for a human: `/tmp/mzgame-pixel-*.png`.
The shipped window idled 9 s with an empty log. Previously: the real vector renderer through the
whole campaign and both draft layouts plus the movement probe (236 frames, `DRAFT_OK`,
`DRAFT4_OK four=4 two=2`, `MOVEMENT_OK step=0.11s halfway=0.50 sprinting=True stamina=0.91
idleStamina=1.00`), a 3,217-frame console run ending on the loss summary. The campaign walker now ticks between steps as the frontend does, and
the killer still took a heart from it during the run. Earlier: cards reaching the next floor
(`maxHearts 4`, `step 0.422`, `barriers 4`), 160 and 224-frame campaign passes, 5,402 console
frames, 1,264 frames through three hearts, and 100 frames collecting a key. The window's pixels
could not be captured on this machine (no GUI framebuffer available to the agent), so one human
look is still owed — the draft screen, the snapped explorer, the sliding killer and the stamina
bar all need judging by eye (see `PROGRESS.md` → Known Issues).

**Roguelike Phase 6 (the pixel renderer) is in progress: slice 1 is done.** The play screen is drawn
from `src/MazeGame.UI/Content/Generated` into a 16 px-per-cell virtual canvas at an integer scale:
`PixelLayout` (geometry and `Fit`), `PixelView` (render target and letterboxed blit), `AtlasManifest`
+ `AssetPaths` + `TileAtlas` (loading, and a loud failure for missing art), `TileMask` (wall faces,
cross-checked against the compiler), `PixelRenderer` (world, cast, lightmap, reticle). `WallStrips`
in `tools/AssetForge` compiles the thin wall faces this maze needs. **Slice 2 is next:** `Glyphs`,
`Pictograms` and `UiForge` in the compiler, then the title, intro, draft and summary screens drawn
inside the canvas with the pixel font — the vector renderer still draws those, which is the one
visible inconsistency left in the phase. `PROGRESS.md` → **Next Steps** has the ordering and
`FUTURE.md` §11 has the phase gates.

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

- **The canvas is never scaled by a fraction.** `PixelLayout.Fit` returns an integer 1–4; pixel art
  resampled at 1.5 (or "sharpened" afterwards) is the one thing the pipeline exists to avoid. If the
  window is too small, the canvas is clipped at 1x rather than squeezed.
- **A wall between two cells is painted once.** `PixelRenderer` draws a cell's north and west faces
  plus the outer rim on the last row and column. Drawing all four would paint two 5 px bands per
  wall, 16 px apart — the bug that looked like "walls in the wrong place" in the first draft.
- **Fog must not dim the reticle.** The aim cursor is drawn after the canvas is scaled (window
  pixels), so the light multiply cannot dim it; anything added inside the canvas is fogged.
- **`Raylib.LoadImageFromScreen` can return the frame before the one just drawn** (double
  buffering). A screenshot check has to draw twice before reading, or it silently inspects stale
  pixels — that cost an hour of chasing a reticle that was drawn correctly all along.
- **`dotnet` may not be on `PATH`.** On the machine where Phase 1 was set up, the SDK
  was installed into `~/.dotnet`, so use `~/.dotnet/dotnet build`. Run
  `dotnet --version` first; you need an 8.0.x SDK.
- **The repository path contains a space** (`.../cs2 projecgt/mazegame`). Quote paths
  in shell commands and avoid unquoted `$(...)` expansions around it.
- **Tests are discovered per folder.** Add a test class under
  `tests/MazeGame.Tests/Maze/`, `Pathfinding/`, `Game/` or `Assets/` to match the project
  folder it covers. `dotnet test --filter FullyQualifiedName~DisjointSet` runs one class.
- **The UI depends on Raylib-cs 8.1.0**, which carries per-platform native binaries; a fresh
  restore downloads them. `RaylibFrontend.TryRun` returns `false` when no window can be
  opened and `Program` then runs `ConsoleFrontend`, so the game still works over SSH — keep
  that fallback alive. Do not add packages unless the task needs them; when you do, list the
  exact version in `ARCHITECTURE.md`.
- **The window loop owns `BeginDrawing`/`EndDrawing`.** `RaylibRenderer.Draw` only fills in a
  frame; calling Raylib drawing outside a bracket crashes. Anything that must run per frame
  belongs in `RaylibFrontend.RunLoop`, not in the renderer.
- **`tools/AssetForge` writes committed files.** Atlases live in
  `src/MazeGame.UI/Content/Generated` (`*-tiles.png` + `manifest.txt`) and **are** committed, so a
  fresh clone needs no tool step. Each atlas is 8 tiles to a row and holds, in order: floor
  variants, `wall`, `edge-0..15`, `blob-0..46`, then the cast (`player-<facing>-<frame>`,
  `killer-<facing>-<frame>`, `key-n`, `wisp-n`, `barrier`, `barrier-cracked`, `arch-locked`,
  `arch-open`) — 315 tiles across the three themes. After changing a tile routine, a theme or the
  palette, run `dotnet run --project tools/AssetForge -- --all` and commit the diff; `--check`
  (also a test) fails on a drifted atlas, a non-deterministic compile, two neighbouring themes
  that are too close in colour or shape, or an invalid theme. The game does **not** read these
  files yet — the pixel renderer phase loads them, so do not wire them into `RaylibRenderer` early.
- **Autotile masks are fixed bits.** N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128, and the tile
  names carry the mask (`edge-5`, `blob-37`), so the renderer must build masks in that order. A
  diagonal only matters when both of its edges are open — that canonicalisation is what makes 47
  blob tiles rather than 256, and `Autotiler` asserts the count at startup.
- **The explorer must snap, the killer may slide.** The player's figure is drawn at its cell, not
  between cells: one press is one tile, which is how the grid stays countable under pressure. The
  step fraction is still published for the walk cycle, never for position.
- **The art is one palette block plus procedural drawing.** Colours and the drawing helpers live
  in `RaylibRenderer`; the look is a bioluminescent cave (green/pink/cyan on near-black teal,
  rounded stone, glowing lichen, hooded explorer, spindly killer). Every detail comes from a
  hash of the cell (or of the mote/grain index and clock), so do not introduce `Random` or
  per-frame state for it, and do not load textures yet — the compiled atlases exist but nothing
  in the game reads them (that is the pixel-renderer phase).
- **Walls are drawn in two passes.** `DrawCellBase` paints floors and the dark rock of every
  wall; `DrawCellDetails` then paints the lit rim, moss and lichen. A wall is drawn by both cells
  that share it, so a single pass would let the neighbour's dark stroke cover the first cell's
  rim. Keep that split if you add wall decoration.
- **Two geometries, and they are different on purpose.** `RaylibLayout` owns the *window*: the
  vector maze is 40 px a cell with a 60 px HUD strip and a 44 px hint strip, and the window is
  opened at the pixel viewport's size (696x872 for a 9-cell viewport at 4x) and is resizable.
  `PixelLayout` owns the *canvas*: one 16 px atlas tile per cell inside a viewport that follows the
  player, blitted at an integer scale (1 to 6). Changing `RaylibLayout.CellSize` means re-checking
  the figure sizes in `RaylibRenderer`; changing the pixel side means re-running AssetForge.
- **The camera owns the view, the floor does not.** `PixelCamera.For` centres the player and clamps
  to the floor, and `PixelRenderer` culls to it — every world pass must go through `CanvasX`/
  `CanvasY`, never `PixelLayout.CellX` directly, or a cell will be drawn at the wrong place once the
  camera has moved.
- **Fog of war is a rule, not a drawing effect.** `GameState.IsVisible(cell)` is the only
  source of truth (own cell + `VisionRadius` steps through passages, cached, recomputed on
  movement). Renderers may decide how to darken, and each keeps its own explored-cell memory;
  never let a renderer invent visibility, and never put its memory in Core.
- **Game rules live in `GameState`, not in `Program`.** The loop only maps keys to
  `TryMove` / the aiming commands / `Tick` and draws. If you are tempted to add a
  rule to the renderer, add it to Core plus a test instead.
- **Aiming is a Core mode, not a UI mode.** `PlacementMode` (barrier or wisp) freezes the
  clock, but no frontend binds it any more: the player's tools are the five one-press verbs, and
  the aiming API survives only for floor rules and tests. It still freezes the
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
- **`Tick` performs at most one step per hunter** and banks at most one interval for each, in
  placement order, so a long frame cannot teleport any of them. With two or three hunters a frame
  may move each of them once; the loop stops early if a catch or a cleared floor ended the game.
  Tests advance time with repeated `Tick(1.0)`.
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
- **Movement is timed by a cooldown, not by moving the cell late.** `TryMove` changes the cell at
  once and then refuses the next step until `PlayerStepSeconds` has passed; `Tick` spends one
  buffered direction when it does. Do not "fix" this by delaying the cell change: every rule that
  reads a cell (keys, the arch, a catch, two path searches) would need a between-cells answer.
  Interpolation is one-way, through `PreviousPlayer`/`PlayerStepFraction` and the killer's pair.
- **A step time of zero means instant**, and that is the seam the rule tests and tools use. The
  shipped timed floors get their value from `FloorDefinition.PlayerStepSeconds`, not from the base
  `GameConfig`. Sprinting is impossible on a zero-step floor by design.
- **Sprint is a request.** The frontend calls `SetSprinting` every frame and `IsSprinting` decides;
  do not let a frontend scale a step time itself. The bar only drains while a step is running:
  holding Shift on the spot has to stay free, or the player sets off with nothing left. Walking goes
  through Core's `StepRepeat` — a fresh press sends one direction, a repeat waits for 0.15 s down
  *and* for the step to finish, and a release resets it — so a tap is one tile and a hold walks at
  the floor's cadence. The aim cursor stays tap-based: a held cursor key flies across the map.
  Never go back to sending the held direction every frame: that refills the one-slot buffer and a
  slow tap moves twice.
- **Cards change a floor's inputs, never its rules.** A card is a `Card` record with a
  `CardEffect`; the run's totals live in `RunModifiers`, are clamped, and are applied by
  `GameFactory.CreateFloor` over `GameConfig.ForFloor`. If a card seems to need a branch in
  `GameState`, add a validated `GameConfig` number instead (that is what `LureStunSeconds`,
  `LureVisionPenalty` and the momentum knobs are). `GameConfig` is a record on purpose: compose
  it with `with`, never with a hand-written copy. Scales (`PlayerStepScale`, `EnemyStepScale`,
  `SprintStaminaScale`) multiply; durations and counts add.
- **A card may raise a floor's heart ceiling, so never cap against the definition alone.**
  `Campaign.HeartsOnNextFloor` uses `floor.MaxHearts + modifiers.MaxHearts`, and
  `GameFactory.CreateFloor` validates the carried hearts against the *composed* config (it still
  throws when nothing could justify them). Capping either place at `floor.MaxHearts` silently eats
  a heart a card paid for — that was a real bug, see `PROGRESS.md`.
- **Momentum is off at zero, and it has to stay that way.** `MomentumMaxBonus`,
  `MomentumRampPerStep`, `TurnPenaltyScale` and `TurnPenaltySeconds` default to 0 and only the
  `momentum` card sets them. `GameState.TrackMomentum` runs *before* the next step's duration is
  computed, so the turn (not the step after it) pays the penalty; with the zeros the whole path is
  skipped and a run without the card keeps exactly the old cadence.
- **The draft is a gate in `Campaign`, not a screen.** `Campaign.Draft` opens lazily on the first
  read after a floor is cleared, its offers are seeded from `{run seed, floor number}`, and
  `TryAdvanceFloor` refuses while it is unresolved. `Draft`'s mutation is internal: resolve it
  through `TakeCard`/`SkipDraft` or the run's modifiers will not hear about it.
- **Do not offer a card the run cannot pay for.** `Card.DraftsNeededAfterThisOne` withholds a
  card whose drawback lands in a later draft; `Lucky Find` needs two, so it cannot appear in the
  three-floor run. The same place enforces no duplicates, no contradictory pair and the two-card
  vision cap (`DraftFactory`).
- **The UI must not contain game rules.** If the UI needs data, add a read-only
  member to `GameState`; do not reach into maze internals.
- **The lantern is a rule, not a drawing decision.** `GameState` owns `IsVisible`, including the
  near-sight spill; a renderer may only darken what it is told. If a wall looks wrong, the first
  question is whether the cell on its other side is visible — the renderer paints a wall from the
  eastern/southern cell's north/west face, and falls back to painting it from the near side when
  that neighbour is unknown. Do not fix a rendering gap by widening `NearSightRadius` in the
  renderer: the constant lives in `GameConfig` for a reason.
- **`Raylib.DrawTriangle` culls clockwise winding.** Every triangle in the old renderer was wound
  that way, so the heart's point, the explorer's hood and the barrier thorns were invisible — a
  bug that survived several passes because the shapes around them still read as the object. Wind
  them counter-clockwise (left, point, right) and be suspicious of any triangle that draws
  nothing.
- **Hunters are a list, and every rule loops.** `GameState.Enemies` is the snapshot renderers read;
  `Enemy` is only the first hunter kept for older callers. `IsEnemyStunned` means *all* of them are
  held. A catch throws only the culprit and stuns everyone, and another hunter's cell costs
  `HunterStackCost` so two rarely stand together. A new rule that touches the killer has to be
  written per hunter, not against `_hunters[0]`.
- **Music is content per floor.** A new floor needs a `MusicStyle`, a `Melody` and (optionally) a
  meter; the tests refuse a reused melody, fewer than eight styles, two adjacent floors sharing a
  style, or a meter other than 12/16/20. Render cost is 170–400 ms (debug) once per floor, during
  the intro card; the loop's rhythm and lead are edge-faded because a drum can end mid-hit.
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
