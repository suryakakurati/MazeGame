# Refinement pass — what to fix before the next content wave

**Status:** audit of the current build. No source file was changed by this audit; this document
is the only addition.
**Baseline:** `dotnet test tests/MazeGame.Tests/MazeGame.Tests.csproj` — **847 passed, 0 failed**;
the build is warning-free. The suite is green, so everything below is an untested seam or a
content/text mismatch rather than a regression the tests should already have caught.

**Scope:** fix and finish what is already shipped. No new cards, floors, rooms, conditions,
screens, settings, or verbs. A refinement may delete dead state or change a rule's implementation,
but it should not add a system.

**Coverage:** this pass read every shipped system: movement and hunters, vision and the Echo, the
five powerups, the six floor rules, conditions and gates, curiosities, secret rooms and relics,
rooms and the casino, drafts and rarity, tutorial and practice, the campaign and garden, save/load,
both frontends, audio, and the docs. Only the systems with a finding are below; a system with no
entry is one this pass found nothing actionable in, not one that was skipped.

**Severity**

| Label | Meaning |
| --- | --- |
| **P0** | Ends or corrupts a run/session for a player who did nothing wrong. |
| **P1** | A normal player hits it and loses progress, a promise, or a clear advantage. |
| **P2** | Wrong value on screen, an offer that cannot deliver, a rule that contradicts its own docs, or a maintainability trap that has already produced a bug. |
| **P3** | Polish, stale comments, small text and cleanup. |

---

## 1. Executive summary

| # | Finding | Severity | Area |
| --- | --- | --- | --- |
| 1 | `--console` cannot pass the first gate; a console run that clears floor 7 ends there with a false death | **P0** | Console frontend |
| 2 | `CONTINUE` regenerates a *different* maze for the same floor, because floor seeds come from `GameConfig.Seed` (null in `Program`) instead of the run seed | **P0/P1** | Save |
| 3 | `Lucky Find` writes a negative queued draft bonus that `RunSnapshot.Validate` rejects, so resuming deletes the save and starts a new run | **P1** | Save |
| 4 | The save banks the current floor's keys but rebuilds the floor with those keys present, so keys can be farmed; curiosity blooms are lost; Mute's heart is granted again on every reload; a spent room is unspent | **P1** | Save / garden |
| 5 | The Mirror drops every one-shot effect (`HealNow`, draft-size changes, House Favour's casino), and the Stall/relic altar defer "heal now" to the next floor | **P1** | Campaign / content |
| 6 | Rooms can sell promises they cannot keep: the Toll on one-hunter floors and the last floor, the Seed Vault on floors 18–20, the Turn's two identical options on floor 6, House Favour from the Mirror on floor 20 | **P1** | Rooms |
| 7 | The console clock keeps running while the Echo and room cards freeze the floor, so reading them can cost the player a heart | **P1** | Console frontend |

Most of these come from two seams: **the run seed and the floor seed are not the same number**,
and **"gain a card" is implemented in more than one place**. The two console items come from a
third: the terminal frontend re-implements the run flow instead of reading the same campaign
properties the window does. Fixing those three seams removes most of the list.

---

## 2. P0 — the console frontend cannot pass a gate

**What happens.** Play `--console` until floor 7 is cleared. The terminal prints the page, prints
the draft, takes a card (or the skip), and then calls `campaign.TryAdvanceFloor()`
(`src/MazeGame.UI/Frontends/ConsoleFrontend.cs:195-209`). Floor 8 is a condition gate, so
`Campaign.TryAdvanceFloor` refuses to descend until `TakeCondition` has been called
(`src/MazeGame.Core/Game/Campaign.cs:860-871`). The console never reads `ConditionOffer`
(a search for `ConditionOffer|TakeCondition` in `ConsoleFrontend.cs` finds nothing). The method
returns `RunExit.Ended`, and the outer `Run` loop treats that as a finished run: it records the
garden, clears the save, and prints a caught/lost ending, because `campaign.Status` is still
`InProgress` and the only other branch is the "caught" text.

The player is alive, floor 7 is cleared, and the run silently ends. `--console` is also the
automatic fallback when no window can open, so this is the *only* way to play on a headless
machine or over SSH. The window has the correct order: `Screen.FloorCleared` sends the player to
`Screen.ConditionDraft` first, then `Screen.Draft` (`src/MazeGame.UI/Frontends/RaylibFrontend.cs:
500-510`).

**Fix.** In `PlayCampaign`, after `PrintPage` and before `PromptForDraft`, add the gate beat:
while `campaign.ConditionOffer is { } offer`, print the two conditions, read a numbered choice,
and call `campaign.TakeCondition(...)`. Then offer the draft and advance. A gate has no skip, so
the interactive loop cannot exit without a choice; when `Console.IsInputRedirected` is true, take
the first offer and print which rule was taken, mirroring the draft's redirected-input path (a
`ReadKey` there would throw). Add a test for the flow — the console is not covered at all today,
which is why this survived.

**Why it is P0.** It does not merely misdraw a screen; it ends a 20-floor run at the first gate
and discards the save.

---

## 3. P0/P1 — CONTINUE rebuilds the same floor number in a different cave

**The seed split.** `Campaign` stores a `_runSeed` used for drafts, floor faces, rooms, the casino
and the refusal (`Campaign.cs:222`). `GameFactory.FloorSeed` builds the maze seed from
`_config.Seed` (`src/MazeGame.Core/Game/GameFactory.cs:262`). `Program.cs` constructs
`new GameConfig()` with no seed (`src/MazeGame.UI/Program.cs:17`), so `_config.Seed` is `null`.
`FloorSeed` therefore returns `null`, and `_createRandom(null)` creates a fresh `Random` every
time (`SeededRandom`).

The result:

- A fresh run's `_runSeed` is `Random.Shared.Next()`, but its floors are generated from *unseeded*
  randomness.
- `RunSnapshot` stores `RunSeed`, but `GameFactory.RestoreCampaign` passes the same `CreateFloor`
  delegate, which again uses `_config.Seed` (still null). The restored floor is a new maze.
- Keys, exit, hunters, the secret-room site, the curiosity, the altar and the relic's placement
  all move. (The relic's *kind* is drawn from `_runSeed`, so that one thing stays; the room it
  sits in does not.) The save's docs and the README both say CONTINUE "rebuilds that floor from
  the seed"; it does not.

The tests hide it: `RunSnapshotTests.AShippedRun_RestoresThroughTheFactory` seeds the factory with
`Seed = 7`, so `_config.Seed` and `RunSeed` happen to agree. `Program` never does that.

**Fix.** Make the run seed the only floor seed:

1. In `CreateCampaign`, generate/choose the run seed first, then pass it to both `Campaign` and the
   floor builder.
2. Change the floor builder signature (or wrap it in a small per-run delegate) so
   `GameFactory.CreateFloor` receives the run seed and uses it for `FloorSeed`, the maze, the
   room salt and placement.
3. `RestoreCampaign` uses `snapshot.RunSeed` for the same builder.

Add a test that constructs `GameFactory` with `new GameConfig()` (no seed), creates a campaign,
snapshots it, restores it, and compares the encoded maze / player / exit. That test should fail
against the current code.

**Why it matters even beyond the save.** `Campaign.RunSeed` is documented as "the seed the whole
run was built from" and is used for the music, faces and drafts; the cave is the one thing it does
not control. Two runs that should be reproducible by the seed are not.

---

## 4. P1 — `Lucky Find` can make a save unreadable

`Campaign.AddCard` applies the card's queued draft-size changes:

```csharp
_nextDraftBonus     += card.Effect.NextDraftOfferBonus;      // +1
_followingDraftBonus += card.Effect.FollowingDraftOfferBonus; // -1
```

(`Campaign.cs:1330-1336`). The next `CreateDraft` moves `_followingDraftBonus` into
`_nextDraftBonus` (`Campaign.cs:846-854`). Both fields can therefore be negative for one or two
floors after the card is taken.

`RunSnapshot.Validate` rejects any negative queue:

```csharp
if (CarryHearts < 0 || BloomsBanked < 0 || DraftRerolls < 0 || NextDraftBonus < 0 ||
    FollowingDraftBonus < 0 || ...)
{
    throw new ArgumentException("A save cannot bank a negative number.");
}
```

(`src/MazeGame.Core/Game/RunSnapshot.cs:112-115`). The window writes the snapshot on SAVE &
QUIT; the console writes it at the top of every floor (`ConsoleFrontend.cs:162`). So:

1. Take `Lucky Find` in the draft after floor N.
2. Reach floor N+1 (or N+2) and save/quit (or let the console write its floor-start save).
3. Resume: `Campaign.Restore` throws `ArgumentException`; both frontends clear the save and start
   a *new run* (`RaylibFrontend.cs:718-724`, `ConsoleFrontend.cs:126-139`).

The player loses the entire run to a legal card. It is invisible in the suite because the restore
tests never hold `Lucky Find`.

**Fix.** Validate the queue against its legal range, not against zero. The queue is bounded by the
draft clamp (`DraftFactory.DefaultOfferCount`, `MinOfferCount`, `MaxOfferCount`): a following
penalty can be at most `MinOfferCount - DefaultOfferCount` (currently `-2`). Either allow that
range in `Validate`, or store the pending changes as a signed queue with an explicit clamp in
`CreateDraft` and validate that. Add a round-trip test with a held `Lucky Find` and both a
`-1` following bonus and a `-1` next bonus.

---

## 5. P1 — the save's bloom and floor state is inconsistent (and farmable)

`RunSnapshot` deliberately does not store the floor; it rebuilds it from the seed. That is a
reasonable checkpoint model, but the fields that *are* stored mix floor-start and mid-floor state.
The result is both duplication and loss.

### 5.1 Keys are banked and then respawn

`Snapshot` sets `BloomsBanked = _bloomsBanked + _floor.KeysCollected` (`Campaign.cs:265`) and
`Restore` builds a fresh floor with every key present. Picking the same keys again adds them to
`BloomsPicked` a second time (`Campaign.cs:375`), because `_bloomsBanked` already contains them.
Repeated save/continue therefore inflates the run garden and, through `RecordRun`, the lifetime
garden. The existing test checks only the value immediately after restore
(`RunSnapshotTests.cs:68-82`); it never re-picks a key.

### 5.2 Curiosity blooms are lost

`BonusBlooms` is counted in `BloomsPicked` (`Campaign.cs:375`), but the only place it is written
into the run is `BloomsPicked` itself: `TryAdvanceFloor` (`Campaign.cs:877`), `TryReincarnate`
(`:934`) and `Snapshot` (`:265`) bank only `KeysCollected`. The arithmetic is worth stating
exactly, because the page turn hides half of it:

- On advance, the garden is `BloomsPicked + FloorsCleared`. The floor's bonus blooms leave
  `BloomsPicked` and the cleared floor adds one page, so a reliquary (two blooms) costs **one** net
  garden point and a shrine (one bloom) is **net zero** — the bloom is still not banked, it is just
  offset by the page it helped clear.
- On save/restore the page count does not change (`_floorIndex` is unchanged and the rebuilt floor
  is in progress), so the whole `BonusBlooms` disappears: a reliquary costs **two** garden points
  and a shrine costs **one**.

Either way the reward is not kept, which contradicts the shrine/reliquary promises and the "one
mark for every bloom picked" rule. The fix is to bank `BonusBlooms` with the keys on advance,
reincarnate and snapshot (and, if the curiosity is spent, to remember that so a restore cannot
pick it again — §5.4).

### 5.3 Mute's heart is granted on every reload

`GameConfig.ForFloor` adds Mute's extra heart directly to `StartingHearts`
(`src/MazeGame.Core/Game/GameConfig.cs:765-799`), and also raises `MaxHearts`. A snapshot stores
the resulting hearts (which include that bonus); `Campaign.Restore` calls `Build` with
`resume.Hearts`, and `ForFloor` adds the bonus *again*. Every reload on a Mute floor is a free
heart. This is a direct consequence of applying a per-floor bonus to a persisted number instead of
computing it from the carried hearts.

### 5.4 Room, relic and per-floor resources reset

- `_roomSpent` is not in the snapshot, so a spent Stall, Confessional, Mirror or casino is
  available again after CONTINUE. Buying the same shop card again costs hearts, but the room can
  be revisited; the casino can be bet again.
- `NextRelic` re-stocks the altar with a relic the run does *not* hold (`Campaign.NextRelic`,
  `Campaign.cs:1380-1400`). Save after taking relic A, restore, and the altar offers relic B; take
  it, save, restore, get C. The altar is free, so this is a relic farm.
- Powerup charges, the Cartographer reveal, the player's cell and the curiosity are all floor
  state; they reset to the floor's start. That is consistent with "start of the floor", but it is
  also a free refill of the kit and a free reset of the hunt's position. The README currently
  advertises SAVE & QUIT as keeping the current floor's progress, which it does not.

### 5.5 Choose one model, then make everything match it

**Option A — a real floor checkpoint.** Add a small floor-progress block to `RunSnapshot`:
player cell, collected key cells (or a hash of them), `BonusBlooms`/curiosity spent, room kind and
`_roomSpent`, relic kind and taken, powerup charges, map reveal. On restore, build the floor, then
apply that block. This is the honest version of SAVE & QUIT and fixes the duplication/exploits;
it is also the larger change, and it must not become a second copy of `GameState`'s timers (do
not save hunter routes; reset them at the floor's start, as the current rebuild does).

**Option B — an explicit floor-start checkpoint.** Save only at the start of a floor (the console
already does this), keep `BloomsBanked` as banked-before-this-floor only, do not count current
keys or bonus blooms in the snapshot, and change SAVE & QUIT/the README to say "restart this
floor". This is the smaller fix and removes the key duplication, but a mid-floor save loses the
floor's progress by design.

**Either way:**

- Decide which floor state is run state (keys, bonus blooms, room spent, relic taken) and store it
  once.
- Do not apply Mute's heart twice; compute the next floor's starting hearts in one place and make
  `HeartsOnNextFloor` the exact number `TryAdvanceFloor` passes on (see §10.1).
- Add round-trip tests that pick keys and a reliquary, save, restore, and assert the garden is
  unchanged and no key is counted twice; add a Mute-floor round trip and a spent-room round trip.
- If the altar can be re-stocked, store the relic and a taken flag so a restore cannot farm a new
  one.

The recommendation depends on whether SAVE & QUIT is supposed to keep the current floor. The
README and the pause card currently advertise a mid-floor save, so **A is the honest completion
of the feature as shipped**; **B is the smaller, consistent alternative** if the team is willing
to change that promise to "restart this floor". The code must not keep the current hybrid, which
is the only model that both loses progress and lets it be farmed.

---

## 6. P1 — one-shot card effects are applied in the wrong place (or not at all)

`Campaign.AddCard` is the one method that knows about one-shot effects:

```csharp
_modifiers = _modifiers.With(card);
_carryHearts += card.Effect.HealNow;
_nextDraftBonus += card.Effect.NextDraftOfferBonus;
_followingDraftBonus += card.Effect.FollowingDraftOfferBonus;
if (card.Effect.GuaranteedCasino && _floorIndex + 1 < _floors.Count)
    _casinoFloor = _floors[_floorIndex + 1].Number;
```

(`Campaign.cs:1330-1341`). It is called from:

| Call site | Context | Correct? |
| --- | --- | --- |
| `TakeCard` (draft) | floor is cleared; next floor is being built | `HealNow` banking is right |
| `ResolveBuy` (Stall) | mid-floor | `HealNow` is deferred and can be lost to a death |
| `Sync` (relic altar) | mid-floor | `HealNow` is deferred (`Marigold Crown`, `Second Harvest`) |
| `ResolveSwap` (Mirror) | mid-floor | **bypasses `AddCard` entirely** |

### 6.1 The Mirror drops one-shots

`ResolveSwap` builds the new totals with `RunModifiers.FromCards(next)`
(`Campaign.cs:1268-1284`). `RunModifiers` has no `HealNow` and no draft-size queue, and its
`GuaranteedCasino` flag is never consulted when a floor is built: only `_casinoFloor`, set in
`AddCard`, forces the room. So a Mirror can hand the player:

- **Blood Pact / Second Skin** — no heal at all, although the face says "Heal two hearts now".
- **Lucky Find** — no draft bonus and no penalty; the card does literally nothing.
- **House Favour** — `GuaranteedCasino` is accumulated into `RunModifiers`, but `_casinoFloor` is
  set only in `AddCard`; no room is forced and the 66% table never appears. The card is a pure
  vision loss. On floor 20 the Stall filters House Favour (`Campaign.cs:1047-1052`) but the Mirror
  does not, so this is reachable there too.

### 6.2 The Stall and the altar defer "now"

`ResolveBuy` calls `AddCard`, which sends `HealNow` to `_carryHearts`. `_carryHearts` is only
applied when `TryAdvanceFloor` builds the next floor. Buying Blood Pact at two hearts drops the
player to one heart for the rest of the floor and heals only after the floor is cleared — and if
they die first, the heal is lost. The card face says "Heal two hearts now"; the shop takes its
heart immediately.

The same is true of relics with `HealNow`: `Marigold Crown` ("Two hearts the moment you take it")
and `Second Harvest` ("A heart when you take it") are added through `Sync` → `AddCard`, so the
relic card shows the promise while the hearts do not change.

### 6.3 Fix: one gain path with an explicit context

Replace the public `AddCard` call sites with one method that takes the card and knows whether the
floor is still in progress:

```text
GainCard(card, floorCleared):
    if floorCleared: _carryHearts += HealNow
    else:            _floor.AddHearts(HealNow)   // immediate, and visible on the relic card
    apply draft-size queue
    set _casinoFloor for the next floor, when one exists
```

The draft calls it with `floorCleared: true`; Stall, Mirror and relic `Sync` call it with `false`.
The Mirror must remove the swapped-out card first, then call `GainCard` (not `FromCards`). If the
card is a relic, `Sync` has already established that it is not held. Add tests for each one-shot
through each source, and for a Mirror House Favour forcing the next floor's room.

This also makes the "House Favour from the last floor" guard one check in one place, instead of a
Stall-only filter.

---

## 7. P1 — room offers whose promise cannot be kept

`Campaign.CreateRoom` decides what a room sells from the run's *current* state, but not from the
state of the floor the promise lands on. Four options can promise something they cannot deliver;
the Toll and the paid Turn also take a heart for it.

### 7.1 Toll on a one-hunter floor or the last floor

`CreateRoom` offers the Toll when `_floor.Hearts > 1` (`Campaign.cs:960-980`).
`Campaign.Build` applies it with `Math.Max(1, floor.HunterCount - _huntersOffNextFloor)`
(`Campaign.cs:1412-1420`). On the last floor there is no next floor; on the authored early floors
(1–8, whose successor carries one hunter) the subtraction is clamped away. A `Greedy` face can
add a hunter and make the Toll work, but the option is still offered on plenty of floors where it
cannot. The player pays one heart for nothing, and the first floor's guaranteed room can be a Toll —
a new player's first room can be a trap with no upside.

**Fix:** offer the Toll only when the next floor exists and its `HunterCount > 1`. (Use the
*faced* definition, since `Campaign` has already applied floor faces.) If the next floor's hunter
count can be reduced to one by another promise, decide whether the promises stack or are
suppressed; today only one Toll promise is kept (`_huntersOffNextFloor = 1`, not `+=`).

### 7.2 Seed Vault on floors 18–20

`ResolveRoom` sets `_seedBloomsOnFloor = FloorNumber + 3` (`Campaign.cs:720-724`) and `Build` pays
when a floor with `Number >= bloom` is built (`:1428-1433`). On floors 18, 19 and 20 the target is
21, 22 or 23, which the 20-floor catalog never builds. The room is free, so the player loses
nothing but a room and a promise; the card says it blooms and pays, and it never does.

**Fix:** hide the option when `FloorNumber + 3 > _floors[^1].Number`, or clamp the payout to the
last floor. The room prompt, option note ("BLOOMS THREE FLOORS DOWN") and payout line should all
say the same thing, and the option note should mention the heart (it currently does not; the
prompt does).

### 7.3 The Turn has two identical options on floor 6

`CreateRoom` adds `back-5` and, when hearts > 1, `back-1` (`Campaign.cs:1130-1145`).
`TryReincarnate` computes `target = toStart ? 0 : Math.Max(0, _floorIndex - 5)` (`:905-910`). On
floor 6 (`_floorIndex == 5`), the free five-floor rewind already targets `0`, and the paid
"GO BACK TO FLOOR ONE" targets `0` for a heart. The paid option is strictly worse and easy to
choose by mistake.

**Fix:** hide `back-1` when the free rewind lands on floor one as well (floor 6 only), or change
the paid option's destination/label. Do not leave two prices for the same outcome.

### 7.4 House Favour via the Mirror on floor 20

Covered in §6.1: filter the card in `ReplacementFor` when no next floor exists, or route the
Mirror through `GainCard`, which can apply the same guard.

Room eligibility is the same class of bug in all four cases: an option is a price plus a promise,
and the promise must be true before the card is shown. Add a test that enumerates the shipped
floors, rooms and options and asserts that every option's promise has a target: next floor exists
for Toll, `floor + 3` exists for Seed Vault, and the Turn's destinations differ.

---

## 8. P1/P2 — the garden is not monotonic

`Campaign.GardenCount` is `BloomsPicked + FloorsCleared` (`Campaign.cs:385`), and `FloorsCleared`
is derived from the current index (`:365`). Two legal actions shrink it:

- **A Turn** rewinds `_floorIndex`, so five pages disappear from the garden even though the run
  cleared those floors. Any bloom earned on the abandoned floor that was banked is kept, but the
  pages are not.
- **A reliquary/shrine** on the current floor is counted in `BloomsPicked` but never banked
  (§5.2); descending loses the reliquary's two blooms to a single page (net −1) and the shrine's
  one bloom to that page (net 0), and a save/restore loses both outright.

The reference to "deepest" makes it worse: a run that reached floor 12, used the Turn to floor 7
and died there is recorded by both frontends with `campaign.FloorNumber` (7), not
`campaign._deepestFloor` (12). `RunSnapshot` already stores `DeepestFloor`; it is never exposed.

**Fix:** keep a monotonic page count (or a set of cleared floor numbers) and bank curiosity blooms
on advance/reincarnate; expose `DeepestFloor` and use it for `RecordRun`. Add a test: climb to a
floor with a reliquary, clear it, advance, and assert `GardenCount` did not decrease; then take a
Turn and assert the same.

Also resolve the two "gardens": the run score (`GardenCount`, shown on the cleared/run-complete
cards) and the lifetime `GardenProfile` (shown on the title) are both called the garden. The
README simultaneously says "the garden is the run's only score ... nothing about it survives the
run" and that `garden.json` keeps it across runs. Pick two names (for example *run garden* and
*the garden*) and use them consistently in the UI, README and docs.

---

## 9. P1/P2 — console time is charged for cards that freeze the floor

The window freezes the floor by not calling `Tick` while a card is up. The console does the same,
but its `Stopwatch` keeps running. `ConsoleFrontend.Play` restarts the clock after the pause menu
(`ConsoleFrontend.cs:654`), after `ReadRelic` (`:688`), and nowhere else. `ReadEcho` (`:224`,
called at `:697`) and `PromptForRoom` (`:445`, called at `:664`) both block on keys, and the next
loop calls `state.Tick(clock.Elapsed.TotalSeconds)` (`:672`) with the whole card-reading time.

Consequences: a player who takes ten seconds to read the Echo gives the hunters a ten-second step,
advances the Eclipse/Tide/Regrowth/Fracture clocks, and can lose a step of the burning lantern.
Reading a shop card can cause the same. The relic card was explicitly fixed for this; the Echo and
the room were missed.

**Fix:** call `clock.Restart()` after `ReadEcho` and after `PromptForRoom` returns, exactly as
`ReadRelic` does. Also restart after any future blocking card. A test would require a console
harness; at minimum, leave a comment in `Play` listing the blocking calls that must restart the
clock.

---

## 10. P2 — preview values, card numbers, and text

### 10.1 Mute and the skip preview are both off by one

`Campaign.HeartsOnNextFloor` (`Campaign.cs:474`) returns
`_floor.Hearts + _carryHearts + _modifiers.HealOnFloorClear`. `GameConfig.ForFloor` adds Mute's
heart separately (`:767-799`). On a Mute floor the actual next floor starts one heart higher than
the preview. The Raylib comment at `RaylibFrontend.cs:505-508` claims the preview "already includes
Mute"; it does not.

The draft footer (`PixelCards.cs:226`) prints:

```
SKIP HEALS ONE HEART - NEXT FLOOR: {campaign.HeartsOnNextFloor}
```

Before the player skips, that number does not include the skip's +1. Pressing S yields
`HeartsOnNextFloor + 1` (and `+1` more on a Mute floor). Either compute the value the player will
actually get after the highlighted choice or split the line into "NOW" and "IF YOU SKIP".

Fix the preview at the campaign level, not in two frontends: `HeartsOnNextFloor` should be the
exact number `TryAdvanceFloor` passes to `Build`, including Mute; add a `HeartsAfterSkipping`
helper or show both. Then the window and any future frontend agree.

### 10.2 Card faces vs the totals line

The card faces use "speed" percentages; `RunModifiers.Describe` prints time reductions:

| Card | Face | `Describe` |
| --- | --- | --- |
| Light Feet | "walk 17 percent faster" (`PlayerStepScale 0.855`) | `STEP +15%` (`(1-0.855)*100 = 14.5`) |
| Glass Cannon | "move 20% faster" (`0.833`) | `STEP +17%` |
| Hollow Bones | "every step ... 25% slower" (`1.25`) | `STEP -25%` |

The face is correct as a speed statement; the totals line is correct as a time statement; the two
are not the same number. Pick one convention (I would keep the face's "faster/slower" and print the
effective step time or a speed multiplier in the totals) and use it everywhere.

### 10.3 Tutorial and card text that is false today

- **Tutorial page 8:** "Press F to shove *every hunter next to you* two cells away." `TryShove`
  pushes exactly one adjacent hunter (facing preferred); every other hunter is only given its next
  step. Fix the page to "the hunter next to you" (and note that other hunters advance).
- **Tutorial page 9:** "a rule at the toll." The Toll does not add a rule; it buys one fewer
  hunter on the next floor (which, per §7.1, is sometimes no hunter fewer). Fix the sentence.
- **Tutorial page 13:** "ESC also closes a safe-room card without spending it, and cancels an
  aim." The Confessional's ESC deliberately takes its refusal option (a heart and a card); aiming
  was removed with the old barrier/lure surface. The page must say the Confessional has no free
  exit and remove "aim".
- **Tutorial page 12 / README:** "Press 1, 2 or 3" / "1–4". A draft can offer up to five cards
  (`DraftFactory.MaxOfferCount`; `Lucky Find` adds one), and both frontends number the rows. Say
  "its number" or "1–5".
- **README controls:** the window's title is a menu with `LEAVE`, not a `Q` key; the console uses
  `Q`. The shared table says `Q (title)`. Split the table by frontend or label the console-only
  keys.
- **`PixelCards.RunComplete` and `Caught`:** both say `ESC: QUIT`; the frontend returns to the
  *title* on Escape (`RaylibFrontend.cs:628-647`). Say `ESC: TITLE` or `ESC: LEAVE`.
- **`PixelCards.Pause` XML doc:** the item indices are listed `0 = continue, 1 = cards,
  2 = save, 3 = abandon`; the actual menu is 0 continue, 1 run details, 2 cards, 3 save & quit,
  4 abandon. The doc is wrong, not the menu.
- **`Tutorial` page 2:** "the footer counts what is left"; the footer chip is
  `BLOOMS collected/total`. Minor, but it is the first screen that teaches the HUD.

### 10.4 The window footer advertises tools the floor has removed

`RaylibRenderer` draws a fixed hint line with all five verbs (`RaylibRenderer.cs:614-617`),
regardless of `config.MutedPowerup` and regardless of zero charges. The console's
`ConsoleRenderer.DescribeHints` filters by charges and Mute (`ConsoleRenderer.cs:155-195`). Mute's
whole promise is "one tool is unavailable each floor"; the window never shows that, and a player
presses a key that does nothing. Filter the hint line the same way, or show the charge counts (the
HUD already has slots). This is a UI fix, not a new control.

---

## 11. P2 — the rarity curve after a Turn contradicts its own rule

`Campaign.CreateDraft` and the shop pass `effectiveFloor: _deepestFloor`
(for example `Campaign.cs:846-858`, `:1028-1038`; `ReplacementFor` also does). `DraftFactory`'s
doc says `effectiveFloor` exists because "a rewind to floor five must not hand out the rares of
floor fifteen" (`DraftFactory.cs:80-90`). Passing the *deepest* floor does exactly that: after
reaching floor 15, a draft on floor 5 uses floor-15 weights and the every-fifth-floor rare
guarantee at depth 15. The current code and the comment disagree; one of them is wrong.

Decide:

- **Measure by the current floor** (the comment's rule): after a Turn the draft goes back to the
  early-floor weights. This prevents farming late rares on a rewind but means the turn costs
  rarity as well as position.
- **Measure by the deepest floor** (the code): a Turn keeps late-run offerings. Then rewrite the
  comment and stop calling it a guard against floor-fifteen rares.

Either way, add a test with a run that has reached a deep floor, turned back, and drafts; assert
the expected tier weights/guarantee. This is a design decision, but the current state is neither
the documented rule nor a documented alternative.

---

## 12. P2 — the casino is a predetermined roll that can be save-scummed

`RoomCatalog.GambleWins` is a pure function of `(runSeed, floorNumber, winPercent)`
(`RoomCatalog.cs:66-90`). Every stake on that floor at the same odds produces the same result.
Within one room visit the room is spent after the first resolution, so this is not a per-stake
exploit in normal play. But:

- Save before opening the room, bet one heart, see the result, restore, and bet the maximum. The
  result is known and unchanged. (The save restore also resets the room's spent flag, §5.4.)
- A Turn back to a floor with a casino re-enables the room; the same roll is known from the first
  visit.

The 45% table is a losing bet in expectation, so it is only a real gamble if the player cannot
learn the outcome before choosing the stake. A save-scummer can always take the known winning
side (or leave after learning a loss). The House Favour's 66% once has the same problem.

**Fix:** mix a bet ordinal into the seed (`GambleWins(..., attempt)`), increment it per resolved
stake, and persist it in the run snapshot if the save model keeps mid-floor state. This preserves
seeded determinism and makes each bet a fresh draw. If the single predetermined roll is intended,
say so in the room's docs and close the save/Turn exploit instead. Do not leave the current
"gamble" both determined and re-playable.

---

## 13. P2 — docs that no longer describe the build

These are not cosmetic: the README is the first thing a player and a grader read, and
`ARCHITECTURE.md` is the onboarding map.

| Claim | Reality | Where |
| --- | --- | --- |
| "Every floor you reach writes the run" / "A crash costs at most the floor you were on" | The window writes only at `StartRun` (`RaylibFrontend.cs:693-696`) and on SAVE & QUIT (`:468`). The console writes at every floor (`ConsoleFrontend.cs:162`). A window crash can lose the whole run since the last manual save. | `README.md` save paragraph |
| "CONTINUE rebuilds that floor from the seed" | False until §3: floor seeds come from the null `GameConfig.Seed`. | README, `GameFactory` remarks, `ConsoleFrontend.PlayCampaign` remark |
| "SAVE & QUIT ... with the hearts and blooms earned on the floor" | Keys are banked but respawn; bonus blooms are dropped; the floor restarts. | README |
| "Collect all three blooms" | Floors ask for 2–5 (and `Greedy`/`Starved` change it). | `README.md` how-to-play |
| "a step takes about a tenth of a second" / "killer starts at 0.6" | Shipped cadence is 0.26 s and floor 1's killer is 1.20 s (the catalog ramps down to 0.22). | README |
| "22 one-shots" | `SfxId` has 21; `ArchLocked`/`ArchUnlocked` are built but never played (see §14). | README, `SfxId` |
| "the garden is the run's only score ... nothing about it survives the run" | `garden.json` persists the lifetime garden, and the title draws it. | README |
| "the run's ceiling plus ... Mute's heart" / `HeartsOnNextFloor` includes Mute | `HeartsOnNextFloor` does not include Mute; `ForFloor` adds it. | `ARCHITECTURE.md` Campaign row, `DECISIONS.md` 2026-10-20 gate note |
| "the first floor ... is not protected" from faces | `FloorFaces.Drawn` returns `Authored` for every floor below `FirstVariedFloor = 4`. | `FloorFaces` remarks |
| `MaxHearts` is "the most hearts the player may hold" / "HUD slots" | Hearts have no ceiling; `GameState.MaxHearts` and `Campaign.MaxHearts` have no UI consumer. | `GameConfig`, `FloorDefinition`, `GameState` |
| "the shipped three-floor campaign" / "twenty-three card catalog" | The campaign is 20 floors; the catalog is 39. | `Card`, `CardCatalog` remarks |
| Aiming/barrier guidance in `GameCommands` | The aiming command and barrier/lure surface are gone; `GameCommands` only forwards moves and the five verbs. | `GameCommands` remarks |
| `PixelCards.Pause` item order | The real menu has five items in a different order (§10.3). | XML doc |

Also: `Review.md` in the repository root is a previous, broader review from an earlier build
(877 tests, no save, the `Q` quit bug, the vector renderer). It is now misleading for current work.
Either mark it historical or fold it into this document; do not leave it looking current.

**Fix:** correct the claims as part of the same pass that changes the behavior. Prefer short docs
that do not restate numbers that live in code; where a count is useful, derive it or test it. Do
not add a documentation tool.

---

## 14. P2 — dead or misleading state

- **`MaxHearts`.** `GameConfig.MaxHearts`, `FloorDefinition.MaxHearts`, `GameState.MaxHearts` and
  `Campaign.MaxHearts` are documented as a ceiling or a HUD slot count, but hearts have no ceiling
  and the UI never reads the property. `FloorDefinition.MaxHearts` still affects `ForFloor`'s
  `ceiling` value, which only feeds `GameConfig.MaxHearts`. Delete the field or rename it to
  something honest (`DesignHearts`) and stop documenting a cap. This also removes one number from
  the `ForFloor`/`GameConfig` pipeline.
- **`GameState.AddHearts`** returns `0` unconditionally; the only caller ignores it. Make it
  `void` (or remove the return) so the comment "kept so callers keep compiling" cannot mislead a
  future caller into thinking a heart was refused.
- **Negative hearts.** `TakeHit` subtracts the full `CatchCost` even when it exceeds the hearts
  left (`GameState.cs:3289-3322`), so a fatal catch with one heart left and a catch cost of three
  leaves `Hearts == -2` (even on a floor whose `MaxHearts` is five). The console draws its final
  frame after the loop, so the terminal can print `hearts -2`; the window clamps the pips to zero,
  which hides the real value rather than making it right. Clamp the field at zero when the run ends
  (or subtract only what remains) and add an invariant check.
- **`SfxId.ArchLocked` / `ArchUnlocked`** are synthesized and never played (no `_engine.Play`
  call; no event raises them). Either raise them at the arch transition (the state is already
  there; no new mechanic) or delete the two recipes and fix the README count.
- **`RoomCatalog.Price(Turn)`** says "ONE HEART TO START OVER", but its `back-5` option is free.
  Make the price per option or say "FREE, OR ONE HEART TO START OVER".
- **Seed Vault option text** says "BLOOMS THREE FLOORS DOWN"; the prompt says "... and pays a
  heart". Put both in the option note, or the player thinks it is a garden bloom.
- **`PixelCards.Draft` footer** says "A / D: CHOOSE"; a draft of four or five cards has a second
  row, and only Up/Down reaches it without cycling through the whole list. Add "ARROWS".
- **`RunSnapshot.Validate`** does not check `SeedBloomsOnFloor`/`CasinoFloor` bounds or that
  `Conditions` are defined values. A hand-edited save can put a promise on a nonexistent floor.
  Low priority, but one `Enum.IsDefined` and two range checks are cheap.
- **`SaveStore`** catches `JsonException` and `IOException` but not
  `UnauthorizedAccessException`; `Write` does not catch I/O failures, so a full disk or a
  read-only folder throws out of the pause menu. Report a save failure and keep playing instead of
  killing the session. This is robustness, not a new feature.

---

## 15. P2 — tests and structure that let these bugs through

The suite is strong at rules and weak at exactly the seams above.

### 15.1 No frontend coverage

There is not one test for `ConsoleFrontend` or `RaylibFrontend`; §2 is the proof. The console is a
blocking state machine and the window is a frame state machine, so a full controller extraction is
a later refactor. The smallest useful step is a headless flow harness around the *decisions*:
a scripted input source that drives "cleared floor 7 → gate → condition → draft → advance" and
asserts the run reaches floor 8. Do not let §2 be fixed by hand without a regression test.

### 15.2 Save tests that hide the seed bug

`RunSnapshotTests` uses a seeded factory (`Seed = 7`) for the shipped-catalog restore, and a
custom deterministic builder for the rest. Neither exercises `new GameConfig()` (the shipped
configuration). Add:

- a null-seed campaign → snapshot → restore, comparing the maze, player and exit;
- a save with `Lucky Find` queued (both the `+1/-1` and `-1/0` states);
- a save after picking keys and a reliquary, asserting the garden does not change and the keys are
  not counted twice;
- a Mute-floor save, asserting the hearts do not grow;
- a save after spending a room, asserting the room is not available again (depending on the model
  chosen in §5);
- a save after taking an altar relic, asserting the altar cannot hand out a second relic.

### 15.3 Card content tests check shape, not truth

`CardCatalogTests` verifies that every face contains " but " and ends in a period; it does not
check that the face's numbers match `CardEffect`. That is how the Mirror's one-shot bug and the
"17% vs +15%" mismatch survive. Add a small table (card id → expected phrase/value) for the
cards whose face names a number, and a test that gains every card through the draft, the Stall,
the Mirror and the altar and asserts its one-shot effects fire exactly once. This is content
verification, not a new feature.

### 15.4 One gain path

`Campaign.AddCard` and `RunModifiers.FromCards` both look like "apply a card"; the Mirror chose
the wrong one. After §6, make `GainCard` the only public way to add a card and make the Mirror's
removal use `FromCards` only for the *remaining* list. A grep test or a comment on `FromCards`
("removal only; never use this to gain a card") would have caught it.

### 15.5 Structural debt worth addressing now, not later

- `GameState` is 4,378 lines and owns placement, movement, five powerups, conditions, the six
  floor rules, visibility, hunter AI, hearts, the Echo and the event queue. That is why the save has no
  clean seam for floor progress. A full split is out of scope for this pass, but a small
  `FloorProgress` DTO (keys collected, bonus blooms, relic, room spent, charges, player cell) would
  give `RunSnapshot` and `GameState` one shared vocabulary for §5 without touching the rules.
- `RunModifiers`' positional constructor (31 positional parameters, including the card list) and
  `CardEffect`'s one-shots are a checklist; every new field has to be added in five places. The
  Mirror bug is exactly what that invites. If §6 centralises gain, the checklist becomes one
  method; do not start a grouping refactor until the correctness fixes are in.
- The two frontends duplicate the title, pause, draft, gate, room, cleared and run-end flows. §2
  and §9 are both parity bugs. A shared `GameSession` controller is the long-term fix; for this
  pass, make the console read the same campaign properties the window does (`ConditionOffer`,
  `HeartsOnNextFloor`, `CanRerollDraft`, `Room`) and keep the text in Core.

---

## 16. Recommended order

| Order | Work | Severity | Cost |
| --- | --- | --- | --- |
| 1 | Console condition gate + a flow regression test | P0 | 1–2 h |
| 2 | Run seed drives floor seeds (fresh and restore) + null-seed restore test | P0/P1 | 2–4 h |
| 3 | Allow the bounded negative draft queue + Lucky Find round-trip test | P1 | 30 min |
| 4 | Choose the save model (§5), implement the bloom/Mute/room/relic invariants, add round-trip tests | P1 | 1 day (B) / 2 days (A) |
| 5 | One `GainCard` path; fix Mirror, Stall and relic one-shots + tests | P1 | 3–4 h |
| 6 | Room eligibility: Toll, Seed Vault, Turn duplicate, Mirror House Favour | P1 | 2–3 h |
| 7 | Console clock restarts after Echo/room | P1 | 30 min |
| 8 | Garden monotonicity, bank curiosity blooms, expose `DeepestFloor` | P1/P2 | 2–4 h |
| 9 | Preview values (Mute/skip), footer filtering, tutorial/run-card text | P2 | 3–4 h |
| 10 | Window autosave at floor start + honest SAVE & QUIT copy | P1/P2 | 1 h |
| 11 | Rarity-curve decision + test; casino bet ordinal decision | P2 | 2–3 h |
| 12 | Card face/effect consistency table; remove/rename dead `MaxHearts`; negative-heart clamp | P2 | 3–4 h |
| 13 | Docs pass (README, ARCHITECTURE, Card/FloorFaces/GameCommands, Review.md) | P2 | 2–3 h |
| 14 | Long-term: frontend controller, `GameState` split, `RunModifiers` grouping | — | later |

Do not start a new content wave (cards, floors, rooms, conditions) until items 1–10 are closed and
the tests in §15 exist. Content added onto the current save and room seams will multiply the
same bugs.

---

## 17. Exit criteria for this refinement pass

- A `--console` run can pass floor 8 and floor 14, choosing a condition at each, and reach the
  ending; a window run and a console run agree on the gate flow.
- A run started with `new GameConfig()` (no seed), saved and restored, has the same maze, player,
  exit, keys, altar and room; or, if the floor-start checkpoint is chosen, the save says so and
  the floor is the same floor.
- `Lucky Find` can be held across a save/continue in every queue state.
- A save/continue cannot duplicate keys, curiosity blooms, a relic or a room visit; the garden
  does not decrease on a descent or a Turn; `DeepestFloor` is what the run record uses.
- Mute's extra heart is granted exactly once per floor; `HeartsOnNextFloor` is the true number;
  the draft footer includes the skip and Mute.
- Every one-shot card fires exactly once through the draft, the Stall, the Mirror and the altar;
  every room option's promise has a target.
- Reading an Echo or a room card in the console does not advance the world.
- `dotnet test` is green, with new tests for §2, §3, §4, §5, §6 and §8; no frontend behavior is
  left to manual testing alone.
- The README's save, count and control claims match the build; `MaxHearts`, the unused arch
  sounds and the dead aiming references are either used or gone.

---

## 18. Appendix — quick reproductions and files to open first

**Console gate (P0).** Run `dotnet run --project src/MazeGame.UI -- --console`, clear floor 7.
Watch the draft resolve and the run end. Files: `ConsoleFrontend.cs` (`PlayCampaign`),
`Campaign.cs` (`TryAdvanceFloor`, `ConditionOffer`), `RaylibFrontend.cs` (`Screen.FloorCleared` for
the correct order).

**Save seed (P0/P1).** In a scratch test, build
`new GameFactory(new GameConfig(), seed => new SeededRandom(seed), new DijkstraPathfinder())`,
`CreateCampaign()`, `Snapshot()`, `RestoreCampaign(snapshot)`, and compare
`campaign.Floor.Maze` with `restored.Floor.Maze`. Expect a different maze. Files:
`GameFactory.cs` (`CreateCampaign`, `FloorSeed`, `RestoreCampaign`), `Program.cs`, `Campaign.cs`
(`_runSeed`, `Restore`, `Build`).

**Lucky Find save (P1).** Build a snapshot with `FollowingDraftBonus = -1` (or take the card and
advance two floors) and call `RunSnapshot.Validate`. It throws. Files: `CardCatalog.cs`
(`lucky-find`), `Campaign.cs` (`AddCard`, `CreateDraft`), `RunSnapshot.cs` (`Validate`).

**Bloom/Mute/room/relic save states (P1).** One test each: pick keys and a reliquary, save and
restore, re-pick; save on a Mute floor and restore; spend a shop, save and restore; take a relic,
save and restore. Files: `Campaign.cs` (`Snapshot`, `Restore`, `BloomsPicked`, `TryAdvanceFloor`,
`TryReincarnate`, `Build`, `NextRelic`), `GameConfig.cs` (`ForFloor`), `RunSnapshot.cs`.

**Mirror and Stall one-shots (P1).** Give a campaign a Mirror and a Stall, swap/buy Blood Pact,
Lucky Find and House Favour, and read the hearts, the next draft and the next floor's room. Files:
`Campaign.cs` (`ResolveSwap`, `ResolveBuy`, `AddCard`, `ReplacementFor`), `CardCatalog.cs`,
`RelicCatalog.cs` (`Marigold Crown`, `Second Harvest`).

**Console clock (P1).** Read the Echo or a room card for ten seconds on a floor with a near
hunter, and observe the catch/tide/regrowth after the card closes. File: `ConsoleFrontend.cs`
(`Play`, `ReadEcho`, `PromptForRoom`, `ReadRelic`).

**Garden (P1/P2).** Take a reliquary, clear the floor, advance; compare `GardenCount`. Take the
Turn; compare again. Files: `Campaign.cs` (`GardenCount`, `FloorsCleared`, `TryAdvanceFloor`,
`TryReincarnate`), both frontends' `RecordRun` calls.

---

*This document is intentionally limited to the systems that already exist. When items 1–13 are
closed and the tests in §15 are in place, the codebase is ready for the next content wave without
carrying a save exploit, a console dead-end, or a card that lies about what it does.*
