# FUTURE — from maze game to roguelike descent

**Status:** design brief only. Nothing in here is implemented, and this file changes no code.
It exists to decide what to build, in what order, and what it will cost.

The asks behind it:

1. **Make it a roguelike.** Three hearts instead of one life, a card draft with real upsides
   and drawbacks between floors, and movement that feels smooth enough to want to master.
2. **Make it look like an indie game.** Pixel art, a maze that reads as a carved grotto map
   rather than a tinted grid, and a different theme for every floor.
3. **Make it sound like one.** Twenty music themes in a fixed, escalating order, music that
   reacts to the chase, and impact sounds for damage and near-misses.
4. **Do not depend on anyone else's art.** No artist, no commissioned music, no asset packs,
   no downloads, no licences. Every sprite, every glyph and every note is produced by code
   that lives in this repository.

That fourth point is the one that changes the plan, so it gets its own sections: **6.6–6.10**
for the sprite compiler and **8.4–8.5** for the synthesiser. The short version is that the
game ships with a small content-generation toolchain instead of an `Assets/` folder. That is
more engineering than sourcing files would be, and it is the only honest way to promise the
game is finished without you drawing anything.

**Contents:** [TL;DR](#0-tldr) · [Baseline](#1-where-the-game-is-now-the-honest-baseline) ·
[Target](#2-the-target-experience) · [Run structure](#3-run-structure) · [Cards](#4-cards) ·
[Movement](#5-movement-and-skill-expression) ·
[Art](#6-art-direction-pixel-grotto-compiled-from-code) · [Themes](#7-themes-twenty-floors) ·
[Audio](#8-audio-a-score-that-is-synthesised-not-sourced) ·
[Architecture](#9-architecture-and-code-impact) · [Balance](#10-starting-balance-numbers) ·
[Build plan](#11-build-plan-and-milestones) · [Scope ladder](#12-scope-ladder-if-time-runs-out) ·
[Risks](#13-risks) · [Decisions](#14-open-decisions-recommended-defaults-in-bold) ·
[Done](#15-definition-of-done-does-it-feel-like-an-indie-game) ·
[Appendix](#16-appendix-the-generation-stack-at-a-glance) · [What I need](#17-what-i-need-from-you) ·
[Powerups and variance](#18-second-playtest-a-new-powerup-kit-and-descent-variance)

---

## 0. TL;DR

- **The run becomes the unit of play.** A run is 20 themed floors, roughly 40–60 minutes,
  with three hearts, one card draft per floor, and no game over until the hearts are gone.
- **The biggest technical change is not hearts, it is the *run* object.** Today one
  `GameState` is the whole game. A roguelike needs a `Campaign` above it that owns the floor
  index, hearts, cards and the save file. That single addition unlocks everything else.
- **There is no art or audio to source, and no asset folder.** The look comes from a
  deterministic **sprite compiler** (`tools/AssetForge`) that rasterises 16x16 tiles,
  characters, props, UI and a 5x7 bitmap font from shape grammars and per-theme parameter
  sets. The sound comes from a **software synthesiser** in the UI project that generates the
  twenty scores live and renders the sound effects at startup. Both are ordinary C# with no
  packages, no network, no binary inputs.
- **This trades a commissioning bill for a generator bill.** The art and music still have to
  be *designed* — the design just moves into code (palettes, grammars, motifs, arrangements)
  where it is testable, diffable, and free to vary twenty times.
- **The look does not need 20 tilesets or 20 atlases.** One atlas per family (three families)
  plus 20 palette-and-parameter sets. Every theme also gets a signature tile, a signature prop
  and a signature hazard, which is what actually makes floors feel different.
- **Recommended first milestone is a vertical slice, not a system sweep:** 3 floors, 9 cards,
  one family, the compiler and one score, hearts and draft working end to end. It answers
  "does this feel good?" before generating 20 of anything.

---

## 1. Where the game is now (the honest baseline)

Facts worth designing around, all from the current build:

| Thing | Today |
| --- | --- |
| Tests | 279 passing, 0 warnings, 0 errors |
| Maze | 15x15, perfect Kruskal + 55% dead-end braid + 8 loops |
| Entities | 3 keys, exit, killer at 0.6 s/step, 3 barriers, 2 wisps, 1 player |
| Player | moves **instantly** one cell per key press, no cooldown |
| Vision | radius 3 through passages; killer aura 6 / 2 |
| Lose condition | `GameStatus.Caught`, game over, end card |
| Screens | title, game (end card overlays the last frame) |
| Window | 656x760 at 40 px cells, whole maze always visible |
| Renderer | ~1,060 lines of procedural Raylib drawing, ~0.7–1.0 ms/frame |
| Assets | **none** — every pixel is drawn by code at runtime, no files loaded |
| Audio | none |
| Save | none, but every game is fully determined by a seed |

**What is already reusable:** the maze decorator pattern (`DeadEndBraider`,
`BraidedMazeGenerator`), `GameFactory` as the "build one game" seam, `GameConfig` as the
per-floor parameter block, the hand-built `DijkstraPathfinder` (which the card design below
uses for real, not decoratively), the screen-state frontend, and the test style (hand-built
corridors plus a random-play invariant fuzz).

**What is missing for a roguelike:** a run object, hearts, floors, cards, event hooks for
audio, a content-generation toolchain, any persistence.

**Why the current look reads as "a nicely coloured grid":** every cell draws its own rounded
rock slab and its own wall strokes, so walls have no continuous silhouette; there is no sub-cell
detail, no autotiling, no landmark or room structure, no texture grid, and all 15x15 of it is
equally visible at once. It is a very polished *diagram* of a maze. Pixel art fixes this by
locking everything to a 16 px grid, joining walls into continuous forms, and giving each theme
one strong silhouette idea.

**Why "no assets" is not a disadvantage here:** the game already ships zero asset files, and
its renderer is already a procedural drawing engine. Turning that engine into a sprite
compiler is a promotion of what exists, not a replacement of a content pipeline. It also means
a theme is a 30-line parameter block instead of a folder of PNGs — which is the only way one
person ships twenty visually distinct floors.

---

## 2. The target experience

**One sentence:** a forty-to-sixty minute descent through twenty increasingly wrong places,
where each floor is a puzzle box you learn, and the thing hunting you is always one mistake
away.

Five pillars, in priority order:

1. **A run you can lose and want to start again.** Hearts make mistakes survivable; cards make
   each run different; twenty floors give the run an arc.
2. **Choice with teeth.** Every card is a trade. No card is free, none is a pure trap either:
   the drawback must be something the player can play around.
3. **A place, not a board.** Pixel art, one silhouette idea per theme, sound that tells you
   where you are and what is coming.
4. **Skill you can feel improving.** Smooth timed movement, readable enemy telegraphs, tools
   that reward predicting the chase instead of reacting to it.
5. **Escalation.** Floor 20 should feel like the game has been building to it since the title
   screen, in art, mechanics and music.

---

## 3. Run structure

### 3.1 The run

```
TITLE -> FLOOR INTRO -> PLAY -> ARCH -> FLOOR CLEAR -> CARD DRAFT -> next floor ...
                                                                    |
                                             (hearts hit 0) RUN OVER -> SUMMARY -> TITLE
```

- **20 floors per run, fixed order, one theme each.** Fixed order is the point: the player
  learns that floor 13 means teeth, and the twentieth floor is a promise the game keeps.
- **Per-floor target: 2–3 minutes.** 20 floors is 40–60 minutes, which is a normal roguelike
  run and already the maximum I would ask of a single sitting for a course project.
- **The floor's exit is the win condition for that floor**, not the run. Reaching it opens the
  draft and the next floor. The run is won on floor 20's exit.
- **No backtracking, no shops, no currency.** Hearts and cards are the only economy. That
  keeps the systems count low enough to finish and to explain in a demo.

Per-band difficulty (the exact numbers live in `GameConfig`-equivalent per floor data):

| Floors | Maze | Keys | Killer step | Player step | Extra hazards |
| --- | --- | --- | --- | --- | --- |
| 1–4 | 13x13 | 2 | 0.62 s | 0.11 s | none (teach one per floor) |
| 5–8 | 15x15 | 3 | 0.56 s | 0.11 s | one theme hazard |
| 9–12 | 17x17 | 3 | 0.50 s | 0.105 s | two hazards |
| 13–16 | 17x17 | 4 | 0.46 s | 0.10 s | hazard + gate |
| 17–19 | 19x19 | 4 | 0.42 s | 0.10 s | hazard + gate + darkness |
| 20 | 19x19 | 5 | 0.38 s | 0.10 s | all of it, plus the heart |

Note the player step time barely moves: speed *cards* are where the player gets faster, so
speed feels earned rather than granted by the floor number.

### 3.2 Hearts

- **Three hearts**, maximum three at the start. Cards may raise the cap to five (rare) or
  spend one.
- **A catch costs one heart and the chase resets, it does not end the floor.**
  1. Hit-stop (~120 ms) and a red palette flash.
  2. The player keeps their cell. The killer is teleported to the furthest cell at least
     `RespawnDistance` (8) steps away, preferring a dead end, and is stunned for 1.5 s.
  3. The player is invulnerable for 1.5 s (killer cannot catch again in that window) and the
     killer's route is invalidated.
  4. Keys, barriers, wisps, cards and the floor layout are **untouched**. Damage costs tempo,
     not progress, which is what makes it survivable without making it toothless.
- **Zero hearts ends the run** (not the floor). The death summary shows floor reached, keys,
  cards, time.
- **Anti-softlock rule:** if the only cells far enough away are unreachable, the killer is
  teleported to the furthest reachable dead end and its route is recomputed; if the maze has
  no dead end at all, the furthest cell. The existing "killer always has a route" invariant
  test carries over and must also cover the respawn placement.
- **Why hearts and not a health bar:** three discrete hearts are readable at a glance in a
  pixel HUD, and each loss is a story ("that was the third time I misread floor 9").

Implementation consequence: `GameStatus.Caught` stops meaning "game over". The enum becomes
something like `InProgress`, `FloorCleared`, `Lost`, and the old `Caught` value either goes
away or becomes `Downed` for one frame before the respawn. Eight test files reference
`GameStatus.Caught` today (18 assertions); they are the migration list, and most of them are
*improved* by asserting "losing a heart keeps the game running".

### 3.3 Floor clear, draft, next floor

1. Step on the unlocked arch: `FloorCleared`. The clock stops, the screen freezes on the
   moment, and a floor-clear line plays.
2. **Results strip:** keys, time on the floor, hearts remaining, cards held. No star ratings;
   the roguelike does not need a score, it needs the next decision.
3. **Draft:** three face-up cards, keyboard selection, one pick (see section 4). The next
   floor's theme is teased behind the cards, so the player knows what they are preparing for.
4. **Floor intro card:** theme name, one-line mood ("FLOOR 9 — HIVE OF LANTERNS"), and the
   floor's hazard in a single pictogram. This is also the loading beat for the next track.

### 3.4 Suspend and resume, for free

Because every game is generated from one random stream, a run can be saved as:

```
{ formatVersion, seed, floorIndex, hearts, cardIds[], elapsedSeconds }
```

Everything else (maze, entity placement, draft offers) is regenerated deterministically. That
is genuinely cheap to build and is the single highest value-per-line feature in this document:
a 40-minute roguelike needs a "quit and come back" that does not throw the run away. The
`formatVersion` field matters because any change to generation order or card RNG order
invalidates old saves; bump it and refuse to load rather than load nonsense.

### 3.5 What "won" means

Floor 20's arch ends the run with a summary. A later "ascension" mode can replay the same
20 themes with modifiers; do not build it before the base run is fun.

---

## 4. Cards

### 4.1 Draft rules

- **Three offers, pick one, after every floor.** No reroll in v1.
- **Offers are seeded** from `{runSeed, floorIndex}`, so a seeded run is fully reproducible
  and the save file stays tiny.
- **Tiers:** common (floors 1–8), uncommon (5–15), rare (11+), with a guaranteed rare or
  curse-bargain offer on floors 5, 10, 15 and 20.
- **No duplicates** of a card the player already holds, unless the card is explicitly
  stackable (marked in the catalog).
- **Skip = heal.** Taking no card restores one heart (up to the cap). This makes a low-health
  run a real decision instead of a forced card.
- **Curse cards** are never offered alone; they appear only as the price of a strong rare
  ("take this rare and this curse"), so the player always chooses the trade.

### 4.2 Design rules for drawbacks

A card is only good if its cost is legible in the next thirty seconds:

1. **Mechanical, not hidden multipliers.** "The killer's aura reaches 2 steps further" beats
   "-10% enemy pathfind weight".
2. **Playable around.** The drawback changes *how* you play ("no barriers this run" is fine if
   the upside is big; "you cannot move diagonally" is not, because there is no diagonal).
3. **Never take away the tools the floor teaches.** A card that removes wisps on floor 18 is a
   trap unless the player is told at pick time what floor 19 costs.
4. **No pure upgrades.** Exception: the floor-20 reward, which should feel like a victory lap.
5. **Pair with the theme.** A curse that is mild in open caves and brutal in the flooded
   floors is good design if the player can see both.

### 4.3 Starting catalog (28 cards)

Tier: C = common, U = uncommon, R = rare, X = curse. Every row is a real trade.

| # | Card | Upside | Drawback | Tier |
| --- | --- | --- | --- | --- |
| 1 | **Wide Eyes** | Vision radius +2 | Killer aura distance +3 (it notices you from further) | C |
| 2 | **Dowsing Pulse** | Every 8 s the nearest key glows for 0.5 s | Each pulse also pings your cell to the killer for 2 s | C |
| 3 | **Cartographer** | Exit direction shown as a small arrow | Key counter hidden; you must remember what you have | C |
| 4 | **Tunnel Vision** | Vision +2 while in a straight corridor | Vision -2 at junctions and rooms | C |
| 5 | **Echo Map** | A third of the floor's walls are revealed at the start | The killer starts inside the revealed third | U |
| 6 | **Deep Roots** | Seals last 18 s instead of 10 | One barrier fewer per floor | C |
| 7 | **Thornwright** | +1 barrier per floor | Seals last 4 s | C |
| 8 | **Smoke Wisp** | Wisps pull the killer for 10 s | Vision -2 while a wisp burns (you are watching it) | U |
| 9 | **Twin Flame** | Two wisps may glow at once | Each wisp lasts 3 s | U |
| 10 | **Bait** | A wisp stuns the killer for 1 s when consumed | One wisp fewer | C |
| 11 | **Light Feet** | Player step time -15% | Max hearts -1 | U |
| 12 | **Momentum** | Speed ramps to +25% while running straight | Speed drops 15% for 2 s after every turn | U |
| 13 | **Sprint Lungs** | Hold Shift for +40% speed (stamina) | Footsteps double the killer's aura range while sprinting | C |
| 14 | **Featherfall** | The first catch each floor does no damage | Barriers cost two charges | R |
| 15 | **Second Wind** | Heal 1 heart now | Vision -1 for the rest of the floor | C |
| 16 | **Blood Pact** | Max hearts +1 | Killer step time -12% | U |
| 17 | **Hollow Bones** | Invulnerability after damage lasts 3 s | Player step time +25% | U |
| 18 | **Bloom** | Revive once at 1 heart when the run would end | Vision -2 for the rest of the run | R |
| 19 | **Shortcut** | Exit is placed ~5 steps closer | One extra key on every future floor | C |
| 20 | **Greedy Grasp** | Keys within 1 step are collected automatically | Keys spawn one cell further from the exit | U |
| 21 | **Trophy Hunter** | Heal 1 heart on every floor clear (up to one over the cap) | Every future floor needs one more key | R |
| 22 | **Predator's Patience** | Killer is 15% slower while it has not seen you | Killer is 10% faster while its aura is on you | C |
| 23 | **Sealed Fate** | You may seal the killer's own cell, stunning it 2 s | Barrier budget is 1 for the run | R |
| 24 | **Lucky Find** | This floor's draft offers four cards | The next floor's draft offers two | C |
| 25 | **The Debt** | Take a rare card of your choice | A curse card is added: **Quiet** (vision -1, aura +2) | R |
| 26 | **Quiet** (curse) | - | Vision -1, killer aura distance +2 | X |
| 27 | **Heavy** (curse) | - | Player step time +15%, barrier budget -1 | X |
| 28 | **Blind Lantern** (curse) | - | Remembered cells are not drawn; only what you can see now | X |

Shipped early (2026-10-20), once timed player steps existed: **Light Feet**, **Momentum**,
**Sprint Lungs** and **Hollow Bones** — the catalog is now 13 cards. Momentum is the only one that
needed a rule rather than a number (straight-step ramp, turn-penalty window), and it is the one
most likely to need re-tuning by feel.

Two rules for the catalog itself: every card gets a unit test for **both** halves of its
trade, and every card must state its drawback in the same sentence as its upside on the card
face. "Vision +2, it sees you sooner" is the whole card, not fine print.

Card **art** is generated too: every card has an id that maps to one of ~20 24x24 pictogram
generators (eye, pulse, root, thorn, flame, feather, heart, key, clock, bone, gate, mask…),
recoloured by tier. No card ships with a blank face.

### 4.4 How cards reach the rules

Cards should not be a pile of `if` statements in `GameState`. The clean shape is:

- A **card catalog** (data) with ids, tiers, text and a list of modifier deltas.
- A **run modifiers** object built from the chosen cards, composed *over* the floor's
  `GameConfig` when the floor is created.
- **Clamped composition** with validation: vision never below 1, budgets never below 0,
  step times never below 0.05 s, aura distance never nil. Invalid compositions must fail
  loudly in tests, not silently produce a broken floor.
- The floor itself is still an ordinary `GameState` built by the factory, so all 279 existing
  rule tests keep meaning what they meant. Cards change the *inputs*, not the rules.

This is also where the assignment angle is strongest: cards like **Dowsing Pulse** and
**Predator's Patience** are implemented with the same `DijkstraPathfinder` (distance to the
nearest key, distance from the killer). The graded algorithm keeps earning its place.

### 4.5 Guards

- **Anti-synergy checks in tests:** "no barriers" + "barriers last 18 s" must be prevented at
  the draft (one card marked incompatible) rather than producing a dead card.
- **Rate limits:** at most two cards may modify player speed, at most two may modify vision,
  otherwise a lucky run walks off the curve.
- **Display the total:** the draft screen shows the run's current modifiers
  ("SPEED +15%, VISION +1, AURA +3") so the player can reason about compounding.

---

## 5. Movement and skill expression

### 5.1 Smooth movement

Today a key press teleports the player one cell, and the renderer draws cells, so the game
looks like a slideshow of states. The fix is **rule-side steps with render-side interpolation**:

- **Rules stay grid-based.** Collision, keys, damage and pathfinding remain cell to cell.
  Interpolation is purely visual, so a catch can never happen "between cells".
- **Timed steps.** The player gets a `player step time` (about 0.10–0.11 s). Holding a
  direction walks at that cadence; a press is buffered for one step so the controls stay
  responsive without allowing faster-than-cadence movement by mashing.
- **Visual lerp.** The renderer draws the player between the previous cell and the current
  one over the step duration, with the lantern glow trailing slightly behind. The killer gets
  the same treatment, which finally makes its approach feel like a creature rather than a
  marker hopping.
- **Turn anticipation.** Because the sprite is 16x24 with four facings, direction changes
  snap the facing immediately even if the body is still slightly behind.

This is the change that will alter difficulty the most: instant movement is generous, timed
movement has a cost per decision. The starting numbers in section 10 assume the player step is
~5.5x faster than the killer's, which preserves roughly today's escape feel while making speed
cards meaningful.

### 5.2 Speed as a stat

- **Base player step time** is a per-floor config value, not a card.
- **Cards modify it multiplicatively** (Light Feet, Momentum, Hollow Bones, Heavy).
- **Sprint** (hold Shift) is a stamina resource, not a card: it makes the *first* half-second
  of an escape better and the rest worse, which is a skill expression rather than a stat.
  Sprinting should be visibly loud (footstep ring in the fog) so its drawback is diegetic.
- **The killer's speed is the pressure dial.** It rises per floor band and some cards slow or
  speed it. Keep those modifiers small: +/-15% is a whole difficulty tier on a 0.5 s step.

### 5.3 Enemy readability

A roguelike can only be fair if the hunter telegraphs. Proposed, in order of value:

1. **Aura already exists** (pink light within 6 steps), keep it and make it pixel-art noise.
2. **Lunge tell:** when the killer closes to 2 steps it pauses for 0.25 s and its eyes flare.
   The pause is a chance to turn; the lunge is what catches you if you keep running straight.
3. **Listening:** the killer's head turns toward your last footstep when you sprint. Visual
   only in v1, mechanically "sprint raises your noise radius" in v2.
4. **Per-theme variants** later: a slow hunter that splits, a hunter that ignores wisps, a
   hunter that only moves when you do. Each is a small `IEnemyBehavior` once a second one
   exists; do not build the abstraction before the second behaviour is actually designed.

### 5.4 Skill checklist (what the player should get good at)

- Reading fog: knowing which branch is a dead end from the shape of what is lit.
- Baiting: dropping a wisp so the killer commits to the wrong corridor, then taking the arch.
- Sealing a corridor at the right moment rather than the safe one.
- Card math: taking a drawback that this floor punishes least.
- Sprint discipline: spending stamina to break line of sight, not to feel fast.
- Route planning under a key count that changes with cards.

If a proposed feature does not sharpen one of those, it is decoration.

### 5.5 Fairness rules

- **No damage without a telegraph** (aura, lunge tell, or a visible hazard).
- **Damage never removes the run's progress** (keys, cards, floor).
- **Every hazard is visible before it can hurt you** (fire vents glow one beat early).
- **The floor is fully connected and the exit reachable at all times**; the existing
  invariant tests extend to "reachable with the current card modifiers".

---

## 6. Art direction: pixel grotto, compiled from code

### 6.1 Diagnosis

The current renderer is good at what it does (rounded rock, moss, spores, a lantern), but it
draws each cell independently. That is why the maze reads as a *grid*: no wall is continuous
with its neighbours, every floor slab is a separate rounded rectangle, and there is no
texture resolution below the cell. A pixel tileset fixes all three at once because it lives
at 16 px, autotiles walls into continuous shapes, and gives every theme a distinct silhouette.

### 6.2 Target style

- **16x16 pixel tiles**, characters 16x24, 4 facings, 3–4 frames per action.
- **Fixed virtual canvas, integer scaling.** Render the whole game into a `RenderTexture2D`
  at about 336x400 (21x21 tiles for a 19x19 floor plus HUD) and blit it to the window at an
  integer scale (2x or 3x) with a point filter and letterboxing. Never fractional scale, never
  a sharpening filter. The window becomes resizable; the art stays crisp.
- **One strong shape idea per theme.** Flooded floors read as flooded because of reflected
  light and water edges, not because of a blue tint. Bone floors read as bone because walls
  are stacked ribs. Palette alone is not a theme.
- **Whole floor always visible**, as today. That is a feature for a maze game and it avoids a
  camera and a minimap. Floors stay 13x13 to 19x19 so they fit the canvas.
- **Pixel font** (5x7 bitmap, rendered at 1x and 2x) for everything, including the HUD and
  cards. No TTF at runtime; the current default font is the single clearest "prototype" tell
  in the game.

### 6.3 Layers and autotiling

Draw order, all inside the virtual canvas:

1. **Floor/base** tiles (4 variants per theme, hash-chosen per cell).
2. **Walls**, autotiled. A 16-tile edge mask for worked stone, and a 47-tile blob set generated
   for organic families by a corner rule; both are generated, neither is hand-drawn.
3. **Props/decals** on their own layer: mushrooms, puddles, bones, chains, runes, roots.
   Per-theme lists, placed by hash so a floor is stable across frames.
4. **Entities** (player, killer, keys, wisps, barriers) with y-sorting against props.
5. **Lightmap**: a small texture (one texel per cell is enough) multiplied over the scene,
   carrying the lantern falloff, the killer aura tint and the theme's ambient level. This
   replaces the current per-cell gradient calls and is both prettier and cheaper.
6. **Fog**: remembered cells drawn at a dark palette (not black), current cells at full light.
7. **HUD and overlays** in the virtual canvas, so they share the pixel grid.

### 6.4 The maze's silhouette is a generator problem

A tileset cannot make a boring layout look like a grotto. The layout itself needs structure,
and this is where the existing decorator pattern pays off. Add decorators over
`MazeGenerator` (still untouched):

- **`RoomCarver`**: opens a handful of 3x3 to 5x5 chambers, so floors have rooms as well as
  corridors. This alone changes the read of the map more than any shader.
- **`LandmarkPlacer`**: one or two special cells per floor (a shrine, a chasm, a collapsed
  bridge) that the tileset can decorate and the player can orient by.
- **`ChasmCarver` / vetoes**: spaces that are not walkable and read as negative space, making
  the walkable maze a shape rather than a rectangle.
- **Theme hazards** as cell sets (`WaterCells`, `FireVents`, `GateCells`) generated per floor,
  not painted by the renderer, because they change rules.

Depth cues that make it read as carved rock: darker floor near walls (cheap ambient
occlusion), a 1-tile dark "inside" edge on wall runs, and a light pool under the lantern.

### 6.5 Characters

- **Player:** 16x24 hooded figure, lantern in the leading hand; 4 facings x 3 frames walk,
  1 idle (breathing), 1 damage, 1 victory. The lantern is the readable part: it should be the
  brightest pixel on screen.
- **Killer:** 16x24 spindly silhouette, 4 facings, 3 frames of a wrong-looking gait, 1 lunge,
  1 stun. Two pale pixels for eyes, nothing else. Theme variants recolour the rim light and
  add a detail (bones, tendrils, a crown).
- **Keys, wisps, barriers, arch:** 16x16 animated, with 2–3 frames each.

All of these are drawn by the same rig code with per-theme palettes and a few silhouette
parameters, so a new floor's cast costs a parameter block, not a sprite sheet.

### 6.6 No external assets: the sprite compiler

**`tools/AssetForge`** is a plain console project (no packages, `net8.0`, references Core only
for `SeededRandom`) that writes PNGs and a manifest into
`src/MazeGame.UI/Content/Generated/`. The game loads the PNGs at startup; the compiler is not
needed at runtime and is not shipped.

Why compile to PNG instead of drawing every frame like today? Because 16 px pixel art is a
*sampling* problem: at runtime the renderer would have to redraw thousands of tiny primitives
per frame and fight the blend modes to stay pixel-exact, whereas an atlas is a single blit per
tile, crisp by construction. It also makes the art inspectable (a PNG you can open), diffable
(hashes in tests) and cacheable (compile only when a theme changes).

The compiler's internals are deliberately boring:

- **`PixelCanvas`** — a 16x16 (or WxH) `byte[]` of palette indices, plus `Rect`, `Line`,
  `Ellipse`, `Speckle`, `Dither`, `Shade`, `Outline`, `Mirror`, `Stamp`, and `ToPng`. Integer
  coordinates only, no anti-aliasing, alpha is 0 or 255.
- **`Palette`** — 16 entries per theme, with named roles (`floor`, `floorDark`, `wall`,
  `wallTop`, `wallEdge`, `moss`, `accent`, `glow`, `flesh`, `bone`, `water`, `hazard`,
  `uiText`, `uiDim`, `shadow`). Artists normally pick these; here a theme picks hues and the
  compiler derives the rest with fixed lightness steps, so contrast is guaranteed rather than
  hoped for.
- **`TileGrammar`** — three families, each a set of small drawing routines that take
  `(palette, familyParams, seed)` and fill a canvas. A family is ~150 lines; a theme is ~25
  parameters. Twenty themes is twenty parameter blocks, not twenty atlases.
- **`Rig`** — the character builder: head, torso, limb offsets, cloak hem, lantern position,
  plus a frame table (`idle`, `walk0..2`, `hit`, `lunge`, `stun`). Four facings come from
  mirrored/rotated limb offsets rather than four drawings.
- **`Glyphs`** — the 5x7 font as stroke data (`'A' = [(0,6)-(0,2), (0,2)-(2,0), ...]`),
  rasterised into an atlas with a 1 px gap. 95 printable ASCII glyphs, plus hearts, keys,
  arrows, pips, chevrons as extra cells.
- **`Pictograms`** — the 24x24 card icons and 8x8 HUD icons, built from the same primitives.
- **`Autotiler`** — generates the 16 edge tiles and the 47 blob tiles by taking a filled wall
  canvas and carving the open sides/corners, so tile *n* provably matches mask *n*.
- **`UiForge`** — 9-slice panels (corner motifs per family), card frames per tier, title
  plaque, heart states, a 3-frame "shatter" strip.

Commands, in the spirit of the existing CLI style:

```
dotnet run --project tools/AssetForge -- --all
dotnet run --project tools/AssetForge -- --theme hive --sheet
dotnet run --project tools/AssetForge -- --check      # validation only, non-zero on failure
```

Generated output is committed, so a fresh clone builds and runs with no tool step. A test
asserts that re-running the compiler on the same inputs produces byte-identical files
(determinism), so a drifted atlas is a red test rather than a mystery.

**Status (2026-10-20):** the compiler is in `tools/AssetForge` — `PixelCanvas`, `Palette`, `Png`,
`CaveTiles`, `WallStrips`, `Autotiler`, `Rig`, `ThemeCatalog` (shallows / rootbound / drowned) and
`SpriteCompiler` with `--all`, `--theme <id>` and `--check`. Each theme's atlas holds floor
variants, the plain wall, the four thin wall faces, the 16 edge and 47 blob tiles, the full cast
and its props: 109 / 108 / 110 tiles in 128x224 PNGs listed in `manifest.txt`. `WallStrips` exists
because this maze stores walls *between* cells: a 5 px face along a closed edge is the shape the
renderer can use, while the solid wall and its autotiles are for the later floors whose
non-walkable cells are wall. **The pixel renderer now loads these atlases** (see 11.2 M4); what is
*not* there: `Glyphs`, `Pictograms`, `UiForge`, and the worked-stone and wrong-organic families.

### 6.7 One atlas, twenty theme parameter sets

Three families, twenty parameter sets:

| Family | Themes | Shapes | Signature tile | Signature prop |
| --- | --- | --- | --- | --- |
| **Organic cave** | shallows, rootbound, drowned, sporefall, hive | rounded boulders, moss, drips, wet edges | glowing cap / root knot | mushroom cluster, drip column |
| **Worked stone** | ossuary, brass, mirror, chapel, choir, rust, celestial | brick courses, chipped corners, carved bands | rib / cog / lens | chain, candelabra, orrery ring |
| **Wrong organic** | cinder, rime, teeth, flesh, storm, abyss, dream, heart | ribs, membrane, veins, vents, crystals | vein / ember / tooth | vat, frost bloom, beating wall |

A theme is a record of about 25 values: family, hue rotations for the 12 palette roles,
boulder roundness, moss/vein density, speckle rate, watermark level, glow strength, prop list,
ambient particle kind, fog colour. Changing hue alone gives a tint; the plan is that **every
theme also changes at least three structural parameters** (roundness, density, tile variant
count, prop set, edge style), so the silhouette differs, not just the colour.

Automated distinctness: for each pair of adjacent themes, require a minimum palette distance
(in a perceptual-ish weighted RGB metric) and at least two differing structural parameters;
otherwise `--check` fails. This is the machine-checkable version of "each floor feels
different".

### 6.8 Font, UI and card art

- The font is **generated from stroke data**, so it can be re-rasterised at 1x and 2x and has
  no licence. Titles use the same glyphs at 2x with a 1 px drop shadow and a glow pass.
- **9-slice panels** are generated per family so the draft screen, the floor intro and the
  pause menu look like they belong to the floor you are on.
- **Card faces** are composed at runtime from: the tier frame, a pictogram, the title in the
  pixel font, and the drawback line in the `hazard` colour. Compose, don't paste: the same
  three pieces serve 28 cards and any future ones.
- **Hearts** are 8x8 with full/empty states plus the shatter strip for the damage animation.
- **No blank fallback:** if a theme id or icon is missing, the loader throws in tests and draws
  a magenta checker in debug builds — the same "fail loudly" rule used for `GameConfig`.

### 6.9 Generated volume

| Output | Count | Where it comes from |
| --- | --- | --- |
| Tile atlases | 3 | one per family, all 47 blob + 16 edge masks |
| Theme palettes | 20 | parameter data, no pixels |
| Props/decals | ~14 per family | 12 prefab generators |
| Player frames | 14 | one rig, 4 directions |
| Killer frames | 18 | one rig + theme dressing |
| Items/objects | 12 | keys, wisp, barrier x2, arch x2, shrine, gate, heart, shatter, arrow, pips |
| Card pictograms | 20 | shared with HUD icons where possible |
| UI | ~25 | 9-slice pieces, frames, buttons, chevrons |
| Font | 1 atlas | 5x7 stroke data, ~100 glyphs |

Every number above is produced by code. The cost that used to be "one artist-week" becomes
"one compiler plus iteration", which is why section 11 budgets it as engineering sessions
rather than an external dependency.

### 6.10 Validation: how I check art I cannot see

I cannot look at a PNG in this environment, so the compiler is built to be checkable without
eyes, and reviewable with them:

**Automatic (`--check`, also a test fixture):**

- Determinism: same seed → identical bytes, for all themes.
- No empty sprite; every atlas slot used by the theme table is non-empty.
- Alpha is binary; every index is in palette range; no sprite exceeds its cell.
- Wall/floor luminance contrast above a floor (so the maze is legible in fog and in the dark
  floors), and the player's lantern pixel is the brightest in its own sprite.
- Open edges of an autotile match its mask (catches the classic "wall floats one pixel" bug).
- Character frames: consistent feet row, non-zero walk-frame difference, bounding box inside
  the cell.
- Theme distinctness (palette distance + structural parameter count).
- Font: every printable glyph non-empty except space, consistent baseline, no clipping.
- Atlas hash recorded in the manifest; a change without a new hash fails review.

**Human, ten seconds each:** `--sheet` writes `docs/art/<theme>.png`, a labelled contact sheet
of every tile, prop, character frame and icon on that theme's palette. You do not need to draw
anything; if you want, you glance at one sheet per family and say "more like this" or "less
moss". That feedback changes parameters, not files.

If a theme still looks bad after that, the fix is a grammar parameter, and it is a one-line
diff — which is the real advantage of generating the art.

### 6.11 Screens

- **Title:** keep the current composition, redrawn in pixels: wordmark in the generated font,
  the cave behind it, animated spores, menu in pixel type, one small figure walking into the
  dark on idle.
- **Floor intro:** full-width card with the floor name and a one-line epithet, 1.2 s.
- **Draft:** three card faces, arrow-key selection, Enter to take, hover/selection enlarges
  the card and shows the drawback in red. Card art is a 24x24 pictogram plus a border that
  encodes tier (common/rare/curse).
- **HUD:** hearts, keys, barrier/wisp pips, floor number. No text labels once the icons are
  learned.
- **Damage/death:** heart shatter at the top, red vignette, then the summary.

### 6.12 Juice list (cheap, high impact)

Hit-stop on damage (120 ms), screen shake (toggleable), heart-shatter particles, lantern
flicker on low hearts, footstep dust, wisp trail, card flip, floor-clear chord, subtle
chromatic edge on damage, and a 1-frame white flash on the killer's lunge. Each is a few
lines; together they are most of "feels like an indie game". Particles and flashes are also
generated (no particle textures — 2x2 and 3x3 pixel stamps from the palette).

### 6.13 Accessibility and comfort

- Screen-shake and flash toggles (photosensitivity), palette-blind-safe variants for the three
  key colours, text size option for the pixel font, audio volume sliders, and a "no jump-scare
  stingers" toggle. Menu, not afterthought.
- A `--reduce-motion` flag that disables shake, hit-stop and the strongest strobes, useful for
  the headless harness too.

---

## 7. Themes: twenty floors

Each floor is data: palette parameters, family, ambient layer, hazard, enemy tweak, music
parameters. The table below is the intended arc; names are placeholders and should be the
team's.

| # | Floor | Look | Mechanical twist | Sound |
| --- | --- | --- | --- | --- |
| 1 | The Shallows | green/cyan bioluminescent, dripping | none: teach movement, keys, arch | sparse drone, water drips |
| 2 | Rootbound | moss, roots, warm lantern light | corridors narrow: one-cell chokes | low strings, wood creaks |
| 3 | Drowned Halls | reflected water, rain light | shallow water slows the player 20% | wet percussion, distant bells |
| 4 | Sporefall | pink/green spores, soft fog | vision pulses +/-1 every 4 s | airy pad, breathing |
| 5 | The Ossuary | bone ribs, chalk dust | floors have bone gates that close behind you | bone clicks, choir hum |
| 6 | Cinderworks | ember orange, smoke | fire vents telegraph 0.6 s then burn | furnace pulse, metal |
| 7 | Rime Vault | blue-white, frost | ice slides you one extra cell | glassy synths, wind |
| 8 | The Brass Labyrinth | clockwork, brass, warm | gates open/close on a 4 s cycle | ticking, mechanical bass |
| 9 | Hive of Lanterns | amber, paper lanterns | wisps are free but last half as long | buzzing choir, hand drums |
| 10 | The Mirror Gallery | black glass, reflections | your own aura is dimmer (vision -1): trust memory | reversed piano, glass |
| 11 | Red Chapel | crimson, candles | the killer is silent (no footsteps) but its aura is bigger | organ, low choir |
| 12 | The Hollow Choir | grey stone, hanging robes | sound cues only: no aura outside 3 steps | dissonant voices |
| 13 | Flood of Teeth | black water, pale shapes | water hides hazards until adjacent | bubbling, teeth clicks |
| 14 | The Flesh Garden | wet red organic | placed seals heal over after 8 s | heartbeat, wet strings |
| 15 | Stormcells | lightning flashes | lightning reveals the whole floor for 0.3 s | thunder, driving pulse |
| 16 | Rust Cathedral | ruined iron, rain | gate+chasms, the killer can cross chasms | iron choir, distortion |
| 17 | The Abyss Looks Back | near-black, single light | vision 2; the killer is faster in the dark | sub bass, whispers |
| 18 | Celestial Machine | white/gold, orrery | the floor rotates its gates each 8 s | bells, arpeggios |
| 19 | The Long Dream | soft violet, drifting | your cards' drawbacks double for this floor | lullaby, reversed pads |
| 20 | The Heart | crimson/black, beating walls | everything at once; the killer is the floor | full theme, title motif |

Escalation rules for the theme data:

- **Hazards never repeat back-to-back**; each floor's twist is one sentence and one visual.
- **Every theme changes at least one rule**, not only a colour. Floors 1–4 teach, 5–12 combine,
  13–20 pressure.
- **The floor-20 theme quotes the title screen motif** (in music and in the arch design), so
  the run closes the loop it opened.

### 7.1 Production strategy: themes are parameter sets

- **Three families, twenty parameter records.** No per-theme atlases, no per-theme font, no
  per-theme audio files. A theme is a C# record plus a palette derivation, so adding floor 21
  is data, and retheming a floor after play-testing is a diff, not an art request.
- **Every theme ships with its signature tile, prop and hazard.** Those three are what make a
  screenshot identifiable, and all three are generated.
- **Hazards are cell sets in Core with tests**; the renderer only paints what it is told.
- **Order matters for tuning:** get floors 1–5 to look and sound right first, because they set
  the language the other fifteen vary. The last five get extra music layers, not extra assets.

---

## 8. Audio: a score that is synthesised, not sourced

### 8.1 Architecture

- **Core emits events**, the UI reacts. Audio must not poll `GameState` and guess. A small
  event stream (`KeyPicked`, `HeartLost`, `HeartLostLast`, `SealPlaced`, `SealBroken`,
  `WispLit`, `WispConsumed`, `FloorCleared`, `RunLost`, `CardTaken`) is enough for every
  sound in this design, and it is also what the VFX layer wants.
- **`IAudioDirector` in the UI**: owns the Raylib audio device, the synth, the sequencer and
  the sound bank. `GameState` and `Campaign` never reference audio.
- **No audio device = silent game.** `InitAudioDevice` can fail on headless machines and in
  CI; the director must degrade to no-ops without throwing or logging every frame. The test
  suite never touches audio; it tests the *generator* as pure math.
- **No files and no formats.** Nothing is decoded, streamed from disk or licensed. Raylib is
  used only as an output device: `AudioStream` fed 16-bit stereo PCM from our own mixer, and
  `Wave` buffers for one-shot effects. This removes the licence question, the download size,
  and the "40 OGG files" problem in one decision.

### 8.2 The score: twenty themes, one engine

**One track per floor**, generated as an arrangement rather than authored as audio. Each theme
is a `MusicTheme` record:

```
root, mode (aeolian / dorian / phrygian / locrian / lydian…), tempo, barsPerLoop,
progression[], instrumentMix{pad, bass, lead, perc, choir}, density, tension, reverbWet
```

On top of that, five **shared motifs** (including the title motif) are transformed per floor —
transpose, invert, retrograde, halve/double time, change mode. That is how twenty floors sound
like one descent: the same handful of melodic ideas keep returning in increasingly wrong forms.

- **Escalation by layers, not files:**
  - Floors 1–5: drone + pad + one motif statement, sparse percussion.
  - 6–10: add bass ostinato, second motif, kick/hat pattern.
  - 11–15: add choir voice, bell/FM voice, wider stereo, more reverb.
  - 16–20: add distorted lead, driving percussion, the title motif in canon.
  - Floor 20: half-time title theme, all layers, a final choir statement when the arch unlocks.
- **Intensity without crossfading files.** Because the arrangement is generated live, the mix
  has per-layer gains. `Calm` = pad only; `Tense` = pad + bass; `Chase` = all layers plus a
  percussion fill; `Hurt` = duck everything 3 dB under the damage stinger. Transitions are
  smoothed over 400 ms in the mixer, so there is no seam to click.
- **Determinism:** the score is a pure function of `(themeId, runSeed)`. The same seed produces
  the same music, which makes it testable and lets a speedrun feel identical.

### 8.3 Sound effects: 26 synthesised one-shots

| Event | Synthesis | Priority |
| --- | --- | --- |
| Player step | filtered noise burst, 3 variants, per surface family | must |
| Killer step | low sine thump + wet noise tail | must |
| Damage | noise crack + descending sine, heart-shimmer | must |
| Last heart | same, longer, with a choir-ish detuned pair | must |
| Heartbeat loop | two sine thumps, tempo follows intensity | must |
| Key picked | FM bell, rising three-note figure | must |
| Key pulse (Dowsing) | sonar ping (sine + long delay tap) | must |
| Seal placed | stone/thorn thunk (noise + lowpass sweep) | must |
| Seal cracks / breaks | crack, then snap | must |
| Wisp lit / consumed | airy ignition / reverse-envelope swallow | must |
| Arch locked / unlocked | dull thud / unlocking chord | must |
| Floor cleared | short chord resolving into the next track's key | must |
| Card hover / take | paper flick / stamp | must |
| Run lost | motif fragment, cut off | must |
| Lunge tell | sharp inhale (band-passed noise swell) | nice |
| Near-miss | passing whoosh (panned noise swept) | nice |
| Ambient loops | one per family (drips, wind, hum, choir air) | nice |
| UI back / pause | soft click | nice |

All are rendered at startup into small `Wave` buffers (mono, ≤ 0.5 s each, ~2 MB total), from
a table of `(oscillator, envelope, filter, noise, duration)` recipes. No two effects share a
recipe, so they stay distinguishable; the `--check` pass also asserts no two FX buffers are
identical and none clip.

### 8.4 The synthesiser, concretely

- **Mixer:** a `MusicEngine` on a dedicated thread renders 512-frame blocks into a lock-free
  ring buffer; a Raylib `AudioStream` drains it (callback or `UpdateAudioStream` from the
  frontend tick). No allocation, no locks, no garbage collection in the audio path.
- **Voices:** triangle lead, detuned saw pad (2 osc), sine sub, FM bell, formant-ish choir
  (saw + two band-passes), noise percussion (hat/snare/kick from filtered noise + sine
  sweep). One-pole or biquad filters implemented by hand; Raylib has no filter graph.
- **Reverb:** a cheap Schroeder network (4 combs + 2 allpass) per theme wet amount. It is the
  single biggest "this sounds like a place" win and costs ~15 lines.
- **Sequencer:** 16th-note grid, patterns generated from the theme's params and seed; motifs
  transformed per floor; drums by density. Loops are whole bars, and the reverb tail wraps
  around the loop point so there is no click.
- **Loudness:** each theme normalised to a target RMS, soft-clipped with `tanh`, then a 1.5 s
  fade at the loop seam only for the very first cycle. Assert peak < 1.0 and no NaN/denormal
  in tests.
- **Cost:** roughly 6–10 voices at 44.1 kHz, stereo, 1 core. Measured budget ≤ 4% of one core;
  if it exceeds that, drop the choir voice before dropping the frame rate.
- **Failure modes:** device lost → silence, keep playing; slow machine → smaller block size
  first, then fewer voices; neither may stall the render loop.

### 8.5 What "music reacts" means, audibly

- The moment the killer's aura touches you: percussion enters, the pad detunes by 8 cents, and
  the motif shifts to the tritone. That is the "it noticed" sound, and it is a *change in the
  arrangement*, not a new file.
- The hatch to `Chase` is beat-aligned (next bar), so it feels composed rather than triggered.
- Damage ducks the music and adds a 1.5 s ringing tone; the last heart duck is twice as long
  and the heartbeat becomes the only thing playing for two bars.
- Floor 20 removes the percussion entirely for the first ten seconds, then brings it back with
  the title motif in canon. That is the moment the whole plan is for.

---

## 9. Architecture and code impact

### 9.1 Core

- **`Campaign`** (new): owns `RunSeed`, `FloorIndex`, `Hearts`, `MaxHearts`, `IReadOnlyList<Card>`,
  `ElapsedSeconds`, and the current `GameState`. Methods in spirit: `StartFloor()`,
  `OnFloorCleared()`, `OnCaught()`, `ChooseCard(id)`, `IsRunOver`, `ToSave()/FromSave()`.
  All the heart, floor and draft rules live here, in Core, with tests.
- **`CardCatalog` / `Card` / `RunModifiers`** (new): data plus a clamped composition over a
  floor's `GameConfig`. Cards are ids and deltas, not code.
- **`ThemeCatalog` / `FloorDefinition`** (new): floor number, name, theme id, maze shape
  parameters, key count, hazard kind, enemy speed, music id. The theme record also carries the
  palette seed and family id, which is all the compiler needs.
- **`GameFactory` becomes `RunFactory`**: builds a floor from `{seed, floorIndex,
  runModifiers}` and builds the first `Campaign`. This is the seam that keeps the floor rules
  unchanged.
- **Events**: a small `GameEvent` value plus a queue on `GameState` (drained by the UI each
  frame). Replaces the "audio guesses from state" anti-pattern.
- **`GameStatus`**: `Caught` becomes `Downed` (one beat) and `Lost`; add `FloorCleared`.
- **Hazards**: per-floor cell sets in `GameState` (water, vents, gates) participating in the
  existing `CostOfEntering` and movement rules, each with tests.

### 9.2 UI

- **`IRenderer.Draw(GameState)` becomes a `RenderView`**: state plus theme id, floor number,
  hearts, animation clock and the drained events. That is the one interface change that
  ripples; it is also what lets the renderer stop reaching for globals.
- **`PixelRenderer`** (new) replaces `RaylibRenderer` behind the same interface once it is
  good; keep the old one until then, switch in `Program`. `ConsoleRenderer` stays as the debug
  view and gains hearts/floor/theme in its header, which keeps the headless smoke test useful.
- **Screen state machine** grows: title, floor intro, play, draft, floor clear, run lost,
  summary. Same pattern as today's two-screen loop; the frontend still owns transitions.
- **`AudioDirector`** (new) as above, wrapping `MusicEngine` and the generated FX bank.
- **Content loading**: `Content/Generated/` PNGs plus `manifest.json` (atlas ids, cell sizes,
  palette count, hashes, font metrics). A test walks the theme catalog and asserts every id the
  game can ask for exists in the manifest; a missing theme is a failing test, never a magenta
  rectangle in a demo.
- **`tools/AssetForge`** (new project, not referenced by the game): the compiler described in
  6.6. Solution gains one project; `MazeGame.UI.csproj` gains `Content/Generated/**` as
  `CopyToOutputDirectory` (or embedded resources — decide in M3).

### 9.3 Tests

- New families: hearts/damage/respawn, campaign progression and save round-trip, card
  composition (every card, both halves), theme catalog completeness (20 floors, every field),
  hazard rules, **generator determinism and atlas validation**, **synth math (no clipping, no
  NaN, loop seam, intensity layer gating, distinct themes)**.
- Existing: 8 files reference `GameStatus.Caught` (18 assertions). Those become "lose a heart
  and keep playing", plus new assertions for the final loss. Keep the random-play invariant
  test and extend it to a full campaign run with random card picks: no sequence may make a
  floor unwinnable, overspend a budget, or leave the killer without a route.
- Visual/audio: keep the current headless harness trick (drive the real renderer, count
  frames, time draws). Add one harness pass per theme that renders a floor and asserts no
  exception, since a missing tile or palette is exactly the kind of bug tests miss.
- **The compiler is tested like any other algorithm**: fixed seed → known hash, mask → open
  edges, palette → contrast floors, font → baseline. This is what replaces "an artist looked
  at it".

### 9.4 What must not change

- `DisjointSet` and `MazeGenerator` (the graded perfect-maze algorithm), `MinHeap<T>` and
  `DijkstraPathfinder` (the graded pathfinder). Everything here composes on top of them.

---

## 10. Starting balance numbers

A first guess to tune from, not a promise:

| Knob | Start | Notes |
| --- | --- | --- |
| Hearts | 3 | max 5 via cards |
| Invulnerability after damage | 1.5 s | 3 s with Hollow Bones |
| Killer respawn distance | 8 steps | nearest free dead end at least that far |
| Player step | 0.11 s | floors 1–8, 0.10 s later |
| Sprint | +40% for 1.2 s, 3 s recharge | drains entirely in ~3 s |
| Keys per floor | 2 / 3 / 3 / 4 / 4 / 5 | by band |
| Floor size | 13 → 19 | keeps the whole map on screen |
| Killer step | 0.62 → 0.38 s | the difficulty dial |
| Card offers | 3 | 4 for Lucky Find, 2 for its drawback |
| Rarity | C/U/R by floor | guaranteed rare on 5/10/15/20 |
| Run length | 40–60 min | 2–3 min per floor |
| Music voices | 6–10 | ≤ 4% of one core |
| Atlas budget | ≤ 4 MB | PNGs, three families, compressed |

---

## 11. Build plan and milestones

This is all engineering now — there is no parallel art or music track, and nothing to wait
for. The work is split into **nine phases**, each ending in a playable build so progress is
always visible and never a long stretch of unplayable scaffolding.

### 11.1 Phase gates

The rule for every phase: `dotnet build` at zero warnings, the full test suite green, the
headless renderer harness drawing every affected screen, and the game launchable (window or
`--console`) before the next phase starts. "Playable" means the rule from the previous phase
still works end to end, not merely that it compiles.

| Phase | Scope | Playable at the end because | Status |
| --- | --- | --- | --- |
| **1** | **Hearts and damage** — 3 hearts, a catch costs one, killer respawn + stun + i-frames, heart HUD and damage flash | A run can still be won or lost; losing a heart is now survivable | **done 2026-10-20** |
| **2** | **Floors and campaign** — `Campaign`, 3 named floors, arch clears the floor, hearts carry, floor intro and run summary | A complete 3-floor run, won by clearing the last arch | **done 2026-10-20** |
| **3** | **Card draft** — 9 cards, clamped `RunModifiers`, draft screen, skip-to-heal | Runs differ; every card has a tested drawback | **done 2026-10-20** (13 cards after Phase 5's slice) |
| **4** | **Movement feel** — timed steps, one buffered input, render interpolation, sprint | Same rules, smooth motion; speed becomes a stat | **done 2026-10-20** |
| 5 | Sprite compiler — `AssetForge`, palettes, three families, rig, font, `--check` | No visual change yet; the atlases and validation exist | **done 2026-10-20 (exit met)**: canvas, palette, PNG writer, autotiler (16 edge + 47 blob), rig (cast + props), 3 themes, `--check`. Font, pictograms, UI chrome and the two other families follow the renderer's screens |
| 6 | Pixel renderer — virtual canvas, integer scale, atlas blitting, lightmap, pixel HUD | The game looks like the planned indie game, same rules | **in progress 2026-10-20 (slice 1)**: virtual canvas, integer scale, atlas blitting, thin wall faces, lightmap, fog, cast, reticle. The font, pictograms, pixel HUD and the four menu screens remain |
| 7 | Audio — synth engine, sequencer, motifs, intensity layers, FX bank, silent fallback | The chase is audible and the game is silent-safe | **done 2026-10-20** |
| 8 | Themes and content — 20 floors, 20 palettes, remaining cards, hazards, save/continue | The full roguelike run exists | **partial 2026-10-20**: 20 floors and 20 themes are shipped; remaining cards, hazards and save/continue |
| 9 | Polish — juice, accessibility, balance, docs, screenshots | Definition of done in section 15 | |

Phase 1 is shipped: `GameConfig` gained `StartingHearts` (3), `MaxHearts` (3),
`InvulnerabilitySeconds` (1.5), `RespawnDistance` (8) and `RespawnStunSeconds` (1.5);
`GameState` spends a heart on a catch, throws the killer to the furthest reachable
non-objective cell, stuns it, and refuses a second catch until the window closes. Only the
last heart ends the run. The window draws the hearts and a red damage flash; the console
header prints them. 22 new tests (301 total), 1,264 renderer frames through three catches,
4,496 console frames, window alive with an empty log.

Phase 2 is shipped too: `FloorDefinition` + `FloorCatalog` (The Shallows, Rootbound, Drowned
Halls — 3/3/4 keys, killer 0.60/0.54/0.48 s) and `Campaign`, which owns the run's floors, the
current `GameState`, the hearts carried between them and the run outcome. `GameState.Won` now
means "this floor's exit", so the run is won only after the last floor; a floor's
`Caught` ends the run at once. `GameFactory.CreateCampaign` builds a run, and each floor's
random source is derived from the run seed plus the floor number, so a seeded campaign is
reproducible with different layouts per floor. The window gained a floor intro card, a
floor-cleared card and a run summary, and the console prints a floor heading and carries hearts
forward. The frontends now take `Func<Campaign>` instead of `Func<GameState>`.

Phase 3 is shipped as well. Nine cards went live then, the four movement cards joined them once
player steps were timed, and a second wave of ten rule-backed cards followed in the 2026-10-21
overhaul — twenty-three today (`wide-eyes`, `blink-step`, `long-veil`, `blood-pact`, `second-wind`,
`lantern-oil`, `quick-hands`, `trophy-hunter`, `lucky-find`, `light-feet`, `momentum`,
`sprint-lungs`, `hollow-bones`, `featherfall`, `magnetic-keys`, `bloom`, `blink-ledger`,
`ghost-veil`, `long-drag`, `wide-light`, `glass-cannon`, `debt-of-sight`, `slow-pursuit`), each one a
`Card` record carrying a `CardEffect` of numbers, composed by `RunModifiers` over the floor's
`GameConfig` and clamped before the floor is built — no card is an `if` inside `GameState`. The
draft is a gate in `Campaign`: clearing a floor that is not the last opens three seeded offers
and `TryAdvanceFloor` refuses until one is taken or the draft is skipped for a heart. The window
has a card grid with the run's totals and the console prints the offers. Two floor rules were
added for two of the cards (`LureStunSeconds`, `LureVisionPenalty`), and the wisp's light now
goes out when it expires. 84 new tests (415 total), 224 renderer frames through two drafts and a
four-offer layout, 1.75–2.03 ms/frame (three runs).

The catalog's nine are the cards expressible with today's rules. The movement cards (Light Feet,
Momentum, Sprint Lungs, Hollow Bones) need timed player steps from Phase 4, the curse pair (The
Debt plus Quiet) needs the rare-with-a-price machinery, and the rest need hazards or blueprint
changes from Phase 8. Two texts were adapted rather than implemented as written: Second Wind's
vision cost is run-wide (a draft happens *between* floors, so "the rest of this floor" had no
referent) and Trophy Hunter's heal is capped by the floor's ceiling. Tiers currently weight the
draw instead of gating by floor band, because three floors would collapse every band onto floor 1.

Phase 4 is shipped. `GameConfig.PlayerStepSeconds` gates the *next* step rather than delaying the
cell change, so every rule still sees one cell at a time; the shipped floors use 0.11 / 0.10 / 0.09 s
against the killer's 0.60 / 0.54 / 0.48, and the base config keeps 0 as the instant-movement seam
the rule tests were written against. One direction is buffered while a step cools down and spent
when it ends. `GameState` publishes `PreviousPlayer`/`PlayerStepFraction` and the killer's pair, and
the window slides both figures (and the lantern pool) between cells. Sprint is a stamina resource
on Shift: 1.4× speed, a 1.2 s bar that only drains while a step is running, a 25% refill gate,
refused while aiming or on an instant floor, with a HUD bar that appears once stamina has been
spent. Walking reads held keys while the aim cursor stays tap-based. 34 new tests (449 total), and
the harness reports
`MOVEMENT_OK step=0.11s halfway=0.50 sprinting=True stamina=0.91 idleStamina=1.00` across 236
frames (draining while moving, free while standing).

The movement cards in §4.3 (Light Feet, Momentum, Sprint Lungs, Hollow Bones) can now be built on a
real player step time, though Sprint Lungs overlaps with the free sprint and needs a redesign. The
diegetic sprint cost from §5.2 (footstep ring, the killer listening) is not implemented yet.

Phase 7 is shipped, and it closes §8 almost as written. `GameState` publishes a twelve-event
stream (`Step`, `KeyPicked`, `HeartLost`, `HeartLostLast`, `KillerStep`, `SealPlaced`,
`SealCracked`, `SealBroken`, `WispLit`, `WispConsumed`, `FloorCleared`, `RunLost`) that the UI
drains; Core has no audio types. The score is one `MusicTheme` record per floor (root, mode,
tempo, bars, progression, instrument mix, density, tension, reverb, band), arranged from five
shared motifs transformed by band and rendered as three stems — because the stems are separate,
the intensity mix is gains rather than a second track: `Calm` bed only, `Tense` adds bass, `Chase`
opens motif and drums, `Hurt` ducks. The change is adopted on the next bar and faded in over 400 ms,
which is the plan's "composed rather than triggered" made literal; floor 20 holds the arrangement
back for ten seconds and states its motif in canon, two of the three special beats §8.5 asks for. Hats, kicks, snares and the reverb are synthesised from the
primitives in `Synth`, so the game still ships no audio files at all. Two deviations from §8: the
device is fed by Raylib's stream callback instead of a ring-buffer thread (raylib refuses a write
when its buffer is full, so polling stutters or lags; the callback measured 100% of real time at
441 frames per call), and the FX distinctness/clipping checks are xunit tests rather than part of
`AssetForge --check`, because the recipes live in the UI assembly and the tool must not reference
the game. The score costs **0.006 ms per 2048-frame block** (~0.3% of a core, budget 4%), one
45–150 ms render per floor during the intro card, and 22 one-shots in 2.6 MB that are built
in 27 ms before the stream starts. What is left is taste: balance, motif memorability, whether
twenty 10–16 s loops are enough before they repeat.

### 11.2 Milestone detail

Roughly **14–18 focused sessions** remain at the current pace, with the content compiler and
the synthesiser being the two places that can eat a session unexpectedly.

- **M0 — run spine (done in Phases 1–2).** Hearts, catch/respawn and the campaign with three
  floors, carried hearts, floor intro and run summary are in place. The save file and a 20-floor
  catalog arrive with Phase 8, because both depend on the theme data.
- **M1 — Draft (done in Phase 3).** Nine cards, a seeded draft after every cleared floor,
  clamped modifier composition, skip-to-heal, a window card grid and a console prompt. Exit met:
  the harness log shows cards reaching the next floor (`maxHearts 4`, `step 0.422`, `barriers 4`)
  and every card has both halves under test.
- **M2 — Movement (done in Phase 4, corrected 2026-10-20).** Timed steps with one buffered input,
  sprint on stamina, killer speed untouched because the 5.5:1 ratio already preserved the escape
  feel. The explorer originally slid between cells like the killer; that was reverted to a snap
  because it made the grid uncountable while running. Exit met: the harness measures the cadence
  (`halfway=0.50`) and no rule regressed (449 tests, all earlier ones untouched).
- **M3 — Sprite compiler (done 2026-10-20).** `PixelCanvas`, `Palette` with the fixed lightness
  ladder, a dependency-free PNG encoder, the organic-cave grammar, the autotiler (16 edge + 47
  blob tiles, generated from the same masks the renderer will use), the rig (explorer and killer
  poses plus key, wisp, barrier and arch), three theme parameter sets and `--check` (theme
  validity, neighbour colour distance and structural difference, determinism, drift against the
  committed atlases). Exit met: floor 1's tiles and cast exist as committed PNGs (315 tiles across
  three themes) and pass validation. **Deferred on purpose:** the font atlas, the pictograms, the
  9-slice UI chrome and the worked-stone / wrong-organic families — they only have consumers once
  the renderer draws screens and the later floors exist.
- **M2b — Movement cards (done alongside M3's first slice).** With a real step time, the four
  movement cards ship: Light Feet, Momentum (a straight-step ramp with a turn penalty — a rule,
  not a number), Sprint Lungs and Hollow Bones. Exit met: each has both halves under test, and
  the four knobs are zero by default, so a run without them has the old cadence exactly.
- **M4 — Pixel renderer (2 sessions; slice 1 done 2026-10-20).** Slice 1: `PixelLayout` sizes a
  16 px-per-cell canvas per floor, `PixelView` renders into it and blits at an **integer** scale with
  letterboxing, `AtlasManifest`/`AssetPaths`/`TileAtlas` load the committed PNGs (missing art throws),
  `TileMask` picks the wall faces from the same bit order the compiler uses (cross-checked by a
  test), and one `cols x rows` lightmap carries the lantern falloff, the remembered cells and the
  killer's tint. Exit met for the floor: **1.03 ms median per draw** over 200 frames, 62% of the
  canvas painted, no fog leaks, the cast animates from the step clocks, and the aim reticle is
  chrome so fog cannot dim it. Slice 2 is the font, the pictograms, the pixel HUD and the four
  menu screens; the vector renderer still draws those, which is why the phase is not closed.
- **M5 — Synthesiser (3 sessions).** Mixer + ring buffer, voices, filters, reverb, sequencer,
  motifs, intensity layers, FX bank, silent fallback. Exit: the chase is audible and the game
  is silent-safe; buffer math tested headlessly.
- **M6 — Themes (2 sessions).** 20 parameter records, 20 palettes, hazard rules, floor intro,
  three families wired, distinctness checks. Exit: any floor renders and sounds like itself.
- **M7 — Content fill and balance (2–3 sessions).** Remaining 19 cards, per-band tuning,
  campaign fuzz test, accessibility menu, save/continue polish.
- **M8 — Polish (1–2 sessions).** Juice, screenshots, docs, play-test passes, `--check`
  cleanup.

**The honest sequence:** M0–M2 and a vertical slice of M3/M4 with one theme, then decide
whether the game is fun before generating nineteen more. The generator's whole advantage is
that the twentieth theme costs a parameter block — but only after the first one is good.

**Sequencing rules that matter:**

- The compiler lands **before** the pixel renderer, so the renderer never has to invent
  placeholder art that then gets thrown away.
- The synth lands **after** movement and hearts, because its intensity mapping is defined by
  the chase, and before the themes, because every theme's audio params need the engine.
- Every milestone ends with `dotnet build` at zero warnings, the full test suite green, and the
  headless harness rendering the affected screens.

---

## 12. Scope ladder (if time runs out)

Cut from the bottom, in this order:

1. **20 floors → 8 floors**, keeping the escalating arc; the run becomes 15–20 minutes.
2. **Per-theme structural parameters → palettes only**, with one signature prop for each of
   the last five (the weakest visual cut; the maze still reads well).
3. **Three families → two** (organic cave + wrong organic), moving the stone floors onto the
   worked-stone props over the cave geometry.
4. **Choir/bell voices and reverb → four voices, dry** (music still escalates by layering).
5. **Per-theme ambient loops → one shared ambience bed**, pitched and filtered per theme.
6. **Sprint** and any stamina system (pure timed movement is enough).
7. **Curse cards** (keep the drawback cards, drop forced curses).
8. **Save/continue** (painful, but a run is short enough to finish in one sitting).

Never cut: hearts, the draft, the load-bearing pixel look on floors 1–5, damage sounds, the
intensity-mapped music, or the fact that the repo generates everything. Those are the actual
request.

---

## 13. Risks

| Risk | Why it bites | Mitigation |
| --- | --- | --- |
| Procedural art looks generic | Generated tile art can converge on "noise with a palette" | Shape grammar per family, three structural params per theme, signature tile/prop, contact-sheet review; iterate parameters, not files |
| Twenty themes drift into tints | Palette swaps alone are not themes | `--check` enforces palette distance + structural difference; each theme has a twist that changes a rule |
| Generated music sounds like a toy | Simple oscillators can read as chiptune pastiche | Reverb, arrangement density, motif transformation, per-theme instrument mixes; keep the palette of voices deliberately small and consistent |
| Synthesiser stutters or crackles | Real-time audio is unforgiving on a loaded machine | Dedicated thread + lock-free ring buffer, no allocation, block-size fallback, measured CPU budget |
| I cannot see or hear the output here | No frame buffer, no audio device in this environment | Generation is pure math with unit tests (hashes, contrast, masks, peak/NaN/seam); contact-sheet PNGs and WAV dumps for human review; the game stays silent-safe |
| Hearts make it too easy | Three lives plus i-frames can flatten tension | Damage costs tempo and the killer comes back angrier; tune respawn distance, not hearts |
| Cards become noise | 28 cards with no synergy is a spreadsheet | Cap the pool at 9 for the first pass; test each card's drawback; show totals |
| Timed movement ruins the feel | Instant steps are generous; cadence can feel laggy | Buffer one step, keep cadence ~0.1 s, test with a bot and a human |
| Compiler becomes a project of its own | Pixel-perfect tooling can absorb unlimited time | Fixed sprite sizes, fixed 16-colour palettes, three families only, `--check` as the brake |
| Save format churn | Every generation change invalidates saves | Versioned save, refuse on mismatch, regenerate from seed |
| Difficulty curve across 20 floors | Most runs die on 6 or never reach 20 | Band-based tuning, beatable with base kit, cards as power fantasy later |

---

## 14. Open decisions (recommended defaults in bold)

0. **Content provenance:** every sprite, glyph and sample is generated by code in this repo;
   no downloads, no packs, no licences, no artist dependency. **This is locked**, because it is
   the request. It is also why the asset pipeline is a tool project rather than a folder.
1. **Run length:** 20 floors in one sitting, ~45 min. Alternatives: 10 floors per run with two
   "acts", or 20 with checkpoints. **Recommend 20 floors, 2–3 min each.**
2. **Hearts:** 3, non-regenerating except by cards and skip-heal. **Recommend yes**, it makes
   the draft a healing decision too.
3. **Damage penalty:** tempo only (respawn the killer far) vs. also losing a key. **Recommend
   tempo only**; the roguelike is about the run, not punishment.
4. **Draft:** 3 offers, pick 1, skip = heal. **Recommend yes**, no rerolls in v1.
5. **Cards with a permanent curse:** allowed only as the price of a rare. **Recommend yes.**
6. **Movement:** timed steps at ~0.1 s with one buffered input. **Recommend yes**, with sprint
   as a stamina resource rather than a card.
7. **Art pipeline:** one generator + three families + 20 parameter sets. Alternatives: draw 20
   atlases (not possible here), or draw everything at runtime (blurry, slow). **Recommend the
   compiler**; it is the only option consistent with decision 0.
8. **Camera:** whole floor visible, floors capped at 19x19. **Recommend yes** (a minimap and a
   camera is a different game).
9. **Music:** a runtime synth with 20 theme records and shared motifs, intensity by layer
   gains. Alternative: pre-render 20 tracks to OGG (still generated, but adds a build step and
   ~60 MB). **Recommend the runtime synth**: sample-accurate transitions, no files, and the
   motifs can actually transform per floor.
10. **Save:** versioned JSON, seed + floor + hearts + cards. **Recommend yes**, it is cheap and
    a 45-minute run without it is hostile.
11. **Meta-progression:** none in v1 (no unlocks, no currency). **Recommend none**; the run is
    the content.
12. **Platform:** desktop window only, 16:9 letterboxed integer scale. **Recommend yes.**
13. **Generated output in git:** commit the PNGs and manifest, or generate at build? **Recommend
    commit** (clone → run, no tool step), with a test that regeneration is byte-identical.

---

## 15. Definition of done: "does it feel like an indie game?"

A checklist someone can actually judge in a ten-minute play-test:

- [ ] The title screen has pixel type, a moving background and no default Raylib font.
- [ ] Floor 1 can be completed in under three minutes by someone who has not played before.
- [ ] Taking damage is loud, visible and survivable; the third heart loss is a bigger moment
      than the first.
- [ ] Every card's drawback is legible on its face and felt within one floor.
- [ ] Movement is smooth, with no stutter at cell boundaries, and speed cards are felt.
- [ ] The floor's theme is identifiable from a screenshot with the HUD cropped off.
- [ ] Walls read as continuous carved rock, not as per-cell boxes.
- [ ] The music changes audibly when the killer closes, without a hard cut.
- [ ] Damage, key pickup, seal break and arch unlock each have a distinct sound.
- [ ] The run summary makes the player want to press R immediately.
- [ ] `dotnet run --project tools/AssetForge -- --check` passes, and the game contains no
      external asset files (no PNGs from outside the repo, no audio files at all).
- [ ] `git status` after a fresh clone and a full build shows no generated files missing and no
      untracked binaries.

---

## 16. Appendix: the generation stack at a glance

```
tools/AssetForge (console, offline)
  Palette ──► TileGrammar (3 families) ──► 47/16 autotiles
        └───► Rig (player, killer) ──► frames
        └───► Pictograms (cards, HUD)
        └───► Glyphs (5x7 stroke font) ──► atlas
        └───► UiForge (panels, frames)
                 │
                 ▼
  Content/Generated/*.png + manifest.json   (committed)
                 │
                 ▼
MazeGame.UI: PixelRenderer ── blits atlas, lightmap, fog
             MusicEngine  ── synth + sequencer ──► AudioStream ──► speakers
             FxBank       ── generated Wave buffers ──► one-shots
```

Two rules keep it honest:

1. **Nothing in the game reads a file that was not produced by this repo's code** (except the
   save file, which the game writes itself).
2. **Every generator is a pure function of its seed and parameters**, so everything is
   reproducible, testable and diffable. Art and music become code review.

---

## 17. What I need from you

Almost nothing — that is the point of this revision.

- **Decisions, not assets.** The calls worth making first:
  1. **20 floors** (~45 min runs) or 10-floor acts. Recommend 20 with a save.
  2. **Runtime synth** vs. pre-rendered generated tracks. Recommend the runtime synth.
  3. **Commit the generated atlases** vs. generate at build. Recommend commit.
- **Taste notes, optional.** After M4/M5 I can produce contact sheets and a short WAV of each
  theme. If you glance at one and say "more moss", "less reverb", "floors 6–10 too busy", that
  is a parameter change and a one-session iteration. If you would rather not look, the
  automated checks still catch the broken cases — the game will not be missing art, it just may
  not be to your taste on the first pass.
- **Nothing else.** No sprites, no fonts, no music, no licences, no downloads.

---

## 18. Second playtest: a new powerup kit and descent variance

**Status:** proposals from the 2026-10-21 playthrough, revised after the first set was rejected,
**shipped in full** on 2026-10-21 (see `PROGRESS.md` Phase 11). The five verbs, all eight descent
conditions, the floor-8/floor-14 gates, the floor gimmicks that replaced the retired verbs, and
the card pass that rewrote the four barrier/wisp cards around powerup charges are in the build;
their costs and prices are as written below. The two earlier asks (the floor reached on the death
card, and a maze that grows with the descent) were already shipped.

The first set of ideas tried to make barriers, wisps and sprint easier to use. That was the wrong
move: all three ask the player to place something and hope, so making them cheaper would not make
them interesting. This revision replaces the powerups instead of tuning them, and adds a run-level
system that makes the later floors play differently from run to run.

### What the player reported

- The barriers, wisps and sprint were never used in a full run.
- The cards did not feel fun.
- Later floors should feel different, not just faster.

### The new kit: five verbs, one key each, no aiming

Each powerup is one key, has an effect that is visible on the next screen, and costs a rule rather
than a menu. Charges refill when a floor starts; there is no shared budget to hoard.

| Key | Powerup | What it does | The price | Charges |
| --- | --- | --- | --- | --- |
| **SPACE** | **Blink** | Step through the wall you face and land one cell beyond it. | The wall stays open: you have made a shortcut the hunters can use too. | 2 |
| **V** | **Veil** | Stand still for half a second to go unseen for 3 s; hunters keep walking to where they last saw you. | Your lantern dims one step and any step breaks it. | 3 |
| **T** | **Drag** | The hunt runs at 40% for 3 s while you move normally. | When it ends, hunters are 10% faster for 5 s. | 1 |
| **L** | **Lantern** | Throw your light up to four cells; for 5 s you see from where it lands and the hunters walk toward it. | Your own cell is dark until you walk onto the lantern or the 5 s end. | 1 |
| **F** | **Shove** | Shove the hunter in the cell you face two cells back and stun it for 2 s. | It is loud: every other hunter targets your cell for 3 s. | 1 |

- **Blink is the traversal verb.** It turns a dead end into a door and leaves the door open. That
  is a spatial decision with a permanent consequence, not a combat trick.
- **Veil is the patience verb.** It rewards standing still and reading the hunt, and it is the
  opposite of sprint; the player has to choose which one the moment wants.
- **Drag is the panic verb.** It is the one button a new player will press when the aura is on
  them, and its hangover stops it from being a free reset.
- **Lantern is the bait verb.** It separates "where I am" from "where my light is", which is the
  most interesting idea in a game about darkness; it is also the most expensive to build.
- **Shove is the contact verb.** It is the only answer to a hunter already in the next cell, and
  its noise makes it a last resort rather than a rotation.

**Charges are per floor and visible.** The HUD replaces the `SEALS`/`WISPS` chips with five small
slots, each showing its key and a pip count. A floor's charges come from the floor definition, so
the first floors can give one or two and the last floors can give the full kit. Nothing is
unlocked by a key, a card or a currency: a powerup is available because the floor is.

**Teaching is the floor's job.** Each verb arrives on a floor that needs it, announced in the
intro card's existing gimmick line and with a one-time prompt the first time it is useful:

- Floor 1: Blink, beside a dead end one wall from the path.
- Floor 2: Veil, with a hunter crossing a corridor ahead.
- Floor 4: Drag, on a long straight with a hunter closing.
- Floor 6: Lantern, on a floor where the light has to go first.
- Floor 8: Shove, in a corridor where the only way out is through.

### Descent variance: two conditions per run, chosen at the gates

Later floors should not be "the same floor with more hunters". A run now draws **descent
conditions**: rules that change the space or the hunt for the rest of the run. They are the
variance, and they arrive at gates the player can see.

- At **floor 8** and **floor 14**, the run offers **two conditions** from the pool (seeded, no
  repeats) and the player picks one. The pick is permanent for the run.
- Floors 1-7 carry none; 8-13 carry one; 14-20 carry two, so the bottom of the run is the
  player's two choices combined with each floor's own gimmick.
- A pick uses the draft screen's shape (two cards, one choice) but no skip: the run has to change.
- The chosen condition is shown as an icon and a line on the HUD and on every later intro card, so
  the player always knows which rules are in force.

| Condition | Rule | How it changes the play |
| --- | --- | --- |
| **Regrowth** | Every 18 s the mouth of one dead end seals. Never the player's cell, never a cell holding a key, exit or hunter. | The maze slowly stops forgiving backtracking; you commit earlier and stop treating dead ends as free hiding places. |
| **Fracture** | Every 22 s one wall opens and a different one closes, both telegraphed. A close that would disconnect the maze is skipped. | Routes are provisional. The map you learned is not the map you leave. |
| **Eclipse** | Vision -1 for 6 s every 16 s. | The floor breathes; you learn to move in the dark half and to time the lit half. |
| **The Listener** | Hunters step only when you step; sprinting gives them two steps. | Standing still becomes a move. The chase is turn-based instead of a clock. |
| **Pack** | Hunters share one target and step on the same clock. | They commit together, so they can be baited around a loop, but they cannot be split or staggered. |
| **Hunted** | One hunter always knows your cell; the others hunt as normal. | There is no hiding from that one, only outrunning it, and the other hunters use the panic. |
| **Mute** | On each floor after the gate, one powerup (rotating) is unavailable, and that floor starts with one extra heart. | You adapt the kit instead of leaning on a favourite button, and the heal makes the loss a trade rather than a punishment. |
| **Drift** | The exit moves to a far cell after each key, announced by a bell. | The last leg is never the one you planned. |

- **Conditions compose with the floor, not replace it.** The Vault + Fracture is a different floor
  from The Vault + Listener, and neither is the authored Vault. With eight conditions and two
  choices, every run's late game is a different pair.
- **The gates are the drama.** Floor 8 is the first time the run asks the player to choose a rule;
  floor 14 is the second, and by then the player knows what the floor's gimmick costs. The choice
  should be legible in one sentence per card.
- **A cheaper alternative** is to draw both conditions automatically and show them on the intro
  card. That is less agency and less UI, but it keeps the variance.

### What happens to the old kit and its floor gimmicks

- **Sprint stays.** It is movement, not a powerup, and it is the one existing verb the new kit does
  not replace.
- **Barriers and wisps are retired from the player's hands.** Their rules can stay in `GameState`
  for a floor that places a pre-built seal, but the inputs, the aim mode and the two HUD chips go.
  The code should be left behind a flag for one pass so the two kits can be compared, then deleted.
- **Three floors got new gimmicks (done).** The Gilded Cage is "no blink down here"
  (0 charges), The Foundry is "shoves are cheap" (+2), and The Moth Garden is "lanterns are
  cheap" (+2). Each is a `Powerups` override on the floor definition, so a floor can still grant
  any kit it wants.
- **The card catalog needed a matching pass (done).** Deep Roots, Thornwright, Bait and Smoke Wisp
  were built on the retired verbs, so they were rewritten as Blink Step (+1 blink, wider aura),
  Long Veil (+2 veils, slower steps), Lantern Oil (+1 lantern, −1 vision) and Quick Hands (+2
  shoves, faster killer). `CardEffect` and `RunModifiers` gained the five charge deltas, clamped at
  zero, and the draft totals line now reports them. No card trades on a verb the player lacks.

### Cost and the first slice

- **The kit** is medium: five small additions to `GameState` (a breach, a veil state, a hunter
  time scale, a light origin, a shove) plus renderer and sound states. Blink, Veil, Drag and Shove
  are local; Lantern is the one that touches visibility and should be built last.
- **The conditions** are medium-to-large: Regrowth and Fracture need a maze-mutation API with a
  connectivity check and tests; Eclipse, Listener, Hunted and Mute are rule flags; Drift is a
  placement rule. Build two of them first, not eight.
- **The first slice is Blink + Drag + Regrowth on floors 1-10**, with the old kit unmapped and the
  conditions drawn automatically. The Gilded Cage's gimmick becomes "no Blink" for that slice.
  Two buttons and one mutation is enough to answer whether the new direction is fun; do not build
  the other three verbs until that slice has been played.

### Recommendation

Start with the slice above, not with the full five-verb kit. If Blink + Drag + Regrowth does not
make the powerups something the player reaches for, the other three verbs will not save it, and
the condition pool can be cut back to Regrowth and Eclipse. If it does, the next pass adds Veil,
Lantern and Shove and the floor-8/floor-14 choice screen, and the card catalog is rewritten around
the verbs that survived.

The player chose the full kit, so the full kit is what shipped. The slice's test — does the player
reach for a verb without being told — is now a playtest question rather than a build decision.
