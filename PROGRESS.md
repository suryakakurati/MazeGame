# PROGRESS

Single source of truth for the state of the project. Update this file at the end of
every work session (see the handoff protocol in `HANDOFF.md`).

Last updated: 2026-10-20 (Phase 6 slice 1: the floor is drawn from the committed atlases on a
virtual canvas)

## Current Status

The solution builds with zero warnings, `dotnet test` is green (574 tests), and the game runs
**in a Raylib window** as a **three-floor campaign with a card draft between floors and timed
steps**: the title leads to a floor intro card, then a seeded 15x15 maze drawn as a bioluminescent
cave — fog of war, braided dead ends, keys, an exit, sealed barriers, wisps, and a killer that
hunts the player with Dijkstra. **The player has three hearts**: a catch costs one, throws the
killer to a distant cell and stuns it, and keeps every key and barrier; hearts carry from floor to
floor, so the run is lost only when the last heart is. Walking is now a **cadence** — 0.11 s per
cell on floor 1, tightening as you descend — with one buffered input and **sprint on Shift** (a
1.2 s stamina bar). The explorer **snaps to its tile** (one press, one tile, so the grid is
countable while running) and the killer **slides** between its cells. Clearing an arch opens a **draft of three cards — take one or skip for a heart** — and
the third arch wins the run. The terminal view offered by `--console` (and used automatically when
no window can be opened) plays the same campaign in ASCII. The catalog holds **thirteen cards**,
including the four movement cards. The **sprite compiler is complete to its Phase 5 exit**: every
committed atlas under `src/MazeGame.UI/Content/Generated` holds the full tileset (floor variants,
wall, four thin wall faces, 16 edge and 47 blob tiles) and the whole cast, checked by `--check`.
**The floor is now drawn from those atlases**: a 16 px-per-cell virtual canvas, blitted to the
window at an integer scale, with a per-cell lightmap for the lantern and the fog. The menus, the
campaign cards and the HUD are still drawn by the vector renderer, which is what the font atlas
will replace next.

What changed in this session (Phase 6 slice 1 — the pixel renderer):

- **The floor is pixel art, at a whole-pixel scale.** `PixelLayout` sizes a virtual canvas from the
  floor (one 16 px atlas tile per cell, an 8 px margin, an 18 px header), `PixelView` renders into a
  `RenderTexture2D` and blits it with **integer** scale and letterboxing — 2x for the shipped
  656x760 window, 512x548 in the middle — so the art is never resampled by a fraction. A resize
  changes only the scale and the offset, never a coordinate inside the game.
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

## Current Task

**Phase 6 is two-thirds done.** Slice 1 (the play screen) is complete: the floor is drawn from the
committed atlases on a virtual canvas (`PixelLayout`, `PixelView`, `TileAtlas`, `AtlasManifest`,
`TileMask`, `PixelRenderer`) with a per-cell lightmap and thin wall faces.

Slice 2a (the art and the type) is done as of this pass:
- **Three shape families.** A theme carries a `TileFamily`; `TileGrammar` dispatches to
  `CaveTiles` (boulders), `WrongOrganicTiles` (roots, veins, membrane) or `WorkedStoneTiles`
  (courses, grout, carved bands), and the families share no drawing code. Rootbound and Drowned
  Halls are no longer the cave in a different hue, and `ThemeFamilyTests` compares palette
  *indices* so a recolour is a red test.
- **The compiled font.** `Glyphs` (5x7, ASCII 32–126), `FontAtlas` (6x8 cells plus the heart and
  pip icons) and `font.png` in the generated folder, with its own `theme`/`tile` section in the
  manifest; tile lines now carry an explicit width and height (default 16x16).
- **The campaign cards in that font.** `PixelFont` blits text and icons at an integer scale, and
  `PixelCards` draws the floor intro, floor-cleared, run-summary and caught cards. The vector
  versions are deleted, not kept side by side.

What is left of the phase: the **pictograms** (24x24 card art), the **UI chrome** (9-slice panels,
card frames, hearts in the canvas) and the **draft screen**, which is the last screen still set in
the system typeface.

## Next Steps

1. **Phase 6 slice 2b: the last vector screen and the chrome.** `Pictograms` (24x24 card art) and
   `UiForge` (9-slice panels, card frames) in `tools/AssetForge`, then the draft screen and the HUD
   redrawn with `PixelCards`/`PixelFont` and the vector chrome retired. That removes the last of the
   primitive drawing, and the window's top strip can shrink once the pills live in the canvas.
2. **Play a run and judge the new look.** The floor is real pixel art now, and the harness measures
   it (1.03 ms median, 62% of the canvas painted, no fog leaks) — but whether the 16 px tiles read
   as a cave at 2x, and whether the thin wall faces are thick enough, needs a human at the window.
   The knobs are `PixelLayout.CellSize`, `WallStrips.Thickness` and the light levels in
   `PixelRenderer.UploadLight`.
3. **Judge the movement.** Timed steps change difficulty more than any card does: instant movement
   was generous, and the first real question is whether 0.11 s per cell plus a 0.6 s killer still
   lets a player escape by reflex.
4. **Momentum needs a play-test of its own.** It is the first card that changes how the game
   *feels* rather than what it allows: 5% a step to +25%, and 15% slower for 2 s after a turn.
   The numbers are one record in `CardCatalog`; the shape (ramp vs. flat) is the open question.
5. **Get the window screens judged by eye.** The draft screen, the interpolated figures, the
   stamina bar and now the card faces (13 of them) are drawn through the real renderer, but no
   framebuffer can be read here (see Known Issues).
6. **Keep the seams clean.** `GameFactory.CreateFloor` remains the only place a floor is built
   (now with the run's modifiers), and `Campaign` the only place floors, hearts and drafts are
   sequenced. Movement timing lives in `GameState`, never in a frontend.
7. Re-run `dotnet build`, `dotnet test`, the renderer harness and a window session after every
   phase; update `PROGRESS.md` and the phase table in `FUTURE.md`.

## Completed

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

- **The menus are still vector.** The play screen draws from the atlases, but the title, floor
  intro, draft grid and run summary are still the old primitive renderer, because the font atlas
  that would let them be drawn in pixels does not exist yet. The two look different on purpose for
  now; slice 2 of the phase replaces them.
- **The solid wall and autotile families have no consumer yet.** The 16 edge and 47 blob tiles
  describe a cell that *is* wall, which this maze does not have: walls live between cells, so the
  renderer uses the thin faces instead. They stay compiled and validated for the floors whose
  chasm and veto cells arrive in Phase 8.
- **The look is measured, not judged.** The harness proves the canvas is painted, the fog does not
  leak, the lantern is the brightest pixel and the reticle lands (see the numbers above), but
  nobody has looked at `/tmp/mzgame-pixel-shallows.png` and said whether it reads as a cave. The
  rig's proportions, the 5 px wall thickness and the light levels are all guesses until then.
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
- **Lucky Find cannot appear in the shipped run.** Its price is paid one draft later, and a
  three-floor run has no second draft after its first, so the draft withholds it rather than offer
  a free upgrade. It is catalogued and tested, and appears in runs of four or more floors. That is
  deliberate (see `DECISIONS.md`) but means one of the thirteen cards is currently unreachable.
- **Card balance is unproven.** Thirteen trades were chosen for rule expressiveness, not for a tuned
  curve; `Blood Pact` (+1 heart for a 12% faster killer) and `Deep Roots` (18 s seals for one
  barrier fewer) are the two most likely to be either mandatory or a trap. The numbers are in
  `CardCatalog`.
- **Tiers do not gate by floor yet.** With three floors, the planned bands (commons 1–8, uncommons
  from 5, rares from 11) would collapse onto floor 1, so the tier currently weights the draw and
  colours the card instead. The bands arrive with the twenty-floor campaign.
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
2. **Skill mechanic — answered:** three barriers that seal for 10 s and then become a 3-second
   toll, plus two wisps that pull the killer off its route for 6 s. Both share one aiming mode.
   Whether *two* mechanics is too much for the course demo is a team decision.
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
dotnet test                       # currently 574/574 passing
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
keys move, **hold Shift to sprint** (window only; the console has no sprint yet), `B` or Space aims
a barrier, `R` aims a wisp (then WASD moves the cursor, B/Space/Enter places it, Esc cancels), and
Q or Esc quits. After a run, `R` or Enter descends
again. The same keys work in the window and in the terminal.
