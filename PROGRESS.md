# PROGRESS

Single source of truth for the state of the project. Update this file at the end of
every work session (see the handoff protocol in `HANDOFF.md`).

Last updated: 2026-10-21 (third playtest: the five-verb powerup kit, and descent conditions chosen
at gates on floors 8 and 14)

## Current Status

The solution builds with zero warnings, `dotnet test` is green (748 tests), and the game runs
**in a Raylib window** as a **twenty-floor campaign with a card draft between floors, five
powerups and timed steps**: the title leads to a floor intro card, then a seeded maze (15x15 at
the top, 25x25 at the bottom) drawn as a bioluminescent cave — fog of war, braided dead ends,
keys, an exit, and a killer (or three) that hunts the player with Dijkstra. **Every floor refills
five one-key verbs** — Blink, Veil, Drag, Lantern and Shove — and **floors 8 and 14 are gates**
where the run takes one of two **descent conditions** for the rest of the descent. **The player has three hearts**: a catch costs one, throws the
killer to a distant cell and stuns it, and keeps every key and barrier; hearts carry from floor to
floor, so the run is lost only when the last heart is. Walking is now a **cadence** — a constant 0.26 s per
cell, with cards and sprint as the only speed-ups — while hunters start at 1.20 s and ramp to 0.22 s,
so the player starts well ahead but has to draft speed to stay ahead. One buffered input and **sprint on Shift** (a
1.2 s stamina bar). The explorer **snaps to its tile** (one press, one tile, so the grid is
countable while running) and the killer **slides** between its cells. Clearing an arch opens a **draft of three cards — take one or skip for a heart** — and
the twentieth arch wins the run. The terminal view offered by `--console` (and used automatically when
no window can be opened) plays the same campaign in ASCII. The catalog holds **twenty-three
cards**: the four movement cards, the four powerup cards and a second wave that changes a catch,
a key pickup, a powerup's duration or a revival, with a guaranteed rare on every fifth floor and
rarer draws late in the run. The HUD shows the five powerup charges as a row of keyed slots. The **sprite compiler is complete to its Phase 5 exit**: every
committed atlas under `src/MazeGame.UI/Content/Generated` holds the full tileset (floor variants,
wall, four thin wall faces, 16 edge and 47 blob tiles) and the whole cast, checked by `--check`.
**The floor is drawn from those atlases**: a 16 px-per-cell virtual canvas, blitted to the
window at an integer scale, with a per-cell lightmap for the lantern and the fog. **Every floor is
now drawn as its own place**: twelve floor motifs, ten wall motifs, a signature object and a
per-floor weather layer, plus a themed HUD with the floor's emblem, name and gimmick. **Every
screen is set in the compiled pixel face** — title, HUD, footer hints, draft and the four campaign
cards — so nothing falls back to the system typeface.
**The game now has sound, and no audio files**: every effect and all twenty floor tracks are
synthesised from code — a 22-recipe one-shot bank, twenty pieces with their own melodies, styles
and meters, a Schroeder reverb and an intensity mix that follows the nearest hunter. A device is
opened through
Raylib's stream callback; with no device the game is silent rather than broken.

What changed in this pass (the five-verb kit and the descent gates):

- **The old barrier/wisp kit is retired from the player's hands.** `PowerupKind` and
  `PowerupCharges` carry five verbs with per-floor charges — Blink (2), Veil (3), Drag (1),
  Lantern (1), Shove (1), taught one at a time on floors 1, 2, 4, 6 and 8 and deepened twice.
  `GameState` implements each as a rule: `TryBlink` removes the wall and moves through it,
  `TryVeil` channels 0.6 s and hides for 3 s, `TryDrag` scales the hunter clocks 0.4x then 1.1x
  for 5 s, `TryThrowLantern` moves `LightOrigin` up to four cells, and `TryShove` pushes a hunter
  two cells and stuns it for 2 s while giving every other hunter its next step. A charge is spent
  only on success. Both frontends bind Space/V/T/L/F, and the HUD prints the five counts.
- **Eight descent conditions, chosen at two gates.** `DescentCondition` + `ConditionOffer` +
  `DescentConditionCatalog` (names, one-sentence rules, seeded two-card offer) and
  `Campaign.TakeCondition`; `TryAdvanceFloor` refuses to pass floor 8 or 14 until the gate is
  resolved. Regrowth seals an empty dead end every 18 s, Fracture opens one wall and closes
  another every 22 s behind a connectivity check, Eclipse drops vision for 6 of every 16 s,
  Listener makes the hunt turn-based, Pack moves every hunter on one clock, Hunted makes the
  first hunter always visible and 20% faster, Mute removes one rotating tool and adds a heart,
  and Drift moves the exit after every key. The chosen rules ride `GameConfig.Conditions` and
  are shown on the HUD and intro card.
- **Content follows the kit.** The four cards built on barriers/wisps (Deep Roots, Thornwright,
  Bait, Smoke Wisp) were rewritten as Blink Step, Long Veil, Lantern Oil and Quick Hands;
  `CardEffect` and `RunModifiers` now compose the five charge deltas, clamped at zero. The Gilded
  Cage, The Foundry and The Moth Garden trade their seal/wisp gimmicks for "no blink", "shoves are
  cheap" and "lanterns are cheap".
- **The cards were then overhauled.** The catalog grew from thirteen to twenty-three with ten
  rule-backed cards: Featherfall (first catch free), Greedy Grasp (magnetic keys), Bloom (one
  revival per floor), Blink Ledger (a key returns a blink), Ghost Veil (longer veil), Long Drag
  (longer slow and hangover), Wide Light (longer throw), Glass Cannon (faster but catches cost two
  hearts), Debt of Sight (two more hearts for two less vision) and Slow Pursuit (slower killer,
  one more key). `GameConfig` and `GameState` gained the hooks (free catch, revival, catch cost,
  magnetic pickup, blink refund, veil/drag durations, lantern range), and `RunModifiers` composes
  them. `DraftFactory` now weights tiers by floor band and guarantees a rare on floors 5, 10, 15
  and 20 when one is eligible, so a rare is an event rather than a lucky draw. The HUD replaced
  the tiny one-line charge readout with five glass slots that show each key, its count, and a
  glow while that verb is active.
- **Tests for every new rule.** `GameStatePowerupTests` (13), `GameStateConditionTests` (9),
  `CampaignGateTests` (3) and `GameStateCardRuleTests` (8) pin both halves of each verb, each
  condition's on/off behaviour, the gate refusal, the seeded offer and each new card hook;
  `RunModifiersTests`, `CardCatalogTests` and `DraftFactoryTests` cover the card pass and the
  banded draw. 45 new tests (748 total).

What changed in the previous pass (the run's score, and a maze that grows):

- **The death card names the floor the run ended on.** `PixelCards.Caught` now takes the floor's
  number, the run's floor count and the floor's name, and prints `YOU MADE IT TO / FLOOR 7 OF 20`
  with the name under it. The card moved out of `PixelRenderer.Draw` and into the frontend's
  `DrawFrame`, where the campaign — the only object that knows how long the run was — is in scope;
  the renderer still draws the world and the HUD underneath, so the death screen shows where the
  player fell. The console frontend already printed the same two numbers.
- **The maze grows as the run descends.** The shipped floors step up instead of returning to 15x15
  after the Gallery: 15x15 for the first arc, then 17, 19, 21 (The Long Gallery), the deliberately
  tiny 11x11 Warren, then 19, 21, 23 and 25 at the bottom. `FloorCatalogTests` pins the ladder —
  the first floor is 15x15, the last is 25x25, and exactly one floor (The Warren) is smaller than
  the one before it — and `ShapedFloorTests` rebuilds every floor and walks to its exit and keys.
  The largest, The Deep's 25x25 blob, opens 371 cells and builds in about 1 ms.

What changed in the previous pass (Phase 9 — the difficulty curve and the score):

- **The hunt escalates by adding hunters, not by turning one dial.** `GameConfig.HunterCount` and
  `FloorDefinition.HunterCount` put **one hunter on floors 1–9, two on 10–15, three on 16–20**.
  `GameState` keeps an internal list; each hunter has its own cell, route, step clock and stun, so
  two of them are not one faster monster. `GameState.Enemies` publishes `HunterView` snapshots
  (cell, previous cell, step fraction, stunned) and `Enemy` is kept as the first hunter, so every
  existing rule test still means what it did. A catch throws only the hunter that made it and
  stuns all of them; a hunter prices another's cell as four steps so two rarely stack; the music
  follows the nearest one. The footer draws one red pip per hunter, so the count is visible.
- **The player's cadence is flat; the hunt is not.** Every floor uses
  `FloorCatalog.PlayerCadenceSeconds` (0.26 s a cell), so speed comes only from cards or sprint.
  Hunters start at 1.20 s and ramp to 0.22 s, so floor 1 is a clear player advantage and the last
  floor's base hunter outpaces the base player. `FloorCatalogTests` pins the constant, the start
  advantage, the late hunter advantage, and scores the descent as hunter-steps per second.
- **The lantern spills onto the rock.** `GameConfig.NearSightRadius` (default 2, capped by the
  torch) adds the cells within straight-line distance of the player to the visible set, walls or
  no walls. That is the missing piece behind the "wall with a hole in it" look: wall faces are
  drawn by the eastern/southern cell, and a cell across a wall was never visible. The renderer
  also draws a south/east face itself when that neighbour is unknown, so the two rules together
  paint every wall once and none not at all. A vision-1 floor gets a one-cell spill; a blind one
  gets none.
- **Music is twenty pieces, not one piece twenty times.** `MusicTheme` gained a `MusicStyle`
  (drone, lullaby, procession, chant, pulse, waltz, nocturne, gamelan, machine, requiem), a
  **per-floor melody** and a meter (`StepsPerBar`: 16, 12 for 3/4, 20 for 5/4). `ScoreRenderer`
  arranges each style with its own instruments, bass, percussion and counterline; the shared
  five-motif cycle is gone. Measured across the twenty: closest pair differs by 0.137 mean per
  sample, every style is used, no neighbouring floors share one, and the loop seam is now
  < 0.003 on every track.
- **A latent culling bug surfaced while adding the pips.** `Raylib.DrawTriangle` culls clockwise
  winding, and every triangle in the game was wound that way: the heart's point, the explorer's
  hood and the barrier thorns were never drawn, and both hearts and pips read as blobs. All four
  calls are counter-clockwise now, so hearts have their point and the footer's hunter pips exist
  at all (they are drawn as crisp rectangles, not triangles, so they match the pixel face).

What changed in the previous pass (Phase 8 — twenty distinct places):

- **The tile grammar grew past three shape families.** `Theme` now carries a `FloorMotif`
  (boulders, flagstones, roots, ripples, drift, mosaic, bones, shards, web, plates, mire, vents),
  a `WallMotif` (moss rock, cut block, woven root, ribbed, stacked plate, crystal, silk, bone,
  grate, frost), a `FeatureKind` (pool, nest, rune, grate, clock, mirror, crystal, web, bones,
  seal, heart) and an `AmbientKind`. `MotifTiles` draws the nine new floor grammars, the seven new
  walls, their faces, the eleven signature objects and a deterministic emblem. The older three
  families still draw boulders/flagstones/roots; nothing else in the compiler knows a theme id.
- **Every theme is unique at the pixel level.** A twelve-bit maker's mark derived from the theme
  id is pressed into every wall face and every signature object, so two floors that share a
  grammar still cannot compile the same face or the same object. The check now requires
  **six** structural differences between neighbouring floors (was two), covering motif, wall,
  feature, weather, family and the older shape numbers.
- **A signature object and an emblem per floor.** `floor-feature` is a transparent decal the
  renderer scatters about once in 29 known cells; `prop-2` is the floor's carved emblem at about
  once in 53. Both are in every atlas and both are asserted distinct across all twenty floors.
- **Weather, light and fog are per floor.** `FloorStyle` holds an accent, one of twelve particle
  behaviours, a warm/cold light cast and a fog density. `PixelRenderer` draws 30 tiny particles
  above the lightmap (they glow in the dark) and tints the lightmap, so The Ember Vault breathes
  embers through a warm lantern while The Hush flicks with static through a cold one. The HUD and
  the letterbox take the same floor colour.
- **The HUD is a floor's HUD.** `RaylibRenderer.DrawOverlay(state, FloorChrome)` draws the floor's
  emblem, number, name and gimmick on the left, four compact status chips on the right, and keeps
  the hearts and the control hints in the footer. The floor intro, cleared, run-complete and
  caught cards use the compiled font, and the first three carry the emblem and a five-block
  palette strip. The draft header gains the emblem and the floor name.
- **The type is the game's type everywhere.** The title wordmark, the HUD's floor label, name and
  gimmick, the four status chips, the footer hints and the draft's panes are all drawn by
  `PixelFont` in whole-pixel integer scales, so the interface is as crisp as the tiles under it.
  Nothing in the UI calls `Raylib.DrawText` any more, and `RaylibRenderer.Text` throws if a font
  was not supplied, so a screen cannot quietly regress to the system face. The hints centre in
  the room the hearts leave and the floor name is truncated arithmetically, because the face is
  fixed-advance: a narrow window drops one size rather than clipping a line.
- **A latent floor-size crash was found and fixed.** `PixelRenderer` kept the constructor's 15x15
  `_rows`/`_columns`, so the 11x11 Warren asked the grid for cells outside its bounds; the
  renderer now reads the floor's dimensions from `GameState.Maze` every frame. The harness had
  never reached floor 9 before because it stopped at an earlier exception or a short run.
- **Music was already one track per floor**, and a new test pins that no two of the twenty
  parameter sets are identical, so the guarantee cannot quietly regress.

Verified in the Phase 9 pass:

- `dotnet build` -> **0 warnings, 0 errors**. `dotnet test` -> **688/688 passing, 0 skipped** (13
  new: the near-sight rule with it on and off, two-hunter placement and movement, a catch on the
  second hunter, the throw and the shared stun, floor-10/16 hunter counts, config validation, and
  the music's per-floor melodies, style spread, adjacent-style rule and three meters).
- All twenty tracks rendered with no exception: 170–400 ms each in a debug build, peak 0.84–0.93,
  RMS 0.05–0.23, loop seam < 0.003. The closest pair of tracks differs by 0.137 mean per sample.
- The window drew all twenty floors (two hunters on 10–15, three on 16–20) and the shipped game
  ran for eight seconds with an empty log. The footer was read back as a pixel map: two pips on
  floor 10 and three on 16/20, clear of the hearts and the hints.

Verified in the Phase 8 pass:

- `dotnet build` -> **0 warnings, 0 errors**. `dotnet test` -> **675/675 passing, 0 skipped**
  (14 new: signature features, wall faces and emblems distinct across all twenty floors, every
  motif/feature used, the style table matching the floor catalog, distinct accents and weather,
  mirrored emblems, and the twenty distinct music parameter sets).
- `tools/AssetForge -- --check` -> `CHECK_OK themes=20 tiles=2264`; every atlas recompiles
  byte-identically. A direct pixel comparison over all theme pairs reports **0** duplicate
  floors, walls, wall faces, features or emblems.
- A real window, 900x760, drew **all twenty floors** and three campaign cards without an
  exception (`/tmp/mzlook-*.png`, `card-*.png`), including the 11x11 Warren and the 21x21 Long
  Gallery. The title screen and the draft were captured at 900x760 and at the 640x560 minimum,
  and the HUD band of a floor was read back as a text map: the emblem, the three floor lines and
  the chips occupy separate columns, and the hints clear the hearts. The shipped window ran for
  eight seconds with an empty log. The screenshots were read back and measured (average colours,
  painted area, HUD/card layout) but not judged by a human eye; the weather, tile detail and
  type are parameter-checked, not taste-checked.

What changed in the previous pass (Phase 7 — audio):

- **Core raises events; the UI listens.** `GameEvent` and `GameState.DrainEvents()` publish the
  moments a frame cannot be inferred from state — `Step`, `KeyPicked`, `HeartLost`,
  `HeartLostLast`, `KillerStep`, `SealPlaced`, `SealCracked`, `SealBroken`, `WispLit`,
  `WispConsumed`, `FloorCleared`, `RunLost`. The queue is capped at 32 so a frontend that never
  drains (the console view, a long test) cannot grow it with the length of the run. This is also
  the VFX seam: nothing in Core references audio.
- **A synth toolkit, not a sample library.** `Synth` has oscillators, a seeded noise source,
  one-pole and band-pass filters, a soft clipper and a four-comb/two-all-pass reverb; `SfxBank`
  arranges 22 recipes from them (three footstep variants, killer step, damage, last heart,
  heartbeat, key pickup, sonar ping, seal placed/cracked/broken, wisp lit/consumed, arch
  locked/unlocked, floor cleared, card hover/take, run lost, lunge tell, near miss). All 22 are
  mono, 0.09–1.8 s, peak ≤ 0.9, and no two share a waveform.
- **Twenty tracks from twenty parameter sets.** `MusicTheme` is `root, mode, tempo, bars,
  progression, per-family mix, density, tension, reverb, band`; `MusicCatalog` holds one per
  floor, with ids **equal to the atlas ids**, and five shared motifs transformed per band
  (inverted from floor 11, retrograded from floor 16). `ScoreRenderer` arranges and renders one
  loop of three stems — bed (pad/choir), rhythm (bass/drums), lead (motif/bells) — with every
  overhanging note wrapped and the reverb run around the loop point, so the seam is ~0.005–0.016
  (a -40 dB step at the loudest point) rather than a cut.
- **The mix reacts on the bar line.** `MusicMix` turns distance into `Calm` (pad only), `Tense`
  (bass in) or `Chase` (everything), and `AudioEngine` adopts a new intensity **only at the next
  bar**, then fades it in over 400 ms so a stem cannot step on top of the waveform mid-note. Damage
  ducks the music to 35% for 1.4 s behind the stinger (2.4 s for the last heart); a stunned killer
  or an open aim drops the music back; on the last heart a heartbeat plays on its own cadence. The
  last floor opens with a **ten-second calm hold** (the drums and motif arrive late) and carries its
  motif **in canon** — a second statement half a loop later, which is a `MusicTheme.Canon` flag, not
  a name check in the renderer.
- **No device is a silent game.** `AudioDirector.TryCreate()` returns null on a headless machine
  and every caller is `?.`; the stream is fed by `SetAudioStreamCallback` (a Cdecl
  `UnmanagedCallersOnly` shim into `AudioEngine.Render`), which is what makes it 100% real time
  rather than a poll that stutters or lags. The mixer allocates nothing per block, the bank is
  built on the main thread before the stream starts, and the score is rendered during the floor
  intro (45–150 ms, once per floor).

Verified in this pass:

- `dotnet build` → 0 warnings, 0 errors. `dotnet test` → **661/661 passing, 0 skipped** (56 new:
  10 event-stream cases, 8 catalog, 5 bank, 9 score, 14 mix (including the distance theory) and 10
  mixer).
- Device path, on this machine: `device: opened`, **461 callbacks / 203,301 frames in 4.6 s** =
  **100% of real time**, 441 frames (10 ms) per callback, through a real score + key sounds +
  two ducks + an intensity change, and disposed cleanly. A windowed run of the shipped game logs
  **nothing** (the earlier polling design logged a warning per block; the callback design is
  silent).
- Mixer cost: **0.006 ms per 2048-frame block** (≈0.01–0.3% of one core, budget 4%), measured over
  215 blocks = 10 s of the densest floor with footsteps mixed in. Score render: 45–80 ms per floor
  after the first (143 ms, JIT); SFX bank builds in 27 ms.
- Generated audio, measured over all twenty tracks: peak **0.85–0.92**, RMS **0.110–0.155**, loop
  seam **≤ 0.016**, loop length within 1% of the tempo, deterministic for a given seed.

What changed in the previous pass (Phase 6 slice 1 — the pixel renderer):

- **The floor is pixel art, at a whole-pixel scale.** `PixelLayout` sizes a virtual canvas from the
  floor (one 16 px atlas tile per cell, an 8 px margin, an 18 px header), `PixelView` renders into a
  `RenderTexture2D` and blits it with **integer** scale and letterboxing — 2x in the 656x760 window
  of the time, 512x548 in the middle — so the art is never resampled by a fraction. (The canvas is
  now a camera viewport at 4x; see the camera pass below.) A resize changes only the scale and the
  offset, never a coordinate inside the game.
- **The atlases are loaded, and a missing tile fails loudly.** `AtlasManifest` parses and validates
  `manifest.txt` (theme lines, tile lines, counts, positions inside the atlas), `AssetPaths` finds
  the folder beside the executable or in the repository, and `TileAtlas` looks a tile up by name.
  An unknown theme or tile throws with the name in it; nothing draws a placeholder.
- **Thin wall faces, and a new tile family to draw them.** This maze stores walls *between* cells,
  so the compiled solid wall and autotile set (a whole cell of rock with its open sides carved) is
  the wrong shape for it: it is for the later floors whose non-walkable cells are walls. A wall is
  now a 5 px face along the closed edge — four new tiles per theme (`wall-north`/`-east`/`-south`/
  `-west`, transparent elsewhere), drawn once per wall by giving it to the cell's north/west side,
  plus the outer rim. 327 tiles across three themes.
- **One light pass, one fog rule.** A `cols x rows` texture holds the lantern falloff, the
  remembered-cell level and the killer's cold tint; it is multiplied over the scene once per frame,
  so there is no gradient call per cell, and the flicker rides on the draw's tint instead of forcing
  a texture upload. Unexplored cells are not drawn at all, remembered ones are drawn tinted, and the
  explorer's lantern is the brightest pixel on screen (measured at 598/765 in the harness).
- **The cast, animated from the step clock.** The explorer picks a facing from the cell it stepped
  from and a walk frame from `PlayerStepFraction`; the killer uses its own pair; keys and wisps
  cycle three frames from the clock; a barrier draws cracked once its seal is spent, and the arch
  opens when the keys are in. The figures snap to their cells, like the vector renderer.
- **The aim reticle is chrome, not world.** It is drawn after the canvas is scaled, in window
  pixels, so fog cannot dim the one thing a player needs while placing a barrier, and the brackets
  stay whole pixels wide at any scale.
- **The HUD is shared, not cloned.** `RaylibRenderer` grew `DrawOverlay` (damage flash, status
  pills, hints, loss card, grain) and both renderers call it; the vector chrome sits in the
  letterbox around the canvas, which keeps the tiles at whole-pixel scale and the type at its
  designed size.

Verified in this pass:

- `dotnet build` → 0 warnings, 0 errors. `dotnet test` → **574/574 passing, 0 skipped** (46 new:
  the manifest parser and its rejections, the shipped tiles the renderer asks for, the mask table
  against the compiler's, and the canvas and integer-scale geometry).
- `tools/AssetForge -- --check` → `CHECK_OK themes=3 tiles=327`; the atlases are regenerated and
  their PNGs and manifest are byte-identical on a recompile.
- Real pixel renderer in a real window, 200 frames after walking 70 steps: **median 1.03 ms**,
  mean 1.34 ms, p95 3.28 ms per draw. The canvas at 2x is **62% painted** with 592 distinct
  colours, the letterbox has **0** lit pixels, the **136 unexplored cells have 0** lit pixels, the
  explorer's cell peaks at **598/765**, all **three themes** load and draw, and the aim reticle
  reads **690/765** at the cursor corner. Screenshots: `/tmp/mzgame-pixel-{shallows,rootbound,
  drowned,aiming}.png`.
- The shipped game opened and idled for 9 s with an empty log, and `--console` is unchanged.

What changed in the previous session (input, snapped movement and the rest of Phase 5):

- **A tap is one tile, a hold walks, Shift runs.** Sending the held direction every frame meant a
  tap that lasted a few frames left a press in the buffer, so a slow tap could move a second tile
  after the player had let go. The decision is now Core's `StepRepeat`: a fresh press sends exactly
  one direction (the state buffers it if the last step is still cooling down), a direction only
  repeats after 0.15 s down **and** only once the previous step has finished, and a release resets
  the gate so the next tap is fresh again. Because repeats wait for the step clock, the hold runs at
  the floor's cadence rather than the frame rate, and Shift needs no special case: it shortens the
  step, so the same repeat simply lands faster. It lives in Core only because that is the only way
  to test it without a window (nine unit tests, plus three that drive it against a real `GameState`).
- **The explorer snaps to its tile.** The window used to draw the player (and the lantern pool)
  between two cells, so at 0.11 s per step a held key was a continuous glide: the figure was
  almost never on a tile, and counting cells while being chased was guesswork. The player is now
  drawn on `GameState.Player`, which changes the instant a step is accepted, so **one press is
  exactly one tile**. `PlayerStepFraction` still exists as the step's clock (for a walk cycle),
  but it is no longer a position. The killer still slides: it is a creature at a distance, and a
  smooth drift reads as stalking without costing the player any navigation.
- **Phase 5's exit is met: the tileset and the whole cast are compiled, committed and checked.**
  The autotiler derives the 16 edge tiles and all 47 blob tiles from open-side masks (a diagonal
  only counts when both of its edges are open, which is what makes it 47 and not 256), and
  `Rig` draws the cast: the explorer (4 facings x idle/walk0/walk1/walk2), the killer (4 x 3), the
  key and wisp (3 frames each), the barrier (fresh and cracked) and the arch (locked and open).
  All three shipped themes carry 104–106 tiles in a 128x224 atlas, and the manifest lists every
  tile's name and position.

What changed in the previous pass (stacking fixes, movement cards, sprite compiler slice 1):

- **Two real card bugs, found by asking whether effects stack as the run gets deeper.** (a) Two
  caps ignored the run's cards: `Campaign.HeartsOnNextFloor` clamped carried hearts to the floor
  definition's ceiling, so a card that raised the ceiling (Blood Pact) could never be filled — a
  skip-heal into a four-heart floor was thrown away — and `GameFactory.CreateFloor` validated the
  carried hearts against the unmodified floor, so the extra heart would have thrown once it got
  through. Both now use the composed ceiling, and the factory still refuses a carry no card could
  justify. (b) The composition itself was already right: additive effects add, the pace scale
  multiplies, and every floor is built from its own definition with the run's totals applied once
  — now with a test per bullet.
- **Movement cards, on a real step time.** They could not exist before Phase 4: there was no
  player step to trade against. `CardEffect` gains `PlayerStepScale`, `InvulnerabilitySeconds`,
  `SprintStaminaScale` and the momentum knobs; `RunModifiers` multiplies the scales, adds the
  seconds and clamps. Shipped: **Light Feet** (-15% step, one heart slot fewer), **Hollow Bones**
  (+1.5 s grace, +25% step), **Sprint Lungs** (×1.5 stamina bar, aura +2) and **Momentum**, which
  needed a rule rather than a number: each straight step earns 5% up to +25%, and the first step
  after a turn costs 15% for 2 s. All of it is zero by default, so a run without the card keeps
  the old cadence exactly.
- **Sprite compiler, slice 1 (`tools/AssetForge`).** A console project with no packages, Core
  referenced only for `SeededRandom`: `PixelCanvas` (index canvas with Rect, Line, Ellipse,
  Speckle, Dither, Shade, Outline, Mirror, Stamp), `Palette` (16 roles derived from two hues on a
  fixed lightness ladder, so contrast cannot drift with the theme), a dependency-free PNG encoder,
  the organic-cave `CaveTiles` grammar, three shipped themes and a `--check` pass that validates
  the themes, neighbouring palette distance and structural differences, and catches a committed
  atlas that no longer matches the code. The atlases are committed to
  `src/MazeGame.UI/Content/Generated`; **nothing in the game reads them yet**.

Verified for this pass:

- `dotnet build` → 0 warnings, 0 errors. `dotnet test` → **516/516 passing, 0 skipped** (19 new
  cases this pass: the autotiler's masks and carves, the rig's facings, frames and props, and the
  atlas layout).
- `dotnet run --project tools/AssetForge -- --check` → `CHECK_OK themes=3 tiles=315`. Every atlas
  was decoded independently (platform zlib, every chunk CRC) at 128x208 or 128x224, and a
  recompile is byte-identical.
- Real renderer through the whole campaign and both draft layouts, plus the movement probe:
  **236 frames**, `DRAFT_OK`, `DRAFT4_OK four=4 two=2`, `MOVEMENT_OK step=0.11s halfway=0.50
  sprinting=True stamina=0.91 idleStamina=1.00`, **2.3–2.4 ms/frame**. The run now ships the new
  cards through real drafts: its final modifiers read
  `WISPS -1   HEARTS -1   WISP STUN 1s   STEP +15%` — four different card kinds composing on the
  floors that followed.

## Previous pass: roguelike Phase 4 (movement feel)

What changed in that pass:

- **Timed steps, with the rules still on the grid.** `GameConfig.PlayerStepSeconds` gates when the
  next step may start; the player's cell still changes the instant a step is accepted, so keys,
  the arch, a catch and every path search still see one cell at a time. Zero means instant, which
  is the seam every rule test was written against; `FloorDefinition.PlayerStepSeconds` is the
  per-floor value (0.11 / 0.10 / 0.09 s against killer steps of 0.60 / 0.54 / 0.48), so the
  shipped game is timed while the tests stay instant.
- **One buffered input.** A direction pressed while the last step is cooling down is remembered for
  the end of it — one only, so mashing cannot outrun the cadence. The buffer is spent on the step's
  completion, dropped if it points into a wall, and cleared if it would fire after the game ends.
- **Render interpolation.** `GameState` publishes `PreviousPlayer` / `PlayerStepFraction` and the
  same pair for the killer; `RaylibRenderer` draws both figures (and the lantern's pool) from them.
  Nothing in the rules can see a position between cells, and a respawn snaps rather than sliding.
- **Sprint is a stamina resource, not a card.** Hold Shift for 1.4× speed while the 1.2 s bar
  lasts, and it only drains while you are actually moving — holding Shift on the spot is free, so
  the bar is full when you set off. An empty bar needs a quarter refill before it can be spent
  again (otherwise sprint would flicker on and off). `SetSprinting` is a *request*: `IsSprinting` refuses it while aiming, when
  the floor has instant steps, when the run is over, or when stamina is gone. The HUD grows a
  sprint bar that only appears once stamina has been spent.
- **Frontends.** The window reads walking through Core's **`StepRepeat`** — a tap is one tile, a
  hold repeats at the cadence, and a release resets the gate — while the aim cursor stays
  press-based (a hold would fly across the map) and Shift is sampled every frame. The console is unchanged: a
  terminal cannot hold a key, so it stays one press per step and has no sprint.

Verified in that pass:

- `dotnet build` → 0 warnings, 0 errors. `dotnet test` → **449/449 passing, 0 skipped** (34 new
  cases: timed steps, the buffer, sprint and stamina, the config rules, and the catalog's step
  values).
- Real renderer through the whole campaign and both draft layouts, plus a movement probe that
  steps onto a tile, draws halfway through it and then sprints: **236 frames**, `DRAFT_OK`,
  `DRAFT4_OK four=4 two=2`, `MOVEMENT_OK step=0.11s halfway=0.50 sprinting=True stamina=0.91
  idleStamina=1.00`, **2.0–2.3 ms/frame**.
- Redirected `--console` run: **3,217 frames**, heading `FLOOR 1/3 — THE SHALLOWS`, ended with the
  loss summary, exit 0.
- Window build stayed alive for 8 s with an empty log (stopped by the harness).

## Previous pass: roguelike Phase 3 (the card draft)

What changed in that pass:

- **Cards are data, and they change a floor's inputs, not its rules.** `Card` carries an id, a
  tier, a face sentence and a `CardEffect` of numbers; `CardCatalog` holds nine of them. Nothing in
  `GameState` knows a card exists: `GameFactory.CreateFloor` composes the floor's config with
  `RunModifiers` before building it, so all 415 tests still describe the same rules.
- **`RunModifiers` composes and clamps.** Additive deltas (vision, aura distance, budgets, key
  count, hearts, wisp rules), two absolute overrides (seal lifetime and wisp lifetime) and one
  multiplier on the killer's pace. Clamps: vision never below 1 step once a card touched it,
  budgets and counts never negative, the killer never faster than 0.05 s per step, and a heart
  ceiling never below the hearts the player is carrying.
- **The draft belongs to the run.** `Campaign` owns the held cards, the offers and the queue of
  pending heals. `TryAdvanceFloor` **refuses to advance while a draft is open**, so no frontend can
  skip the choice. Offers come from `DraftFactory`, seeded by the run seed and the floor number,
  so a seeded run reproduces its drafts exactly. Skip = one heart, capped by the next floor.
- **Nine cards, each with a tested price.** Wide Eyes (+2 vision / aura +3), Deep Roots (18 s
  seals / one barrier fewer), Thornwright (+1 barrier / 4 s seals), Blood Pact (+1 heart / killer
  12% faster), Second Wind (heal now / vision −1), Bait (wisp stuns / one wisp fewer), Smoke Wisp
  (10 s pull / −2 vision while it burns), Trophy Hunter (heal per clear / +1 key per floor), Lucky
  Find (+1 offer next draft / −1 the one after).
- **Two new floor rules** back two of the cards: `LureStunSeconds` (a wisp can stun the killer)
  and `LureVisionPenalty` (a burning wisp can shrink the torch). Both default to zero, so the
  shipped game is unchanged without cards. The wisp's own light now also goes out when it expires
  or is consumed (it used to linger until the player's next move).
- **Draft guards.** No card is offered twice unless the catalog marks it stackable; a card that
  contradicts a held one is not offered (Deep Roots vs Thornwright, which both set an absolute
  seal time); no card may change vision once two held cards already do; and a card is withheld
  when the part of the run that pays for it does not exist — which is why Lucky Find cannot appear
  in the three-floor campaign at all.
- **Frontends.** The window gained a draft screen between the floor-cleared card and the next
  intro: cards in a grid (two rows above three offers), tier colour, the face sentence wrapped
  inside each card, the run's totals underneath the title, and "next floor: n hearts". Choose
  with A/D or the arrows, take with Enter/Space or a number key, skip with S. The console prints
  the offers and takes a keypress, or skips automatically when input is piped.

Verified in that pass:

- `dotnet build` → 0 warnings, 0 errors. `dotnet test` → **415/415 passing, 0 skipped** (84 new
  cases: every card's two halves, the clamps, draft determinism and guards, the campaign's draft
  beats, and the two wisp rules).
- Real renderer through a whole campaign with drafts — title, three intros, three cleared cards,
  two drafts, the run summary — and a second four-floor run that draws the four-offer layout and
  the two-offer draft that follows: **224 frames**, `DRAFT_OK` + `DRAFT4_OK four=4 two=2`,
  **1.75–2.03 ms/frame (three runs)**. Cards visibly reach the next floor in the same log: `maxHearts 4`,
  `step 0.422`, `barriers 4`, `WISPS -1`, `WISP STUN 1s`.
- Redirected `--console` run: **5,402 frames**, heading `FLOOR 1/3 — THE SHALLOWS`, ended with the
  loss summary, exit 0.
- Window build stayed alive for 8 s with an empty log (stopped by the harness).

## Previous pass: roguelike Phase 2 (floors and the campaign)

What changed in that pass:

- **A run object in Core.** `Campaign` owns the floor list, the current `GameState`, the hearts
  carried between floors and the run outcome. `CampaignStatus` (InProgress / Won / Lost) keeps the
  run separate from `GameStatus.Won`, which now means "this floor's exit was reached". A floor's
  `Caught` ends the run immediately; a clear is progress, and the run is won only after the last
  floor.
- **Three floors as data.** `FloorDefinition` + `FloorCatalog`: The Shallows (3 keys, 0.60 s per
  killer step), Rootbound (3 keys, 0.54 s), Drowned Halls (4 keys, 0.48 s). Each carries a name
  and a theme id that the renderer maps to an accent colour; hearts start each floor at the count
  left on the previous one, out of that floor's ceiling.
- **Per-floor seeds.** `GameFactory.CreateCampaign()` and `CreateFloor(floor, hearts)` replace the
  single-game assembly for the frontends. The random factory is now `Func<int?, IRandom>`, and
  floor n uses `seed + (n - 1) * 7919`, so a seeded campaign reproduces exactly while its floors
  are genuinely different caves. `Create()` still builds one game from the base config for tests.
- **Frontends.** Window: title → floor intro (1.6 s, or Enter/Space) → play → floor-cleared card
  (Enter) → next intro → … → run summary; a lost run uses the existing end card. Console: a floor
  heading, the carried-hearts line, auto-advance when input is piped, and a run summary. Both now
  take `Func<Campaign>`.
- **The window says where you are.** The title bar becomes
  `Bioluminescent Depths — Floor n/3`, and the intro/cleared cards use the floor's accent.

Verified in that pass:

- `dotnet build` → 0 warnings, 0 errors. `dotnet test` → **331/331 passing** (30 new cases across
  campaign progression, the floor catalog, and factory floor/campaign building).
- Real renderer through a whole campaign — title, three intros, three floors cleared by walking,
  three cleared cards and the run summary: **160 frames**, `CAMPAIGN_OK status=Won floor=3
  cleared=3 hearts=3`, **2.09 ms/frame**.
- Redirected `--console` run: **3,753 frames**, heading `FLOOR 1/3 — THE SHALLOWS`, ended with the
  loss summary "Floor 1 of 3, 0 cleared.", exit 0.
- Window build stayed alive for 8 s with an empty log (stopped by the harness).

## Previous pass: roguelike Phase 1 (hearts and damage)

What changed in that pass:

- **Three hearts, one mistake at a time.** `GameConfig` gained `StartingHearts` (3),
  `MaxHearts` (3), `InvulnerabilitySeconds` (1.5), `RespawnDistance` (8) and
  `RespawnStunSeconds` (1.5), all validated. A catch spends a heart and the floor continues;
  only the last heart produces `GameStatus.Caught`.
- **The catch is a respawn, not a restart.** `TakeHit` throws the killer to the furthest
  reachable cell, preferring a dead end and avoiding keys, the exit, an active wisp and existing
  barriers (a second pass drops that preference so a tiny floor still resolves). Walking into the
  killer during the grace window is refused instead of overlapping, and the killer waits beside
  the player rather than sharing their cell. Keys, barriers and the layout survive.
- **Presentation.** Hearts (filled/dim, pulsing on the last one) sit in the footer beside the
  hints so the HUD keeps its "IT IS CLOSE" warning; a 0.35 s red wash marks the loss; the
  console header prints `hearts 2/3`; the ending line is "The killer took your last heart."
- **Key visibility fixed (play-test bug).** The lantern falloff was painted *after* the cell's
  contents, so a key at the edge of the torch was multiplied down to a few percent of its own
  glow — rule-visible, but effectively invisible. The falloff now goes on before the contents,
  and the key itself is a beacon: a magenta pool on the floor, a six-band shaft of light that
  spills past the cell, a pulsing ring, a bobbing bright core with a white highlight, and two
  orbiting sparks. Verified by walking the player next to a key in a real window and drawing 100
  frames (`KEYS_RENDERED`, key visible and present, 2.27 ms/frame).
- **Stun is real time, not a delayed step.** While stunned the killer neither moves nor banks
  progress toward its next step, so the 1.5 s window is usable.

Verified for that pass:

- `dotnet build` → 0 warnings, 0 errors. `dotnet test` → **301/301 passing** at the time (22 new
  cases: 11 heart-rule tests and 11 config validation cases across 5 test groups, plus updated
  chase, lure, invariant and factory expectations).
- Real renderer, **1,264 frames** through three catches with seed 7: hearts `[2,1,0]`,
  `InProgress → InProgress → Caught`, invulnerable and stunned after catches 1–2, no shared cell
  until the final catch (`HEARTS_OK`), average draw **1.87 ms/frame**.
- Redirected `--console` run: **4,496 frames**, header `hearts 3/3` → `hearts 0/3`, ended with
  "The killer took your last heart.", exit 0.
- Window build stayed alive for 7 s with an empty log (stopped by the harness).

## Previous pass: title screen, replay and the game factory

- **A title screen.** `RaylibRenderer.DrawTitle` (a partial of the cave renderer, so it shares
the palette, vignette, grain and text helpers) draws the same cave seen from further away:
breathing green/cyan/pink light pools, drifting spores, a hand-spaced `BIOLUMINESCENT / DEPTHS`
wordmark with a broken glowing rule, and a two-entry menu (`DESCEND`, `LEAVE`). The selected
entry pulses and gets a pair of triangle chevrons — triangles rather than glyphs because
raylib's built-in font is ASCII only. The console prints the same wordmark and waits for a key.
- **Replay after death.** `RaylibFrontend` is now a two-screen loop (title, game). The end card
says `R / ENTER: DESCEND AGAIN   ESC: QUIT`, and R builds a brand-new game and keeps the same
window. The console prints the ending, then `[R] descend again   [Q] leave`. Because `Cell` is a
value type, cells of a new maze compare equal to the old one's, so `RaylibRenderer.Reset()`
clears the explored-cell memory at every new run — otherwise a replay would start with the old
cave already drawn in.
- **`GameFactory` (Core).** One place that builds a complete playable game: its own random
source per run, the shipped maze pipeline (perfect Kruskal → `DeadEndBraider` →
`BraidedMazeGenerator`), then `GameState`. Replay is just `Create()` again, so it is a tested
rule rather than glue in two event loops. A seeded config replays the identical cave; the
shipped `Seed = null` gives a new one every time.

Verified for that pass:

- `dotnet build` → 0 warnings, 0 errors. `dotnet test` → **279/279 passing** (10 new factory
  tests: fresh playable game, both maze shapers wired, perfect maze with both off, seed
  reproducibility, unseeded variety, replay after a caught game is independent and fully
  restored, one random source per run, and the argument/validation failures).
- Title layout measured in the real font: the widest line (`BIOLUMINESCENT`) is 413 px of 600
  usable, the menu chevrons reach 97 px from the centre, and the vertical stack leaves gaps at
  every step (wordmark 148-264, rule 300, menu 412/468, hints 678/712), so nothing collides.
- Title and replay driven through the real renderer in a window: **321 frames** — both menu
  states, the first run played to `Caught`, the end card, then a replay created and drawn — with
  no exceptions (`RENDER_OK`). Title drawing costs **1.76 ms/frame**.
- The shipped window stays alive on the title with no input (empty log, exit 143 is my SIGTERM),
  and a redirected `--console` run still plays and logs frames with the title prompt skipped, as
  documented (the terminal cannot answer a key prompt when input is piped).

## Previous pass: chase feel (measured, still current)

The complaints this pass answered were "it is very easy to get cornered and the barriers do not
flow into the gameplay":

- **Dead ends are braided, not ignored.** `DeadEndBraider` decorates the generator and opens one
  extra wall at `DeadEndBraidPercent` (55%) of the maze's dead ends. Removing a wall can never
  create a new one, so this only removes traps: a 15x15 map goes from **57 dead ends to 18.8**
  (8 seeds). One opening can braid two neighbouring dead ends, so the loop counts *cells*, not
  openings, and skips ones a neighbour already fixed. It is a second decorator, so the graded
  Kruskal generator is still untouched and `DeadEndBraidPercent = 0` restores every trap.
- **A barrier is now a seal, then a husk.** `ConfirmBarrier` starts a
  `BarrierSealSeconds` (10) seal and records it in `BarrierSeals`. A sealed cell is priced at
  `SealedBarrierCost` (40) — more than any detour on a 15x15 map — so the killer takes another
  route while the seal holds. If the seal is the only way through, it tears the barrier apart
  over `BarrierBreakSeconds` (3) and the barrier is destroyed. After the seal lapses the husk
  stays and costs `ObstacleCost` (5) as before. Both prices stay finite, so the
  "no soft-lock" invariant survives, and a random-play test still proves the killer always has
  a route.
- **A new power-up: the wisp.** `LureCount` (2) charges, `LureSeconds` (6) lifetime. Aimed exactly
  like a barrier (one shared `PlacementMode` and cursor, one shared clock freeze). While it
  glows, the killer runs to whichever of the player and the wisp is **cheaper by Dijkstra** —
  two `FindPath` calls compared by `RouteCost`, ties to the player, no scripted "distracted"
  state. Reaching it consumes it; it also expires. The wisp lights its own cell and its
  neighbours, so you can watch the trick work from across the map.

Verified for the chase pass:

- `dotnet build` → 0 warnings, 0 errors. `dotnet test` → 269/269 passing at the time (52 new).
- Measured over 8 seeds of the shipped 15x15 config (`DeadEndBraider` + 8 loops), counting every
  cell on the killer's route:

  | Maze | Dead ends | Route cells a seal can detour | Killers forced to tear down |
  | --- | --- | --- | --- |
  | Before (perfect + 8 loops) | 57.0 | 40% | 60% |
  | Shipped (55% braid + 8 loops) | 18.8 | **81%** | 19% |
  | 100% braid + 8 loops | 0.0 | 92% | 8% |

- The killer's tear-down is time-based, not step-based: a half-second step time still spends the
  full `BarrierBreakSeconds` in front of the wall (covered by a test).
- Console frontend: 482 frames with the new `wisps` counter and hints, no exceptions.
- Raylib renderer driven directly for 136 frames through barrier aiming, a sealed and a spent
  barrier, wisp aiming, a glowing wisp and the end card: `RENDER_OK`, no exceptions.
- Drawing cost measured at **0.70–1.04 ms/frame** draw submission and **2.3–3.5 ms/frame**
  including `BeginDrawing`/`EndDrawing`, with every optional layer on (400 frames, 656x760,
  three runs: 0.84/0.84/0.90 ms draw-only). That is CPU-side submission; the GPU cost is
  unmeasurable here because no frame buffer can be read back.
- The HUD still fits with the new wisp pill: measured in the real font, keys + barriers + wisps +
  exit end at x=519 of 600 usable px, and the "IT IS CLOSE" warning starts at x=549.
- Window build stayed alive for 6 s with an empty log; 482 console frames with no exceptions.

Core contains:
- `DisjointSet` (path compression, union by size), `MazeGenerator` (randomised Kruskal),
  `DeadEndBraider` (trap-removal decorator) and `BraidedMazeGenerator` (loop decorator).
- `IRandom`/`SeededRandom`, `Direction`/`Cell`/`Grid`, `MinHeap<T>`,
  `DijkstraPathfinder` behind `IPathfinder`.
- `GameConfig` (every tunable, validated), `GameStatus`, `PlacementMode`, and `GameState` —
  the single object the UI talks to: placement (barrier or wisp), movement, keys, exit, barrier
  seals, the wisp, visibility (torch, wisp and killer aura), win/lose, enemy AI.
- `GameFactory` — builds one complete game per call (random source, the two maze decorators,
  `GameState`), which is what the title screen and the replay both use.

## Current pass: what a playtest found, and what was changed

Four things came back from a first real playthrough: the music sometimes tore into static, the
walls blended into the path so the maze read as guesswork, tapping was the only way to move quickly
because holding did not walk, and every floor was the same rectangle.

- **The music no longer glitches.** Arranging a floor (200–400 ms of synthesis and a few megabytes
  of stems) used to run on the game thread during the intro card; it now runs on a worker and is
  adopted on the first frame of play, while the previous track keeps playing. The device callback
  also clears its buffer instead of returning it untouched when there is no director, and the mixer
  adopts a published score between blocks. Measured: `PlayFloor` returns in **2.4 ms** (was
  200–400 ms), the score is live after **0.36 s**, the slowest `Update` is **0.87 ms**, and the
  device drained **100 % of real time** over 4 s. The calm-hold bug this exposed — the last floor
  opening at full chase for one block — is fixed and covered by `AudioEngineTests`.
- **Walls are the loudest thing in the palette after the lantern.** Wall body 0.40 → 0.50, rim
  0.58 → 0.68, contact shadow 0.07 → 0.05, floor 0.20 → 0.18; the band is 6 px instead of 5 (a
  corridor still keeps 4 px of floor); remembered (seen, unlit) cells are lit at 0.55 with a
  brighter tint instead of 0.40 with a 45 % one. Measured on the committed atlases, the drawn band
  body is now **2.5–8.3x** its floor (was 1.6–3x), and `WallContrastTests` fails if it drops below
  2.2x, if the rim goes under 5x, or if two bands would leave a corridor with no floor.
- **Holding a direction walks.** The input gate used to wait 0.15 s on top of the step before it
  would repeat; now a repeat is due the moment the step that the press caused finishes, at the
  cadence of that step — so sprint and momentum are felt immediately — and a tap is still exactly
  one tile because the gate stops sending the moment the key is up. `StepRepeat.Next` takes the
  last step's duration, and `GameState.LastPlayerStepSeconds` is what the frontend passes it.
- **Floors are not all rectangles.** `MazeShape` grows a `Blob` or an `Ellipse` over the grid and
  closes the rest as solid rock *before* generation, so passages can never open into the rock and
  the cave is exactly as connected as a rectangular one. Eight shipped floors use a shape (Rootbound,
  The Hush, The Foundry and The Deep are blobs; The Salt Choir, The Long Gallery, The Glass Orchard
  and The Tide are ovals). `Grid.IsSolid`, `OpenCellCount` and a shape-aware placement rule keep the
  player, keys, exit and hunters on floor cells; `MazeShapeTests` and `ShapedFloorTests` walk the
  real catalogue and prove every key and exit stays reachable.

## Current Task

**The twenty floors are now twenty places.** Each one selects a floor motif, a wall motif, a
signature feature and a weather; the renderer gives it a warm or cold light, a fog, a particle
layer and a HUD with its emblem, name and gimmick; the cards carry the emblem and a palette strip.
The compiler's `--check` requires six structural differences between neighbours, and the twenty
signature features, wall faces and emblems are asserted distinct at the pixel level. What is left
is taste: whether a given motif reads as the place its name promises, whether the weather is
visible enough without obscuring keys, and whether the HUD still fits at the minimum window size.
Those need a human at the window and an ear on the score.

## Previous task: twenty floors and their themes

**The campaign is twenty floors, each with its own theme and gimmick.** Phase 6's play screen, the
camera and the compiled font are done; that pass was content and legibility.

- **Twenty floors** in `FloorCatalog`, each with a `Gimmick` line for its card and, where the floor
  is about a *rule* rather than a number, its own optional overrides: `VisionRadius`,
  `BarrierBudget`, `LureCount`, `LureSeconds`, `ExtraLoops`, `DeadEndBraidPercent`,
  `EnemyAuraRadius`/`EnemyAuraDistance`, `RespawnDistance`, `InvulnerabilitySeconds`. `ForFloor`
  composes them over the run's settings, so no gimmick is an `if` in `GameState`. Examples: Rootbound
  closes the lantern in (vision 2), The Gilded Cage removes barriers entirely, The Blind Mile puts the
  killer's glow out, The Ossuary respawns it three cells away, The Bramble braids every dead end.
- **Twenty themes** in the compiler's `ThemeCatalog`, one per floor, from the three shape families,
  each with its own hue pair, moss, glow, speckle and course height. `--check` rejects a neighbouring
  theme that is too close in colour or differs in fewer than two structural parameters, so the
  descent cannot drift into recolours; `ThemeFamilyTests` proves on palette indices that no two
  floors draw the same tile, and that the compiler's table and the campaign's line up.
- **Every floor has an accent**, in `RaylibRenderer.ThemeAccent`, used by the cards and now by the
  letterbox (`PixelView.Present(..., backdrop)`), so each floor sits in its own coloured frame.
- **Walls, including floor 2.** The root cause on Rootbound was that the *floor* was as bright as a
  wall: its fibres were drawn in the wall's body colour, full width, and its knots in wall-bright
  glyphs. Floor growth is now moss over shadow, the wall band carries a near-black outline at both
  edges, and the palette ladder sits at wall 0.40 / rim 0.58 / shadow 0.07 against a 0.20 floor.
  Measured on the committed atlases, band vs floor is now 1.96–7.30 across all twenty themes
  (Rootbound 2.23, was 1.52), and in a rendered frame floor 2's walled cell tops read 1.49–2.89x
  their own floors against 0.88 for open cells.

What is left of the visual phase: the **draft screen** is still the last screen set in the system
typeface, and the **HUD** still lives in the window's letterbox rather than inside the canvas.
The floor cards and the HUD now carry each floor's emblem, palette and weather, so the remaining
work is chrome (9-slice frames, card art) rather than identity.

## Next Steps

**Open from the second playtest (2026-10-21):** the player never used a powerup, did not enjoy the
cards, and wants the later floors to play differently. The death screen and the growing maze from
that session are shipped. `FUTURE.md` §18 proposed retiring barriers and wisps for a five-verb
kit (Blink, Veil, Drag, Lantern, Shove) and adding two run-scoped **descent conditions** chosen at
floors 8 and 14; that whole pass is now shipped, including the gate screen, the HUD marks, the
three replacement floor gimmicks and the card rewrite. What it needs next is the playtest, not
more code.

0. **Play floors 1–4 with a hand on the keys.** The playtest that produced this pass stopped at
   floor 4, and it reported four things: static, unreadable walls, a hold that did not walk, and
   rectangular floors. All four are changed and covered by tests, but the judgement — do the walls
   now read at 3x, does holding a direction feel like walking, is a blob floor more interesting
   than a rectangle — needs the same human again. If the walls are now *too* loud, the knobs are
   the palette ladder in `Palette.Derive` and `WallStrips.Thickness`; both need `AssetForge --
   --all` and a recommit.
1. **Look at the twenty floors and judge the motifs.** The compiler proves they are different
   pixels; only a human can say whether a mire reads as a mire and a plate floor as a foundry.
   The knobs are the `Theme` records and the routines in `MotifTiles`; a motif change is one file,
   then `AssetForge -- --all` and a commit.
2. **Listen to a run, then tune the balance.** The numbers measured here (music peak 0.72 under
   effects at 0.9, duck to 35%) are guesses that only an ear can settle. The knobs are
   `AudioEngine.MusicGain`/`EffectGain`/`DuckDepth`, `MusicMix.Gains` and the per-theme `Mix` in
   `MusicCatalog`.
3. **Phase 8's remaining content.** Five of the twenty-eight planned cards are still unbuilt (the
   rest need hazards, echoes or blueprint changes), and the run has no save/continue yet:
   remaining cards, hazards and suspend/resume.
4. **Give the cards art.** Every screen is now in the compiled face, but a draft card is still
   type on a rectangle: `Pictograms` (24x24 card art) and `UiForge` (9-slice panels, card frames)
   in `tools/AssetForge` are the missing pieces. The HUD reads like the floor yet still lives in
   the window's letterbox rather than inside the canvas.
5. **Play a run and judge the escalation.** Two hunters from floor 10 and three from 16 are the
   biggest balance risk in the game: each is at 0.72–0.54 s a step, but a pincer in a corridor may
   still be unwinnable. The knobs are `FloorDefinition.HunterCount`, `EnemyStepSeconds` and
   `GameConfig.HunterStackCost`; the first thing to try if 10 feels unfair is a slower hunter, not
   a smaller one.
6. **Play a run and judge the movement and the weather.** The harness measures the canvas and the
   mixer, but whether 0.26 s per cell on floor 1 plus a 1.20 s hunter still allows escape by
   reflex, and whether 30 particles are visible enough at 3x, needs a human at the window.
7. **Keep the seams clean.** `GameFactory.CreateFloor` remains the only place a floor is built,
   `Campaign` the only place floors, hearts and drafts are sequenced, and `GameState` the only
   source of gameplay events. Movement timing lives in Core, never in a frontend; audio never
   lives in Core.
8. Re-run `dotnet build`, `dotnet test`, the renderer harness and a window session after every
   phase; update `PROGRESS.md` and the phase table in `FUTURE.md`.

## Completed

- [x] 2026-10-21 — The card overhaul. The catalog is twenty-three cards; ten new rule hooks in
      `GameConfig`/`GameState` (free first catch, per-floor revival, catch cost, magnetic keys,
      blink refunds, veil/drag durations, lantern range); `RunModifiers` composition and draft
      totals; floor-banded tier weights and a guaranteed rare every fifth floor; a five-slot
      powerup HUD; 20 new tests (748).
- [x] 2026-10-21 — The five-verb kit and the descent gates. `PowerupKind`/`PowerupCharges` with
      per-floor refills and the teaching ladder; five verbs in `GameState` with their prices; five
      `GameEvent` moments (plus wall/exit/eclipse events); HUD counts in both renderers; the
      condition enum, catalog, seeded offer and `Campaign` gate; eight condition rules including
      two that mutate the maze behind a connectivity check; `CardEffect`/`RunModifiers` charge
      deltas and four rewritten cards; three replacement floor gimmicks; 25 new tests (728).
- [x] 2026-10-21 — Twenty distinct places. `Theme` gained a floor motif, wall motif, signature
      feature and weather; `MotifTiles` draws nine new floor grammars, seven new wall grammars,
      their faces, eleven signature objects and a mirrored emblem; every face and feature carries
      a theme-seeded maker's mark, so all twenty are pixel-distinct. `FloorStyle` (accent, weather,
      warm/cold light, fog) drives a 30-particle weather layer and the HUD; `FloorChrome` and
      `FloorEmblem` give every floor a name plate and a mark; the intro, cleared and summary cards
      show the emblem and a palette strip. `--check` now requires six structural differences
      between neighbours. Fixed a latent crash on the 11x11 Warren (renderer kept the constructor's
      15x15 bounds). 14 new tests (675 total), 2,264 tiles, all twenty floors and the cards drawn
      in a real window.
- [x] 2026-10-20 — Roguelike Phase 7: audio. `GameEvent` + `GameState.DrainEvents()` (twelve
      moments, capped queue); `Synth` (oscillators, noise, one-pole, band-pass, soft clip,
      Schroeder reverb), `SfxBank` (22 recipes, all distinct, 0.09–1.8 s, peak ≤ 0.9); `MusicTheme`
      + `MusicCatalog` (20 tracks keyed by atlas id, 5 motifs, 4 bands) and `ScoreRenderer` (three
      stems, wrapped tails, reverb around the loop, seam ≤ 0.016); `MusicMix` (distance → Calm /
      Tense / Chase, bar-aligned) and `AudioEngine` (16 voices, duck, 0.006 ms/block);
      `AudioDirector` (Raylib stream callback through an `UnmanagedCallersOnly` shim, silent when
      there is no device, 100% of real time on this machine). 56 new tests (661 total).
- [x] 2026-10-20 — Twenty floors, twenty themes, and walls that read. `FloorCatalog` grew from three
      floors to twenty, each with a gimmick line and optional rule overrides (vision, barriers,
      wisps, loops, braid, killer aura, respawn, grace) composed by `GameConfig.ForFloor`;
      `ThemeCatalog` grew to twenty parameter sets across the three families, with `--check` holding
      colour and shape apart for every neighbour. `RaylibRenderer.ThemeAccent` gives each floor its
      colour, now also used for the letterbox. The Rootbound wall problem was traced to wall-bright
      floor growth, fixed in `WrongOrganicTiles` and `WallStrips` (outline at both band edges) and
      guarded by `WallContrastTests` plus a floor-vs-band measurement: 1.96–7.30x across the themes.
      604 tests pass; 20 atlases, 2,224 tiles.
- [x] 2026-10-20 — Camera viewport, visible walls, resizable window. `PixelLayout` sizes the canvas
      from the vision radius (9–13 cells, odd) and `PixelCamera` follows the player, clamped to the
      floor; `PixelRenderer` culls to the viewport, rebuilds the lightmap per viewport cell, and
      keeps a whole-maze exploration set. A vision card zooms the camera out by rebuilding the
      render target. The palette ladder and `WallStrips` were reworked so a wall band is 1.7x its
      floor in relative luminance with a bright rim and a near-black shadow, guarded by
      `WallContrastTests`; the window opens at 696x872 and is resizable to 6x. 596 tests pass.
- [x] 2026-10-20 — Phase 6 slice 2a: three shape families and the compiled font. `TileFamily` +
      `TileGrammar` dispatch floor/wall/face/props to `CaveTiles`, `WrongOrganicTiles` and
      `WorkedStoneTiles` (three shape languages, no shared drawing code), and `ThemeFamilyTests`
      proves on palette indices that the shipped floors are different places rather than three
      tints. `Glyphs`/`FontAtlas` compile a 5x7 ASCII face into its own `font.png` (98 tiles) with
      a manifest section; `PixelFont` and `PixelCards` set the floor intro, cleared, run-summary and
      caught cards in it, and `RaylibRenderer` lost the four vector card implementations.
      `--check` green at 334 tiles; 583 tests pass.
- [x] 2026-10-20 — Phase 6 slice 1: the pixel renderer. The floor is drawn from the committed
      atlases into a 16 px-per-cell virtual canvas (`PixelLayout` 256x274, `PixelView` render target,
      integer scale 2x with letterboxing), through `AtlasManifest`/`AssetPaths`/`TileAtlas` with a
      fail-loud lookup, `TileMask` for the wall faces (cross-checked against the compiler's blob
      table), a per-cell multiplied lightmap for the lantern, fog and the killer's tint, and the
      cast animated from the step clocks. `tools/AssetForge` grew `WallStrips` (4 thin faces per
      theme, 327 tiles) because a cell-per-room maze needs walls *between* cells. `RaylibRenderer`
      exposes `DrawOverlay` so the HUD is one implementation for both renderers. 46 new tests (574
      total), asset `--check` green, harness at **1.03 ms median** over 200 frames.
- [x] 2026-10-20 — Snapped movement and the rest of Phase 5. The window draws the explorer on its
      own cell (`CellCenter`) instead of interpolating, so one press is one tile; the killer keeps
      its slide and `PlayerStepFraction` becomes a walk-cycle clock. `Autotiler` derives the 16
      edge and 47 blob wall tiles from open-side masks (corner bits canonicalised), and `Rig`
      draws the whole cast: explorer 4x4 poses, killer 4x3, key and wisp 3 frames, barrier
      fresh/cracked, arch locked/open. `SpriteCompiler` lays 8 tiles to a row into 128x208–224
      atlases (105/104/106 tiles per theme, 315 total) and the manifest lists every tile's name
      and position. 19 new tests (516 total), `CHECK_OK themes=3 tiles=315`, build 0/0, harness
      unchanged.
- [x] 2026-10-20 — Card stacking fixes, movement cards, sprite compiler slice 1. Two caps that
      ignored the run's cards: `Campaign.HeartsOnNextFloor` now caps at
      `floor.MaxHearts + modifiers.MaxHearts`, and `GameFactory.CreateFloor` validates the carried
      hearts against the *composed* floor instead of the floor definition, so a ceiling a card
      raised can actually be used. New `CardEffect`/`RunModifiers` fields: `PlayerStepScale`,
      `InvulnerabilitySeconds`, `SprintStaminaScale`, and momentum's four numbers; `GameState`
      grows the momentum rule (straight-step ramp, turn penalty window). Four cards ship —
      Light Feet, Momentum, Sprint Lungs, Hollow Bones — taking the catalog to 13. `tools/AssetForge`
      is new: `PixelCanvas`, `Palette`, a dependency-free PNG encoder, `CaveTiles`, three themes,
      a committed atlas set and a `--check` pass. 48 new tests (497 total), build 0/0, harness
      `DRAFT_OK`/`DRAFT4_OK`/`MOVEMENT_OK`, `CHECK_OK themes=3 tiles=12`.
- [x] 2026-10-20 — Roguelike Phase 4: movement feel. `GameConfig.PlayerStepSeconds` (0 = instant,
      the test seam), per-floor values in `FloorDefinition` (0.11/0.10/0.09), one buffered input
      spent at the end of a step, `PreviousPlayer`/`PlayerStepFraction` and the killer's pair for
      render interpolation, and sprint as a stamina resource (1.4×, a 1.2 s bar that drains only
      while a step runs, 25% refill gate, refused while aiming or on an instant floor). The window reads walking as held keys and
      samples Shift; the renderer draws both figures and the lantern pool from the interpolated
      positions and shows a sprint bar once stamina is spent. 34 new tests (449 total), harness
      `MOVEMENT_OK … stamina=0.91 idleStamina=1.00` across 236 frames.
- [x] 2026-10-20 — Roguelike Phase 3: the card draft. `Card`/`CardEffect`/`CardCatalog` (9
      cards), `RunModifiers` with clamped composition over `GameConfig` (now a record, so a card
      cannot silently drop a tuned value), `Draft` + seeded `DraftFactory` with duplicate,
      incompatibility, vision-cap and short-run guards, and `Campaign` owning the offers, the
      held cards, the pending heals and the gate that blocks `TryAdvanceFloor` until the draft is
      resolved. Two new floor rules for two cards: `LureStunSeconds` and `LureVisionPenalty`;
      `ClearLure` now recomputes visibility, so an expired or consumed wisp stops lighting cells.
      Window draft screen (grid, wrapped faces, run totals) and a console draft prompt. 84 new
      tests (415 total), 224 renderer frames through two drafts and a four-offer layout at
      1.75–2.03 ms/frame (three runs).
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
      recomputed on movement) with 7 vision tests.
- [x] 2026-10-20 — Phase 6: `RaylibRenderer`, `RaylibLayout`, `Raylib-cs` 8.1.0,
      `Frontends/` + `Input/` split, `--console` and automatic console fallback.
- [x] 2026-10-20 — Play-test fix 3: barrier delay retuned from 15 s to 3 s (`ObstacleCost`
      25 → 5, one number), with the delay tests reading the shipped default.
- [x] 2026-10-20 — Play-test request 4: `BraidedMazeGenerator` removes `ExtraLoops` (8)
      interior walls after generation, adding loops while keeping the maze connected and the
      boundary closed. 8 tests: exact loop count, connectivity, perfect maze at zero loops,
      clamping when asked for more walls than exist, closed boundary, seed reproducibility,
      more loops leaving fewer walls, negative count rejected.
- [x] 2026-10-20 — Play-test request 5: the killer's distance-gated aura
      (`EnemyAuraDistance` 6, `EnemyAuraRadius` 2) is part of `IsVisible`, recomputed when it
      moves, with `EnemyGlow` for the renderer. 7 tests: absent while far, appears at the
      boundary, absent one step beyond it, radius 0, shines around a bend, still hides the rest
      of the maze.
- [x] 2026-10-20 — Play-test request 6: moonlit atmosphere from `references/image.png` —
      indigo night gradient, blue stone with a highlighted top edge, indigo shadow, warm
      lantern glow and dust motes in the light, violet killer aura, breathing light that
      quickens when the killer is near.
- [x] 2026-10-20 — Art pass 2: the whole window rebuilt as a bioluminescent cave — rounded
      stone tiles, rounded rock walls drawn in two passes so both sides of a shared wall keep
      their lit rim, moss and glowing lichen, cave mushrooms, a hooded lantern-bearer, a
      spindly killer, fungus keys, a stone arch exit and thorn barriers, plus green/pink/cyan
      spores, background blooms, grain and a vignette. Cells grew from 36 to 40 px.
- [x] 2026-10-20 — Chase-feel pass: `DeadEndBraider` (55% of dead ends opened, 57 → 18.8 per
      map, with the shared-wall case counted as two braids) plus a retuned `ExtraLoops` = 8.
      9 braider tests: no dead ends at 100%, one wall per dead end at most, about half at 50%,
      monotone in the share, perfect maze at 0%, connectivity, closed boundary, seed
      reproducibility, corridor ends skipped instead of throwing, bad percentages rejected.
- [x] 2026-10-20 — Chase-feel pass: barriers seal the cell (`BarrierSeals`, 10 s), the killer
      detours while a seal holds (`SealedBarrierCost` 40) and tears it down
      (`BarrierBreakSeconds`) when it is the only way; the husk stays at `ObstacleCost`. 8 seal
      tests: placement seals, zero seal time, expiry by the clock, cracked husk price, tear-down
      when it is the only route, destruction, no walking through while a detour exists, scaled
      step time, and three seals never wall the killer in.
- [x] 2026-10-20 — Chase-feel pass: the wisp lure (`LureCount` 2, `LureSeconds` 6) with
      `PlacementMode`, a shared aim cursor and a Dijkstra-comparison target rule. 12 lure tests:
      aiming mode, no charges, one at a time, charge spent, own-cell refusal, cheaper-route
      target, resume after consumption, ignored when the player is closer, expiry, frozen timer
      while aiming, its own light in the dark, and budget exhaustion. Aim API renamed to
      `AimCursor` / `MoveAimCursor` / `ConfirmPlacement` / `CancelPlacement` so both placeables
      share one path; `R` aims a wisp in both frontends.
- [x] 2026-10-20 — Chase-feel pass verified: build 0/0, 269/269 tests, 136 renderer frames with
      seals/wisps/reticles (`RENDER_OK`), 482 console frames, 0.70 ms/frame draw submission
      (2.40 ms full frame), window alive for 6 s, and the detour table above from a real
      measurement over 8 seeds.
- [x] 2026-10-20 — Title screen and replay: `RaylibRenderer.Title.cs` (partial: same palette,
      breathing light pools, drifting spores, hand-spaced wordmark, broken glowing rule, two-entry
      menu with pulsing selection and triangle chevrons), `RaylibRenderer.Reset()` so a replay
      does not inherit explored cells, a two-screen `RaylibFrontend` loop with `R`/Enter to
      descend again, an ASCII title and `[R] descend again` prompt in `ConsoleFrontend`, and the
      new `GameFactory` in Core. 10 factory tests: fresh playable game, both maze shapers wired,
      perfect maze with both off, seed reproducibility, unseeded variety, replay after death is
      independent and fully restored, one random source per run, invalid config, null arguments,
      null random source. Verified: 279/279 tests, 321 title/replay frames through the real
      renderer (`RENDER_OK`), 1.76 ms/frame title drawing, window alive on the title with an
      empty log, console run unaffected (title skipped when input is redirected).
- [x] 2026-10-20 — Roguelike Phase 1: hearts and damage. `GameConfig.StartingHearts` (3),
      `MaxHearts` (3), `InvulnerabilitySeconds` (1.5), `RespawnDistance` (8) and
      `RespawnStunSeconds` (1.5), all validated; `GameState` spends one heart per catch,
      teleports the killer to the furthest reachable non-objective cell (dead ends preferred),
      stuns it, and refuses a second hit while the grace window is open. Only the last heart
      sets `GameStatus.Caught`. The window shows hearts in the footer (dim when spent, pulsing
      on the last one) with a 0.35 s red damage wash; the console header prints `hearts n/n`
      and the ending says "The killer took your last heart." 22 new tests (301 total); 1,264
      renderer frames through three catches (`HEARTS_OK`, 1.87 ms/frame), 4,496 console frames
      ending at 0/3, window alive for 7 s.
- [x] 2026-10-20 — `FUTURE.md` reopened as a phased plan: nine phases, each ending in a
      playable build, with Phase 1 (hearts) marked done and the remaining phases (campaign,
      cards, movement, sprite compiler, pixel renderer, synth audio, themes, polish) listed with
      their gate checks.
- [x] 2026-10-20 — Play-test fix: keys were effectively invisible. `DrawCellDetails` painted the
      lantern falloff over the cell contents, so a key at the torch edge lost ~65% of an already
      faint glow; the falloff now goes on first and the contents (keys, exit, wisp, killer,
      player) are drawn above it. `DrawKey` was rebuilt as a beacon (floor pool, light shaft past
      the cell edge, pulsing ring, bobbing fungus with a white core, orbiting sparks). Verified
      with a window harness that walked to a key and drew 100 frames: `KEYS_RENDERED`, key
      visible, 2.27 ms/frame; 301/301 tests still green (no rule changed).
- [x] 2026-10-20 — Roguelike Phase 2: floors and campaign. `FloorDefinition` + `FloorCatalog`
      (The Shallows 3 keys / 0.60 s, Rootbound 3 / 0.54 s, Drowned Halls 4 / 0.48 s, each with a
      theme id), `CampaignStatus` and `Campaign` (floor sequence, carried hearts, `TryAdvanceFloor`,
      `Status` mapping a cleared floor to progress and a caught floor to `Lost`).
      `GameConfig.ForFloor` composes a floor over the run's shared rules; `GameFactory` gained
      `CreateCampaign`/`CreateFloor` and a `Func<int?, IRandom>` random factory so floor n uses
      `seed + (n-1)*7919`. Window: floor intro card, floor-cleared card, run summary, floor in the
      title bar, and a campaign loop; console: floor headings, carried hearts and run summary;
      both frontends take `Func<Campaign>`. 30 new tests (331 total); full-campaign renderer
      harness `CAMPAIGN_OK` (160 frames, 2.09 ms/frame), 3,753 console frames with the correct
      floor heading and loss summary, window alive for 8 s.

## Known Issues / Bugs

- **The audio is measured, not heard.** Every check that can run headless passes (all twenty
  tracks audible, inside the ceiling, in time, seamless, deterministic; all 22 effects distinct;
  the device drained at exactly real time), but nobody has listened to a run. The balance ratios,
  the reverb amounts and whether the motifs are memorable are taste calls. The one design
  deviation from `FUTURE.md` §8.3 is that the FX checks live in `MazeGame.Tests` rather than
  `AssetForge --check`: the recipes are in the UI assembly and the tool must not reference the
  game.
- **A floor's track starts a beat after play begins.** The score is arranged on a worker and
  adopted on the first frame of play (measured 0.36 s after `PlayFloor`), so the previous floor's
  music is still playing during a floor intro's last moments. A measured 2.4 ms to arrange and
  0.87 ms slowest frame is the fix for the static, but a human ear still has to confirm that no
  new glitch appears on a real device at a real buffer size.
- **The menus are still vector.** The play screen draws from the atlases, but the title, floor
  intro, draft grid and run summary are still the old primitive renderer, because the font atlas
  that would let them be drawn in pixels does not exist yet. The two look different on purpose for
  now; slice 2 of the phase replaces them.
- **The solid wall and autotile families have no consumer yet.** The 16 edge and 47 blob tiles
  describe a cell that *is* wall, which this maze does not have: walls live between cells, so the
  renderer uses the thin faces instead. They stay compiled and validated for the floors whose
  chasm and veto cells arrive in Phase 8.
- **The look is measured, not judged.** The harness proves the canvas is painted, the fog does not
  leak,  the lantern is the brightest pixel and the reticle lands (see the numbers above), but
  nobody has looked at a rendered frame and said whether it reads as a cave. The rig's proportions,
  the 6 px wall band (raised from 5 after a playtest said the walls blended into the path) and the
  new 0.18 floor / 0.50 wall ladder are measured — 2.5–8.3x drawn-band contrast across the twenty
  themes — not judged.
- **The shaped floors are unjudged as places.** `MazeShapeTests` and `ShapedFloorTests` prove an
  oval or blob floor is connected and keeps every key reachable, but whether a blob reads as a
  cave or as a maze with its corners chewed off needs a human in the window. The knobs are
  `MazeShape.BlobFill` (0.66) and `EllipseReach` (0.47).
- **No props or decals layer.** `FUTURE.md` §6.3 asks for mushrooms, puddles, bones and chains on
  their own layer with y-sorting; the atlas has no prop tiles yet, so the floor is bare tiles and
  the cast.
- **The PNG encoder does not compress.** Atlases are stored-deflate, so a 64x16 strip is ~4 KB
  instead of ~1 KB. That is deliberate for this slice (byte-identical output, no packages); if the
  committed sets grow, add a fixed Huffman pass and re-commit every atlas in one go.
- **Momentum is a guess.** 5% per straight step, capped at 25%, with 15% for 2 s after a turn.
  Nothing has played it: the card could feel like a reward for corridor-running or like a tax on
  corners, and those are opposite designs.
- **Light Feet's heart cost is invisible at full health.** The clamp never takes a heart the player
  is holding, so the card only bites once wounded or when a heal lands. That is intended, but the
  face text does not explain it.
- **Movement feel is unjudged by eye.** The cadence, the buffer and the interpolation are measured
  (`MOVEMENT_OK step=0.11s halfway=0.50`), but whether walking *feels* right — and whether the
  killer's slide reads as a creature rather than a drifting marker — needs a human at the window.
  The knobs are `FloorDefinition.PlayerStepSeconds` per floor and the two sprint numbers.
- **Sprint has no diegetic cost yet.** `FUTURE.md` §5.2 asks for a footstep ring in the fog while
  sprinting, and §5.3 for the killer turning toward your last sprint footstep. Neither exists:
  sprint currently costs only stamina, so its drawback is mechanical rather than visible.
- **The console cannot sprint or hold a direction.** A terminal key press is a single event, so
  `--console` movement stays one press per cell and Shift is unmapped there. The window is the
  only place the movement feel is real.
- **The console draft prompt was never driven interactively.** The window's draft screen is
  verified through the real renderer, and the campaign API behind the console prompt is covered
  by tests, but a piped console run can never clear a floor (the player cannot move), so the
  "press 1..n / S" branch has only been read, not exercised. A human at a terminal should run
  `--console`, clear floor 1 and take a card once.
- **The draft screen's look is unjudged.** Card sizing, the two-row layout above three offers and
  the wrapped face text are drawn and measured, but the pixels cannot be read here. The knobs are
  local to `RaylibRenderer.DrawDraft`/`DrawCardFace`.
- **Lucky Find is a mid-run card only.** Its price is paid one draft later, so the draft
  withholds it unless at least two drafts remain; it cannot appear in the last two drafts of a
  run. It is catalogued and tested, and its eligibility is guarded rather than left to the RNG.
- **Card balance is unproven.** Twenty-three trades were chosen for rule expressiveness, not for a
  tuned curve; `Glass Cannon` (25% faster for a two-heart catch) and `Debt of Sight` (+2 hearts for
  two steps less vision) are the two most likely to be either mandatory or a trap. The numbers are
  in `CardCatalog`.
- **Tiers are weighted, not hard-gated.** The draw shifts from commons towards rares as the run
  descends and every fifth floor guarantees a rare when one is eligible, but a lucky floor-1 draft
  can still contain one. A strict band gate is a later tuning decision, not missing machinery.
- **The campaign has no save yet.** Quitting mid-run throws the run away; the save file is
  Phase 8 because it depends on the final floor/card data. A run is currently short enough
  (three floors) that this is tolerable, but it must not survive into the twenty-floor build.
- **Floor "themes" are names and accent colours only.** The Shallows/Rootbound/Drowned Halls
  differ in text, accent, key count and killer pace, not yet in maze look or ambience. That is
  deliberate: the per-floor cave look needs the sprite compiler (Phase 6) and the palette work
  (Phase 8), so the current themes are a placeholder that must not be mistaken for done.
- **The key beacon cannot be judged from here.** Its draw path, visibility and cost are verified,
  but whether the shaft and ring read as "collect me" rather than as decoration needs a human
  look. The whole look is `RaylibRenderer.DrawKey`; the palette and radius are local constants.
- **Heart balance is unproven.** With three hearts, a 1.5 s grace window, a 1.5 s stun and a
  killer thrown eight steps away, a stationary player was caught every ~20 s in the harness
  (18.6 s, 20.0 s, 20.1 s). That is the intended "a mistake costs tempo" feel, but it may read
  as too generous for a horror game; the knobs are the five heart values in `GameConfig`, not
  code, and a play-test should decide.
- **Damage feedback is inferred, not emitted.** The renderer notices a drop in
  `GameState.Hearts` between frames and flashes for 0.35 s; there is still no `GameEvent`
  stream, so audio and particles would each have to reconstruct events for now. The event queue
  is scheduled with the audio phase.
- **The window's pixels could not be checked on this machine.** Window creation and the frame
  loop are verified (hundreds of renderer frames across every state, including the title screen
  and a replay, process stays alive), but reading the framebuffer back returns black because
  the build environment has no GUI session. A human must look at the window once and judge the
  atmosphere and the title layout; nothing else depends on that check.
- **A detour can be shorter than a tear-down.** Measured on 15x15, a seal that can be avoided
  costs the player only the extra walk (about 2 cells, ~1.2 s on average) while a seal that
  cannot be avoided costs 3 s. So a seal in a corridor is *better* than one in an open area,
  which is the intended skill expression but is the opposite of what "seal the route" sounds
  like. Tuning lives in `SealedBarrierCost`, `BarrierBreakSeconds` and `DeadEndBraidPercent`.
- **Vision radius 3 hides most of a 15x15 maze.** Intended horror feel, and the killer's aura
  now gives you a warning glow, but it is still the likeliest difficulty complaint
  (`VisionRadius`).
- **Barrier and wisp balance is unproven.** The seal (10 s), the tear-down (3 s) and the wisp
  (2 charges, 6 s) need a group play-test; the numbers are in `GameConfig`, and the measured
  diversion rate is in the table at the top of this file.
- **A torn-down barrier disappears instantly.** There is no break animation; the cell simply
  goes back to floor. The delay before it is what reads, and that is only proven by a human
  looking at it.
- **No audio yet**, and Raylib audio adds a device dependency; deferred until the scope
  question is answered.
- **Aura and torch can overlap.** When the killer is visible in your own light there is nothing
  special to mark it; the violet halo only appears once the aura rule is what makes it visible.
  That is deliberate, but it means the "danger" cue is quiet at very close range.
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
2. **Skill mechanic — answered, then revised:** the shipped kit is now **five one-key verbs**
   (Blink, Veil, Drag, Lantern, Shove) with per-floor charges. Barriers and wisps survive as
   floor rules and tests but are no longer placed by the player. The open question is which
   verbs the playtest actually reaches for, not how many mechanics to build.
3. **Braided mazes — answered for now:** shipped as two decorators (`DeadEndBraider` at 55% and
   `BraidedMazeGenerator` with 8 loops) on top of the graded perfect generator, which is
   untouched. Either can be switched off (`DeadEndBraidPercent = 0`, `ExtraLoops = 0`).
4. **Version control:** there is now a `.git` folder in the repository root (created outside
   this session); nothing has been committed by the agent. Confirm the team's policy before
   relying on it.
5. **Theme content:** the palette is settled (reference art), but enemy and item names are still
   placeholders.
6. **Audio:** Raylib audio is optional; confirm scope before Phase 7 because it adds a device
   dependency (and possibly asset files).
7. **Difficulty tuning:** enemy step 0.6 s, 3 keys, 3 barriers, 15x15, vision radius 3, aura
   distance 6, 8 loops. All of it lives in `GameConfig` and needs a group play-test.

## How to Build, Run, and Test

Run these from the repository root (the folder containing `MazeGame.sln`):

```bash
dotnet build                      # must report 0 Warning(s), 0 Error(s)
dotnet test                       # currently 748/748 passing
dotnet run --project tools/AssetForge -- --check   # atlases and themes are healthy
dotnet run --project src/MazeGame.UI              # Raylib window
dotnet run --project src/MazeGame.UI -- --console # terminal fallback
```

If `dotnet` is not on `PATH` (this is the case on the setup machine):

```bash
~/.dotnet/dotnet build
~/.dotnet/dotnet test
~/.dotnet/dotnet run --project src/MazeGame.UI
```

Controls: on the title, W/S or the arrows select and Enter/Space confirms. In play, WASD or arrow
keys move, **hold Shift to sprint** (window only; the console has no sprint yet), and the five
powerups are Space (Blink), V (Veil), T (Drag), L (Lantern) and F (Shove). At a gate, A/D selects a
condition and Enter takes it. Q or Esc quits; after a run, R or Enter descends again. The same keys
work in the window and in the terminal.
