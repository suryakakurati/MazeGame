# Code Review — *Bioluminescent Depths* (MazeGame)

**Scope.** I read the rules (`GameState`, `Campaign`, `GameConfig`, `RunModifiers`, the catalogs), both frontends, the renderers, the audio stack, maze/pathfinding, the test suite and the documentation set. I ran `dotnet build` (0 warnings, 0 errors) and `dotnet test` (877/877 passing). **No source file was changed by this review** — this document is the only new file.

**How to read this.** Findings are labelled by severity:

| Label | Meaning |
| --- | --- |
| **P0** | A player-visible defect that destroys a run or the session. Fix before anything else. |
| **P1** | A design or control inconsistency a player will hit in normal play. |
| **P2** | Maintainability debt that will cost a contributor real time (or already has). |
| **P3** | Polish, tidiness, and things that are merely wrong on paper. |

This is not a beginner project. The layering, the seeded determinism, the invariant fuzz test, the asset compiler and the synthesized score are all better than what a course project usually contains. The criticism below is proportionate to that: the code can take it.

---

## 1. Snapshot

| Measure | Value |
| --- | --- |
| Core | 14,306 lines |
| UI | 12,753 lines |
| Tests | 15,847 lines, 877 cases from 787 `[Fact]`/`[Theory]` |
| Asset compiler | 5,513 lines |
| Documentation | 6,853 lines across seven files (`DECISIONS.md` alone is 1,980) |
| Build | warning-free, warnings-as-errors, nullable, analyzers on |
| Dependencies | Raylib-cs 8.1.0 only; PNG encoder, synth, and every sprite are in-repo |
| Largest files | `GameState.cs` 4,336 · `PixelCards.cs` 1,608 · `RaylibRenderer.cs` 1,551 · `PixelRenderer.cs` 1,257 · `RaylibFrontend.cs` 1,230 |

**Severity overview**

| Area | Grade | One-line verdict |
| --- | --- | --- |
| Rules & determinism | A− | Careful, tested, soft-lock-aware; the best part of the project |
| Test suite | B+ | Broad and honest, but blind to every screen and to content honesty |
| Architecture | C+ | One god object and two hand-written frontends |
| Rendering | B− | Genuinely handsome, plus ~800 lines nobody can reach |
| Audio | A− | The most sophisticated subsystem, and the least verified |
| UX / product | D+ | Q ends the game; no save, settings, remap, or accessibility |
| Documentation | B− | Impressive in volume, already contradicting itself |
| Fun / content | C | A superb world wrapped around one objective and 39 spreadsheets |

---

## 2. Defects

### P0 — `Q` ends the entire session from inside a run

`RaylibFrontend.TryReadCommand` maps `Q` to `GameCommand.Quit` unconditionally (`src/MazeGame.UI/Frontends/RaylibFrontend.cs:726-730`). `GameCommands.Apply` returns `false` for `Quit` (`src/MazeGame.UI/Input/GameCommands.cs:83-84`), and the `Screen.Play` handler turns that `false` into `return campaign` (`RaylibFrontend.cs:339-342`), which leaves `RunLoop`, closes the window in `TryRun`'s `finally`, and ends the process. Because the run is still `InProgress`, `TryRun` prints **nothing** — the window simply vanishes.

This is the same failure mode as the Escape bug that was just fixed, and it contradicts the comment twelve lines earlier in the same file ("Escape is the pause key, not the quit key: the run only ends from a menu the player chose", `RaylibFrontend.cs:304-306`). `Q` is also adjacent to `W` on a QWERTY keyboard, so it is exactly the key a panicking player hits.

The two frontends already disagree about what quitting means, which is the proof that this is accidental rather than designed:

| Key | Window | Console |
| --- | --- | --- |
| `Q` in play | Ends the process, prints nothing | Prints "You slip back into the dark" and ends the process (`ConsoleFrontend.cs:521-526`, `Run:56`) |
| Pause → quit run | Returns to the title (`RaylibFrontend.cs:401-411`) | Ends the process (`ConsoleFrontend.cs:840-843`, `Run:56`) |
| `Esc` in play | Pauses | Pauses |

**Fix:** remove the `Q` binding from play entirely; the pause card already owns "quit run". If a hard exit is wanted, bind it to `Esc` *on the pause card with a confirmation*, not to a movement-adjacent key.

### P1 — Escape means three different things on adjacent screens

| Screen | `Esc` does | Where |
| --- | --- | --- |
| Play | Opens the pause card | `RaylibFrontend.cs:304-310` |
| Floor cleared | **Abandons the session** (window closes) | `RaylibFrontend.cs:434-437` |
| Condition gate | **Abandons the session** | `RaylibFrontend.cs:468-471` |
| Draft | **Abandons the session** | `RaylibFrontend.cs:518-521` |
| Run lost / complete | **Abandons the session** | `RaylibFrontend.cs:531-534` |
| Paused | Resumes | `RaylibFrontend.cs:368-372` |

The README sells `Esc` as "pause" and the pause card's own `QUIT RUN` returns to the *title* — so the same key alternates between "freeze", "go back", and "throw away a 40-minute run and close the window". The cleared card is the screen a player sees most often (nineteen times a run), and it is one accidental Escape away from losing everything with no save to fall back on. Make Escape always mean "back one step", and make "leave the session" a deliberate title-screen action.

### P1 — The practice floor is a chamber, but the README says corridor

`README.md:153-154` still describes the practice floor as "a corridor nothing can reach them on", while line 44 of the same file says "a nine-by-nine chamber". The code was changed; one paragraph was missed. This is a small symptom of a large problem (see §7).

### P1 — The README's control table is wrong about quitting

`README.md:144-146` lists `Esc (in a floor)` as pause and `Q (title)` as quit. In the shipped window `Q` quits from *inside* play (`RaylibFrontend.cs:726`) and `Esc` quits from four card screens. The one table a new player will read is the one that does not match the build.

### P1 — The tutorial's controls differ between frontends, and the console's title offers keys the window does not

The window's title is navigated with `W`/`S`/`Enter` (`RaylibFrontend.cs:1118-1146`); the console's title is a key-per-entry menu (`D`, `T`, `P`, `C`, `Q`, `ConsoleFrontend.cs:555-592`) with a `[P] practice` line the window does not have. The window reaches the practice floor through the tutorial. Two first screens, two different mental models. The console comment even calls it "the three menu entries" while printing five (`ConsoleFrontend.cs:594-596`).

### P1 — Barriers and wisps are a whole feature no player can use

`GameState.BeginBarrierPlacement` / `BeginLurePlacement` (`GameState.cs:1726`, `:1741`) are public, documented, and thoroughly tested. `GameCommands` maps `BeginAim`/`BeginLureAim` (`GameCommands.cs:51-57`). **No frontend ever emits those commands** — a grep for `BeginAim|BeginLureAim` across `src` finds only Core and the command mapper.

What is being maintained for a feature no one can touch:

- the aiming mode, cursor movement, confirm/cancel and the two placement rules (`GameState.cs:1751-1903`);
- barrier seals, cracking, tear-down and the two pathfinding weights (`GameState.cs:1836-1878`, `:3372-3385`, `:3852-3885`);
- wisp targeting inside the hunter's route choice (`GameState.cs:3441-3458`);
- five sound ids and their recipes (`SfxBank.cs:66-70`);
- six `CardEffect` fields and their `RunModifiers` plumbing (`CardEffect.cs:28-34, 83-101`);
- roughly ten `GameConfig` knobs, three `FloorDefinition` fields, and a third of the invariant test's random input (`GameStateInvariantTests.cs:52-80`);
- a `README` paragraph explaining why they exist anyway.

`ARCHITECTURE.md` is honest that "the old aiming set ... is still present for floor rules and tests but no frontend reaches it". Honest is not the same as free: every new contributor has to learn two verbs that do not exist, and a future frontend that binds `GameCommand.BeginAim` would silently change the game. **Decide.** Either promote them back to player verbs (they are designed, balanced and tested — that is the cheaper path) or delete the aiming surface, the sound ids and the card fields, and leave the *rules* that a floor pre-builds a seal on.

### P2 — An 800-line renderer that nothing calls

`RaylibRenderer.Draw` (`RaylibRenderer.cs:143`) — the vector cave renderer — has **no call site anywhere in the solution**. The frontend draws the floor with `PixelRenderer` and calls only `RaylibRenderer.DrawOverlay` (`PixelRenderer.cs:268`), `DrawTitle` and `AccentFor`. Everything the old renderer owns is therefore unreachable: `DrawBackground`, `DrawCellBase`, `DrawCellDetails`, `DrawFloor`, `DrawWalls`, `DrawWallDetails`, `DrawRock`, `DrawFlora`, `DrawMossTufts`, `DrawContents`, `DrawFigures`, `DrawPlayer`, `DrawKiller`, `DrawKey`, `DrawExit`, `DrawBarrier`, `DrawLure`, `DrawEnemyAura`, `DrawMotes`, `DrawAimCursor`, `DrawVignette`, `Flicker` — roughly lines 224–1017, plus thirty colour constants at the top of the file.

That is two-thirds of the largest rendering file, and it is the *only* code in the UI that still draws barriers and wisps. It is also where a contributor will go first when they are asked to change how walls look. Delete it (or move it to a `Legacy/` folder with a `[Obsolete]` and a note) and let the pixel atlas be the one truth.

### P2 — `MaxHearts` is inert, and four cards modify it invisibly

Hearts have no ceiling (`GameState.AddHearts`, `GameState.cs:1205-1215`), and `_maxHearts` is read by exactly one place: the `MaxHearts` property (`GameState.cs:1120`). A grep across `MazeGame.UI` finds **zero** consumers — the HUD draws `state.Hearts` directly. So the field is decorative state kept alive by tests.

That alone would be untidy. The real problem is the cards:

| Card | Face says | Effect also does |
| --- | --- | --- |
| `light-feet` (`CardCatalog.cs:195-202`) | "You walk nearly 18 percent faster, but the killer gives you 8% less time between steps." | `MaxHearts = -1` |
| `featherfall` (`CardCatalog.cs:105-112`) | "The first catch each floor costs no heart, but your torch loses a step..." | `MaxHearts = -1` |
| `debt-of-sight` (`CardCatalog.cs:170-177`) | "Heal two hearts now, but your torch loses two steps..." | `MaxHearts = +2` |
| `blood-pact` (`CardCatalog.cs:71-78`) | "Heal two hearts now, but the killer gives you 12% less time..." | `MaxHearts = +1` |

Four cards change a number their face never mentions, and that number currently does nothing. That is precisely the "hidden modifier" the project's own design rule forbids (`Card.cs:8-12`), and no test catches it because the card tests only assert the *shape* of the sentence:

```csharp
// tests/MazeGame.Tests/Game/CardCatalogTests.cs:50-59
Assert.Contains(" but ", card.FaceText, StringComparison.Ordinal);
Assert.EndsWith(".", card.FaceText, StringComparison.Ordinal);
Assert.Single(card.FaceText.Split('.', StringSplitOptions.RemoveEmptyEntries));
```

`CardCatalog`'s own doc comment claims "every entry has a test asserting both halves through `RunModifiers`" (`CardCatalog.cs:33-34`). `RunModifiersTests` covers ~26 of 39 cards by hand; the rest are unverified, and no test compares prose to numbers.

**Fix:** delete `CardEffect.MaxHearts` and the four hidden deltas, or restore a ceiling and put the number in the faces. Then add a test that fails when a card's `FaceText` disagrees with its `CardEffect` — a table of expected phrases per field is enough.

### P3 — Small lies in comments

- `GameState.cs:87-88` — "How much standing still costs one step of vision"; `AdvanceLanternLoss` (`:765-790`) decays on elapsed time regardless of movement.
- `GameState.cs:2560` — "a frame long enough to be clamped below"; `Tick` never clamps, it simply performs at most one step per call.
- `PixelRenderer.cs:907-910` — says the light stamp includes "a tenth-of-a-second bucket for the lantern's flicker"; `LightStamp` (`:915-937`) has no time term, and the flicker is applied as a draw tint instead (`:888-897`).
- `ConsoleFrontend.cs:594-596` — "the three menu entries", five are printed.

Each is trivial to fix and each costs a reader five minutes of confusion.

---

## 3. Architecture

### P1 — Two frontends, two copies of the game's flow

`RaylibFrontend` (1,230 lines) and `ConsoleFrontend` (910 lines) each implement the entire screen flow: title, tutorial, practice, codex, floor intro, play, pause, draft, condition gate, room, echo, cleared, run lost, run complete. Only the *rules* (Core) and the *strings* (`Tutorial`, `CardCatalog`, `StoryCatalog`) are shared.

The consequences are already visible:

- The quit semantics differ (§2).
- The title menus differ (§2).
- The console has no `StepRepeat` walking; it relies on terminal auto-repeat, so the feel of movement differs from the window.
- The practice floor, the echo card and the pause card each exist twice, once per frontend.

The window loop is a state machine driven by `Raylib.IsKeyPressed`; the console loop is a blocking `ReadKey` state machine. They cannot share rendering, but they *can* share a controller: a `GameSession` that owns the `Screen` enum, the selections, the timers and the transitions, driven by a small `Input` record and a `View` interface. That is one refactor, and it deletes roughly 40% of both files while making every future screen single-source.

### P2 — `GameState` is a god object

4,336 lines, ~50 fields, ~110 methods, and at least a dozen responsibilities:

- maze and placement (constructor, `ChooseHunterStarts`, `PlaceThreadGates`, `ChooseEchoCell`);
- movement and momentum (`Step`, `TrackMomentum`, `NextPlayerStepSeconds`);
- five powerups and their state;
- hunter AI, perception, wandering, pack clock (`AdvanceEnemy`, `RefreshHunterSenses`, `WanderRoute`);
- visibility, memory, auras, bloom glow, echo glow, map reveal (`RecomputeVisibility`);
- floor rules (`SealDeadEnd`, `FractureMaze`, `RelocateExit`, `AdvanceLongDoor`, `AdvanceLanternLoss`);
- descent conditions (`AdvanceConditions`);
- catch/respawn economy (`TakeHit`, `ChooseRespawnCell`);
- hearts and spending hearts;
- story (the Echo and its lines);
- the UI event queue (`Raise`, `DrainEvents`);
- dead aiming state (§2).

The XML docs are excellent, and the file is *readable* — but it is no longer reviewable. Nobody can hold the interactions of six timers, three hunters, a veil, a lantern and a fracture in their head at once, and the existing tests reflect that: they are per-mechanism, not per-interaction.

A low-risk split that keeps every test meaningful:

| New type | Takes | Evidence it is already separable |
| --- | --- | --- |
| `FloorMap` | maze, placement, gates, echo, exit, keys, respawn cells | `ChooseRespawnCell`, `FindFurthest`, `RelocateExit` only need the grid |
| `HunterDirector` | the three hunters, senses, routes, pack/listener clocks | `Hunter` is already a private class with its own fields |
| `Visibility` | `_visible`, `_enemyGlow`, the BFS helpers | `RecomputeVisibility` reads state but mutates only these two sets |
| `FloorRules` | the four rule timers + fracture/regrowth | `AdvanceConditions` is already a table |
| `Powerups` | charges, veil/drag/lantern/shove state | five independent field clusters |
| `RunClock` | step cooldowns, sprint, momentum | already isolated by `AdvancePlayerClock`/`NextPlayerStepSeconds` |

### P2 — Numeric plumbing with a 37-argument constructor

`RunModifiers` takes **37 positional parameters** in its private constructor (`RunModifiers.cs:35-73`), then re-lists all 37 in `With` (`:320-369`) and again in `Apply`'s `with` block (`:403-430`). Adding one card property touches: `CardEffect`, `RunModifiers` (field, ctor, `With`, `Apply`, `Describe`), and the call sites in `None`.

The types are not distinct: `int visionRadius, int enemyAuraDistance, int barrierBudget, int maxHearts, int keyCount, int bloomGlowRadius, ...` is a row of same-typed integers. A silent transposition compiles cleanly and only shows up as a weird card. Group the numbers by domain (`Vision`, `Hunt`, `Hearts`, `Kit`, `Draft`) and pass five records; the compiler then does the work.

### P2 — `GameConfig` as a tuning monolith, including knobs for dead features

969 lines of init-only properties, each with a paragraph of documentation — one per mechanic that has ever existed. It is the project's extension seam, and it has become the project's attic: `BarrierBudget`, `BarrierSealSeconds`, `BarrierBreakSeconds`, `SealedBarrierCost`, `ObstacleCost`, `LureCount`, `LureSeconds`, `LureStunSeconds`, `LureVisionPenalty` are all inert for the player, and `MaxHearts` is inert for everyone (§2).

This is the mirror image of the card problem: the "seam" is a list of knobs, so a new idea becomes a new knob, and the game's actual verbs are buried among numbers that no longer matter. Split it by system (`SightConfig`, `HuntConfig`, `KitConfig`, `RunConfig`) and delete the dead ones.

### P2 — The two content catalogs are the design

`FloorCatalog` (20 floors) and `CardCatalog` (39 cards) are pure data, which is the right call, but the *content itself* is mostly scalar: floors differ by key count, hunter count, step time and size; cards differ by ±6–20% on a handful of axes. The architecture is a content engine driving content that does not yet use its range. That is a design finding, not a bug — see §6 and §10.

---

## 4. Performance

Nothing here is currently slow — a 25×25 floor with three hunters runs comfortably. The concern is that the hot path allocates on every frame, and the timing figures quoted in the docs were measured by hand rather than by the suite.

### P2 — A breadth-first search per hunter, per frame

`Tick` runs, for each hunter, `SecondsUntilNextStep(hunter)` (`GameState.cs:2643`). That calls `EnemyRouteToTarget` (`:3403`), which first calls `RefreshHunterSenses` (`:3463`), which performs `DistancesFrom(_maze, hunter.Cell, radius)` — a full BFS that allocates a `Dictionary<Cell,int>` (`:3779`). This happens **even when the cached route is valid**, because senses are refreshed before the cache check.

At 60 fps on the three-hunter floors that is ~180 dictionary-producing BFS walks per second, for a value that only changes when the hunter or the player moves. It is the same cost that makes `RecomputeVisibility` allocate four to nine dictionaries per player step (`:3595-3737`: torch, safe-room clue, lantern spill, lure, echo check, echo halo, one per hunter).

Two cheap fixes:

1. Refresh senses only when a hunter's route is invalidated or the player steps — not on the clock query.
2. Keep small reusable buffers (a `Dictionary<Cell,int>` pool or a flat `int[]` indexed by cell) inside `GameState`; `DistancesFrom` is otherwise a clean function.

### P3 — Smaller hot-path habits

- `OpenRandomWall` allocates `new[] { Direction.East, Direction.South }` inside the nested loop (`GameState.cs:4028`, again at `:4075`); `SealDeadEnd` allocates a `List` on every interval (`:3933`).
- `PixelRenderer.TrackExploration` walks the whole maze every frame (`:951-968`, called at `:876`) with an `IsVisible` hash lookup per cell, although the state already owns the visible set — iterate that instead.
- `UploadLight` unloads and reloads the GPU texture on every change (`:1034-1036`) instead of `UpdateTexture`.
- `DijkstraPathfinder.FindPath` allocates two `int[CellCount]` arrays and a heap per call (`DijkstraPathfinder.cs:80-90`); on the deep floors that is called for every hunter after every player step (plus a second call when a wisp burns).

None of this is user-visible today. All of it is the reason the game will stutter on a large floor or a slow machine before it needs to.

---

## 5. Tests and verification

The suite is genuinely good: 877 cases, one class per concern, a property test that plays random games (`GameStateInvariantTests`), and named tests that read like sentences. It is also the reason the rules can be trusted.

The gaps are specific:

### P2 — No frontend is tested, and that is where the last three bugs lived

There is not one test for `RaylibFrontend` or `ConsoleFrontend`. Rendering tests cover pure geometry (`PixelLayout`, `TileMask`, `FloorStyle`), not screens. So the three defects this project has hit in recent passes — Escape closing the window, the practice screen drawing a black frame, and the current `Q`-quit — were all found by hand, in the one layer with no automated coverage.

The frontends resist testing because they are static methods over Raylib. Extract the state machine (§3) behind an `IScreen`/`InputSource` pair and you can test transitions headlessly: "Escape on the draft returns to play", "Q does nothing in play", "Enter on the cleared card opens the draft", "the practice floor ends at Won and not before".

### P2 — The property test only exercises the dead half of the game

`GameStateInvariantTests` drives movement and aim commands, with `StartingHearts = 1`. It never uses the five powerups, the four floor rules, the descent conditions, a second hunter, the echo, the safe room or a long-door hold. The interesting interactions — a sleeper waking during a veil, a fracture closing the corridor behind a throw, a catch on floor 19 with one heart left — have no fuzz coverage. Adding a second generator that picks uniformly from `TryBlink/TryVeil/TryDrag/TryThrowLantern/TryShove` and a random condition set would multiply the suite's value for maybe 60 lines.

### P2 — Content tests pin constants instead of checking the content

`Assert.Equal(39, CardCatalog.ShippedCount)` (`CardCatalogTests.cs:22`) and `ExactlyFifteenNumericCardsStack` with a hard-coded id set (`:72-99`) mean every content addition edits the suite. Worse, they give the *appearance* of content verification while checking none of the things that can actually be wrong (a face that contradicts its effect; a card whose downside lands after the run ends; a card that is strictly better than skipping).

Prefer invariants: every card's prose contains a phrase for each non-default effect field; every card's `HealNow` is ≥ 2 (the skip price) when it has a drawback; no card's effect is all-positive; every floor's `KeyCount` fits its maze; every floor's rule is reachable in that floor's config.

### P3 — Audio output is untested by construction

`Synth`, `ScoreRenderer`, `MusicMix` and `AudioEngine` have real tests (levels, seams, loop-point continuity, allocation-free blocks), which is impressive. There is no golden audio or spectral check, so a change that makes every floor sound like a foghorn would pass. Not urgent for a course project; worth one snapshot test if this ever ships.

---

## 6. UX, product, and accessibility

### P1 — There is no save, and the run is 40–70 minutes

A run is 20 floors of 11×11 to 25×25 mazes, walked at 0.26 s per cell. Even at a brisk 3 minutes a floor that is an hour. Nothing is persisted: closing the window, a crash, a `Q` key, or a laptop going to sleep loses the whole run. A grep across `src` finds no serialization, no save file, no settings file.

The irony is that persistence is nearly free in this design. `Campaign` is fully determined by `(runSeed, floor index, cards taken, hearts, conditions, rooms used)`; floors and drafts are already derived from the seed (`GameFactory.FloorSeed`, `DraftFactory.Mix`). A save is a dozen integers and a string list — no maze, no entity positions.

### P1 — No settings at all

Confirmed by grep: no volume control (the only `Volume` is an internal mixer field), no fullscreen toggle, no key remapping, no gamepad support (a comment in `GameCommands` mentions it), no window icon, no pause when the window loses focus, no way to mute the music while keeping effects. The title has four entries and none of them is Settings.

On a laptop, this is the difference between "a project" and "a game": people will alt-tab, they will want the music quieter, they will want fullscreen.

### P1 — Accessibility is absent

- **Colour carries meaning**: magenta blooms, cyan arch, pink hunter glow, cream safe room (`PixelRenderer` colours). There is no shape/label alternative and no colourblind mode.
- **Flashing**: static, embers, ash and rain weather layers, plus the damage flash and the `pulse` on the last heart. No reduce-motion or reduce-flashing option.
- **Text**: a 5×7 bitmap face at integer scale. At the minimum window size it is legible on a monitor and marginal on a projector or a laptop at 125% scaling.
- **Difficulty**: no assists, no sliders, no "I am new" mode. See below.

### P1 — The difficulty curve is a cliff, and it is not adjustable

The numbers, from `FloorCatalog` and `GameConfig.ForFloor`:

| Floors | Hunters | Hunter step (range across the arc) | Player step | Catch cost |
| --- | --- | --- | --- | --- |
| 1–5 | 1 | 0.90–1.40 s | 0.26 s | 1 heart |
| 6–10 | 1–2 | 0.80–1.00 s | 0.26 s | 2 hearts |
| 11–15 | 2 | 0.54–0.80 s | 0.26 s | 3 hearts |
| 16–20 | 3 | **0.22–0.60 s** | 0.26 s | **4 hearts** |

By floors 19–20 a hunter covers a tile faster than the player does (0.28 and 0.22 s versus 0.26 s), three of them are hunting, and a single catch costs four hearts while the run may be carrying three. The intended answer is "draft speed and heals", but a run that drafted poorly is not merely harder — it is arithmetically lost, and the player has no way to know that until the floor starts. There is no difficulty setting to fall back to, and no save to retry from.

For a roguelike this is not automatically wrong — dying is the genre — but it should be a *choice*, and the game should tell the player the price before floor 16, not after the first catch.

### P1 — The most distinctive content is the rarest

- **Safe rooms** appear on floor 1 and then roll 10% per floor (`SecretRoomCarver.ChancePercent`): about 2.9 rooms per run. There are **eight** room kinds (`RoomCatalog`). A typical run sees two or three of them; `The Turn` (a five-floor rewind) requires floor 6+ *and* a 5/100 kind roll *and* a room roll, which puts it in a small minority of runs. The best-written content in the game is behind its rarest dice.
- **`House Favour`** is the only `SuperRare`, at weight 1 against pools of ~139–141 total weight. That is ~0.7% per offer, ~2% per three-card draft, ~33% across a whole run — and what it buys is a casino whose 45% double is 0.9× expected value per heart, for a permanent vision penalty. The rarest card in the game is a bad bet guarding a losing table.
- **Floor rules** are on 4 of 20 floors (14, 18, 19, 20). The other sixteen differ by counts and percentages.

### P2 — No post-run information, no persistence of achievement

The run summary reports floors and hearts. It does not report time, blooms, catches, cards taken, rooms visited, deepest floor (that is in the page count), or a best-ever. The "garden" is a lovely idea — a strip of marks for every bloom and page — but the README says outright that "nothing about it survives the run", which turns a score into a receipt.

### P2 — Audio is excellent and uncontrollable

Twenty hand-arranged pieces, three stems, bar-aligned intensity, 22 synthesized one-shots, a 46 ms device period, a silent fallback on headless machines. This is the best-engineered subsystem in the repository and the player can only turn none of it down.

---

## 7. Documentation

6,853 lines of prose against 42,700 lines of code is 16% — unusual, and for a course submission, impressive. It is also the fastest-drifting part of the project, and it is already wrong in ways that matter:

| Claim | Reality |
| --- | --- |
| `README.md:144-146` — `Esc` pauses, `Q` quits only at the title | `Q` quits from play; `Esc` quits from four cards |
| `README.md:153-154` — practice floor is "a corridor" | It is a 9×9 chamber (`PracticeTutorial`) |
| `README.md:200` — "Collect all three blooms" | Floors ask for 2–5 (`FloorCatalog`) |
| `CardCatalog.cs:33-34` — every card's halves are tested | Faces are only shape-checked (`CardCatalogTests.cs:50-60`) |
| `GameState.cs:87-88` — standing still costs vision | It is elapsed time (`:765-790`) |
| `PixelRenderer.cs:907-910` — stamp has a flicker bucket | No time term in `LightStamp` |
| `ConsoleFrontend.cs:594-596` — "three menu entries" | Five are printed |
| `ARCHITECTURE.md` — describes the vector RaylibRenderer as the renderer | The game renders from pixel atlases; the vector path is unreachable |

The failure mode is structural: `PROGRESS.md` (1,366 lines), `HANDOFF.md` (516), `DECISIONS.md` (1,980) and `FUTURE.md` (1,978) all restate the same facts in different tenses. Every change has to be written four times, so one of them is always behind. Trim to: a short README, one architecture doc, a changelog, and short ADRs (one file per decision). Delete anything that restates what the code says; keep only the *why*.

The good news is that the prose that is *interesting* is genuinely good — `DECISIONS.md` has real reasoning, and `StoryCatalog` is the best writing in the project.

---

## 8. What is genuinely strong (and should be protected)

Not padding — these are the things a refactor must not break:

1. **Rules sit behind one object and one tick.** `GameState` takes time in, never reads a clock; every UI decision is downstream. This is why the tests can exist at all.
2. **The soft-lock analysis is real.** Exit walkable while locked, seals always tearable, wisp can never change reachability, hunters start in dead ends, fracture checks connectivity. Someone thought about the failure states.
3. **Determinism is designed, not accidental.** Per-floor seeds, salted streams for rooms/drafts/shops, seeded gamble, `SeededRandom` injected everywhere. A replay reproduces the run.
4. **The hidden-information AI is a real idea.** Hunters that only know your cell inside the aura, remember where they last saw you, and wander otherwise: this is the single best piece of *game design* in the codebase and it is implemented plainly (`RefreshHunterSenses`, `WanderRoute`).
5. **The asset pipeline is a compiler with a brake.** `--check` rejects themes that are only a tint, neighbours that are nearly identical, non-deterministic output, and committed atlases that have drifted. That is a professional habit.
6. **Audio has no files and no excuses.** A DSP toolkit, a score renderer with ten styles and 3/4 and 5/4 meters, stems that arrive on the bar, a silent headless fallback, and allocation-free mixing.
7. **The build rules.** Warnings as errors, nullable, analyzers, XML docs required on Core. The restraint shows in the code.
8. **The story.** Twenty authored pages with a consistent voice, delivered on a stone in a dead end, are better than most indie roguelikes manage.

---

## 9. Prioritised fixes

| # | Severity | Fix | Cost |
| --- | --- | --- | --- |
| 1 | P0 | Remove `Q` from play; route quitting through the pause card | 5 min |
| 2 | P1 | Make `Esc` mean "back" on every card, never "end the session" | 30 min |
| 3 | P1 | Save and resume a run (seed + cards + hearts + floor) | 1–2 days |
| 4 | P1 | Delete the unreachable vector renderer (~800 lines) | 1 h |
| 5 | P1 | Either ship barriers/wisps as verbs or delete the aiming surface | 1 day either way |
| 6 | P1 | Fix the seven wrong documentation claims; add a doc-drift check | 2 h |
| 7 | P1 | Extract a shared screen controller so the frontends cannot drift | 3–5 days |
| 8 | P2 | Delete `CardEffect.MaxHearts` and its four hidden deltas | 30 min |
| 9 | P2 | Add a card-face/effect consistency test | 2 h |
| 10 | P2 | Move `RefreshHunterSenses` off the per-frame clock path | 2 h |
| 11 | P2 | Split `GameState` into five collaborators | 1 week, mechanical |
| 12 | P2 | Group `RunModifiers`' 37 numbers into domain records | 1 day |
| 13 | P2 | Add frontend state-machine tests once (7) lands | 1 day |
| 14 | P3 | Settings: volume, fullscreen, remap, colourblind markers | 2–3 days |

---

## 10. Five high-impact changes to make this a game people *want* to play

The systems are already better than the game they are wrapped around. These five changes spend that capital where players actually feel it.

### 1. Give each arc of the descent a different question

**Today:** all twenty floors ask the same thing — collect 2–5 blooms, reach the arch. Four floors add a rule; the other sixteen are the same objective with different numbers.

**Change:** make eight to ten floors ask a genuinely different question, using machinery that already exists:

- **The carrying floor** (`KeyCount` + `PlayerStepScale`): one bloom must be carried to the arch; carrying slows you, so the last leg is a chase.
- **The closing floor** (`FloorRule.Fracture` as a pursuit): walls close behind you on a timer instead of at random, so the route is a decision.
- **The light floor** (`FloorRule.BurningLantern` + `LongDoor`): your torch is the resource and the arch must be held while it burns down.
- **The echo floor** (Echo + no aura): the only way to find the arch is to follow the stone, so the floor is a listening puzzle.
- **The two-door floor** (ThreadGates as a choice, not a shortcut): two gates, only one survives the crossing.

**Why it is high impact:** variety is what players retell. "The floor where I had to carry the flower while it was breathing down my neck" is a memory; "floor 14, three hearts, 0.54 s" is not. This is content work inside an engine that is already built for it (`FloorRule`, `FloorDefinition`, `GameConfig` composition).

### 2. Persist the run, and make the garden the meta-progression

**Today:** no save. An hour-long run dies with the process, and the herbarium is wiped at the end by design.

**Change:**

- Save `{runSeed, floor, hearts, cards, conditions, rooms used, casino flags}` on every floor clear and on pause-exit; add `CONTINUE` to the title. Because floors and drafts are seed-derived, this is a small JSON file, not a world save.
- Keep the garden across runs: pages found, deepest floor, blooms picked, runs ended. Unlock cosmetic herbarium pages, floor intros and alternate title art from it.

**Why it is high impact:** it converts a one-sitting demo into a game with a reason to come back, and it removes the single worst failure state (losing an hour to a keypress or a crash). The codebase is one of the few that makes this cheap.

### 3. Turn the draft from accounting into identity

**Today:** 39 cards, roughly 30 of them are ±vision, ±aura, ±speed, ±charges, with percentages between 6 and 20 and the same three downside currencies. The draft happens 19 times — it is the game's main decision layer — and it mostly asks "which small number do I prefer".

**Change:**

- Cut to 18–22 cards. Every card must change *how you play*, not how fast a number ticks. Worked examples using existing hooks: a card that makes your thrown lantern pull hunters instead of the player (`Lantern` + route target); a card that seals the walls you blink through (`TryBlink` + `Grid.AddWall`); a card that makes the arch cost a heart to open (`LongDoor` variant); a card that turns every catch into a 4-second hunter stun but halves your grace (`TakeHit`).
- Write faces in the gardener's voice, and move the number to the HUD or a short stat line: "The roots will take the light instead of you." rather than "your torch loses a step for the rest of the run."
- Keep the honest-trade rule (it is good), but make the trades *qualitative*.

**Why it is high impact:** builds are the reason people replay roguelikes. Right now a "build" is a stack of multipliers; a build should be a sentence the player says about their run.

### 4. Sell the catch — make the hunt cinematic and legible

**Today:** a catch subtracts hearts, flashes red, and teleports the hunter far away. The hunter's slide and the aura are good; the *moment* is not staged. Off-screen hunters have no directional cue, so a catch can feel arbitrary on the deep floors.

**Change:**

- 0.4–0.6 s hitstop, a camera nudge, and a visible throw: the hunter is already animated between cells, so let the player watch it fly.
- A single line of voice on the catch ("It lets go of you. It is not finished.") from the gardener's voice already in `StoryCatalog`.
- A directional audio sting and a brief bloom-glow ring when a hunter is within two steps and unseen — the bloom-glow system already exists (`StartBloomGlow`).
- When the arch unlocks, freeze the frame for a beat and light the whole floor's blooms; that is the payoff the game has been building to.

**Why it is high impact:** indie feel is mostly juice in the 200 ms around consequences. This is the cheapest change on the list per unit of felt quality, and it uses `GameEvent`, the audio stems, and the vignette that are already there.

### 5. Build a front door: settings, accessibility, and a five-minute opening

**Today:** the only way in is a twelve-page tutorial, and the first real floor already has a hunter. There is no volume, fullscreen, remap, colourblind mode, reduce-motion option, or difficulty choice.

**Change:**

- A settings card reachable from title and pause: master/music/effects volume, fullscreen, screen-shake and flashing reduction, remap, colourblind markers (letters or shapes on bloom/arch/hunter, since colour currently carries all meaning).
- A first-descent mode: floor 1 with no hunter, floor 2 with one that wakes after the first bloom, then the normal curve. The tutorial becomes optional and contextual ("you found a bloom" beats a page of prose).
- An assists panel for the late floors: one fewer hunter, +1 heart grace, or 15% slower hunters, clearly labelled as assists (no scoreboard shame — there isn't one).

**Why it is high impact:** the first five minutes decide whether anyone sees the twenty floors, the story, the score and the art. Right now the front door is the tutorial, and the back door is `Q`.

---

### Closing note

The engineering here is ahead of the design, which is a good problem to have: the rules engine, the seeded determinism, the AI's hidden information, the sprite compiler and the score are all assets that most indie teams would love to start from. The gap is not capability — it is the last 20% that turns a system into a game: persistence, settings, a legible catch, objectives that differ, and cards that ask a question instead of a percentage. Fix the `Q` key today; the rest of the list can be scheduled with confidence.
