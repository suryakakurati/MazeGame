# PROGRESS

Single source of truth for the state of the project. Update this file at the end of
every work session (see the handoff protocol in `HANDOFF.md`).

Last updated: 2026-10-20 (Title screen, replay after death, and a tested game factory)

## Current Status

The solution builds with zero warnings, `dotnet test` is green (279 tests), and the game runs
**in a Raylib window**: it opens on a title screen, then plays a seeded 15x15 maze drawn as a
bioluminescent cave, with fog of war, braided dead ends, three keys, an exit, sealed barriers,
wisps, and a killer that hunts the player one step every 0.6 s using Dijkstra. When a run ends,
the end card offers another descent, which builds a fresh cave and game without reopening the
window. The terminal view offered by `--console` (and used automatically when no window can be
opened) has the same title and replay, in ASCII.

What changed in this session:

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

Verified for this pass:

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

None in flight. Cornering, the seal, the wisp, the title screen and the replay are implemented,
tested and verified; the next session starts with a human play-test of the title screen and the
chase.

## Next Steps

1. **Play the window build and judge the title screen.** It is the first thing anyone sees, and
   it is drawn from primitives with no asset: the wordmark, the breathing light and the menu
   need one human look to say whether it reads as intentional indie design or as debug text.
   `RaylibRenderer.Title.cs` is the whole thing, and the layout numbers are at the top of
   `DrawTitle`.
2. **Play the window build and judge the chase.** The numbers say the cornering is gone (19 dead
   ends instead of 57) and that 81% of seals buy a detour, but only a person can say whether the
   seal *feels* like a wall, whether the wisp is worth a key binding and whether the tear-down
   animation (currently: the barrier simply vanishes) reads as the killer breaking it.
3. **Tune the seal.** `BarrierSealSeconds` (10) is deliberately longer than the walk from the
   killer's usual distance, and `BarrierBreakSeconds` (3) equals the old stall, so a seal in a
   corridor is never *worse* than the old barrier. If barriers now feel too strong, lower the
   seal; if detours feel too cheap, raise `ExtraLoops` or `DeadEndBraidPercent`.
4. **Watch the wisp in real play.** `LureSeconds` (6) is about ten killer steps. It may be too
   generous if the killer is pulled off a route twice per chase; `LureCount` (2) is the knob for
   frequency, `LureSeconds` for the size of the escape.
5. **Decide on audio** (Open Question 6) before Phase 7's polish, because Raylib audio adds a
   device dependency.
6. **A short "the thing is close" cue** that is not just the light dimming, and a line of
   story on the title screen. The title screen itself now exists, including the controls hint.
7. Re-run `dotnet build`, `dotnet test` and a window play session; update all tracking files.

## Completed

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

## Known Issues / Bugs

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
dotnet test                       # currently 279/279 passing
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
keys move, `B` or Space aims a barrier, `R` aims a wisp (then WASD moves the cursor,
B/Space/Enter places it, Esc cancels), and Q or Esc quits. After a run, `R` or Enter descends
again. The same keys work in the window and in the terminal.
