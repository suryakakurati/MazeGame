using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MazeGame.Core.Abstractions;

namespace MazeGame.Core.Game;

/// <summary>
/// The kinds of relic a floor's altar can hold.
/// </summary>
/// <remarks>
/// A relic is the run's permanent side reward: unlike a card, it is not drafted and has no price
/// on its face. The price is the trip — an altar always stands off the shortest route, and taking
/// one wakes everything asleep on the floor. The enum order is the catalog's order, which is also
/// the order <see cref="RelicCatalog.Pick"/> walks when it skips a relic the run already holds.
/// </remarks>
public enum RelicKind
{
    /// <summary>A heart comes back on every floor clear.</summary>
    EmberHeart,

    /// <summary>The player walks a little faster, forever.</summary>
    QuietSoles,

    /// <summary>One more blink and veil on every floor from now on.</summary>
    DeepPocket,

    /// <summary>Secret rooms appear twice as often.</summary>
    SleepersDust,

    /// <summary>The torch reaches one step further.</summary>
    LanternShard,

    /// <summary>The first catch of each floor is free.</summary>
    IronSkin,

    /// <summary>The grace after a catch lasts longer.</summary>
    MothsWing,

    /// <summary>Blooms within one step are drawn to the player.</summary>
    MagnetStone,

    /// <summary>The hunter's glow can be felt from further away.</summary>
    GildedTooth,

    /// <summary>Sprinting tires the player out more slowly.</summary>
    WanderersMark,

    /// <summary>The first fatal catch of each floor leaves one heart.</summary>
    SaltRing,

    /// <summary>The hunters move more slowly, forever.</summary>
    SlowCoil,

    /// <summary>Every bloom taken gives a blink back.</summary>
    KeyEater,

    /// <summary>Every catch costs one heart less, never below one.</summary>
    HollowIdol,

    /// <summary>One more lantern, thrown one step further.</summary>
    LanternOfGreed,

    /// <summary>Drag holds the hunt for longer.</summary>
    VowOfSilence,

    /// <summary>Straight runs top out at a higher speed.</summary>
    BraidedVine,

    /// <summary>The veil lasts a second longer.</summary>
    TideGlass,

    /// <summary>One more veil on every floor.</summary>
    Veilwalker,

    /// <summary>One more shove on every floor.</summary>
    Bulwark,

    /// <summary>One more drag on every floor.</summary>
    Dragnet,

    /// <summary>Secret rooms appear a little more often.</summary>
    MothersGift,

    /// <summary>Turning costs less speed.</summary>
    PilgrimsStep,

    /// <summary>The sprint lasts a third again as long.</summary>
    SteadyBreath,

    /// <summary>The hunt does not speed up as much after a drag.</summary>
    QuietDrag,

    /// <summary>A heart when it is taken, and a heart on every floor clear.</summary>
    SecondHarvest,

    /// <summary>Blooms within ten tiles glow on their own tile, through the dark.</summary>
    GraveLantern,

    /// <summary>Every floor asks for one bloom fewer.</summary>
    HollowLantern,

    /// <summary>The torch reaches two steps further.</summary>
    LastCandle,

    /// <summary>Two hearts back on every floor clear.</summary>
    GraveNurse,

    /// <summary>Hunters can be felt even further away.</summary>
    Nightjar,

    /// <summary>Two more lanterns on every floor.</summary>
    NineLanterns,

    /// <summary>A catch lights every bloom on the floor.</summary>
    SporeSieve,

    /// <summary>Two hearts the moment it is taken.</summary>
    MarigoldCrown,

    /// <summary>A slightly longer grace after a catch.</summary>
    SaltCandle,

    /// <summary>Turns take less of your speed away.</summary>
    AshWedge,

    /// <summary>Secret rooms are a little more likely.</summary>
    CopperCoin,
}

/// <summary>
/// The family a relic belongs to, used by both frontends for its colour and by the run details
/// card for grouping.
/// </summary>
/// <remarks>
/// A family is a reading aid, not a rule: it changes nothing about what a relic does. It exists so
/// that a gem's colour means the same thing in the window as it does in the run's list, instead of
/// two switches drifting apart.
/// </remarks>
public enum RelicFamily
{
    /// <summary>Walking, hearts and the body's speed.</summary>
    Body,

    /// <summary>The torch, the veil and everything the player can see.</summary>
    Light,

    /// <summary>Hunters: their pace, their senses and the verbs that answer them.</summary>
    Hunt,

    /// <summary>Blooms, rooms and the run's economy.</summary>
    Garden,

    /// <summary>Healing, revivals and the ritual bargains.</summary>
    Rite,
}

/// <summary>
/// How rare a relic is, which decides how often an altar offers it.
/// </summary>
/// <remarks>
/// <para>
/// Relics are not cards and are not drafted, so <see cref="CardTier"/> does not describe them: a
/// relic's rarity is a weight on the altar's draw, and nothing else. The three bands are the
/// promise the altar makes — a common is a small, welcome thing; a legendary changes how a run is
/// played and should feel like finding one.
/// </para>
/// <para>
/// The names are deliberately not <see cref="CardTier"/>'s: a relic's common is not a draft
/// common, and a reader should not have to remember which scale a number belongs to.
/// </para>
/// </remarks>
public enum RelicRarity
{
    /// <summary>A small bonus, offered most often.</summary>
    Common,

    /// <summary>A real change to the run, offered less often.</summary>
    Rare,

    /// <summary>A run-defining find; roughly one altar in thirteen holds one.</summary>
    Legendary,
}

/// <summary>
/// The relic catalog: the permanent finds a floor's altar can hold, as card-shaped effects.
/// </summary>
/// <remarks>
/// <para>
/// A relic is a <see cref="Card"/> with no drawback, stored in the run's modifiers like any other
/// card. That is deliberate: the whole composition path — clamping, the run summary, the HUD's
/// list, saving and restoring — already understands cards, so a relic needs no second pipeline and
/// no special case in <see cref="GameState"/>. The only thing that tells them apart is where they
/// came from.
/// </para>
/// <para>
/// Every relic is permanent, and a run never holds two of the same: <see cref="Campaign"/> skips a
/// relic it already holds when it stocks the next altar. When the pool runs dry the altar is
/// simply absent, rather than paying out a card the player cannot use.
/// </para>
/// <para>
/// Rarity is a weight, not a gate. An altar's stock is drawn by walking a weighted shuffle of the
/// whole pool, so a legendary can appear on floor one and a common on floor twenty — it is simply
/// much less likely. That is what keeps the altar a small event rather than a checklist.
/// </para>
/// </remarks>
public static class RelicCatalog
{
    /// <summary>Gets how many times a common relic is weighted against a legendary.</summary>
    public const int CommonWeight = 6;

    /// <summary>Gets how many times a rare relic is weighted against a legendary.</summary>
    public const int RareWeight = 3;

    /// <summary>Gets the weight of a legendary relic: the unit the other two are measured in.</summary>
    public const int LegendaryWeight = 1;

    private static readonly ReadOnlyCollection<Card> Catalog = new(new List<Card>
    {
        Relic(
            "relic-ember-heart",
            "Ember Heart",
            "A heart returns every time you clear a floor.",
            new CardEffect { HealOnFloorClear = 1 }),
        Relic(
            "relic-quiet-soles",
            "Quiet Soles",
            "You walk a little faster on every floor from now on.",
            new CardEffect { PlayerStepScale = 0.944 }),
        Relic(
            "relic-deep-pocket",
            "Deep Pocket",
            "One more blink and one more veil on every floor from now on.",
            new CardEffect { BlinkCharges = 1, VeilCharges = 1 }),
        Relic(
            "relic-sleepers-dust",
            "Sleeper's Dust",
            "Secret rooms appear twice as often.",
            new CardEffect { RoomChancePercent = 10 }),
        Relic(
            "relic-lantern-shard",
            "Lantern Shard",
            "Your light reaches one step further on every floor.",
            new CardEffect { VisionRadius = 1 }),
        Relic(
            "relic-iron-skin",
            "Iron Skin",
            "The first catch on each floor costs no heart.",
            new CardEffect { FirstCatchFree = true }),
        Relic(
            "relic-moths-wing",
            "Moth's Wing",
            "The grace after a catch lasts half a second longer.",
            new CardEffect { InvulnerabilitySeconds = 0.6 }),
        Relic(
            "relic-magnet-stone",
            "Magnet Stone",
            "Blooms within one step come to you.",
            new CardEffect { MagneticKeys = true }),
        Relic(
            "relic-gilded-tooth",
            "Gilded Tooth",
            "You feel a hunter's glow two steps further away.",
            new CardEffect { EnemyAuraDistance = 2 }),
        Relic(
            "relic-wanderers-mark",
            "Wanderer's Mark",
            "Your sprint lasts 40% longer.",
            new CardEffect { SprintStaminaScale = 1.4 }),
        Relic(
            "relic-salt-ring",
            "Salt Ring",
            "The first fatal catch of each floor leaves you one heart.",
            new CardEffect { ReviveOnce = true }),
        Relic(
            "relic-slow-coil",
            "Slow Coil",
            "The hunters move more slowly on every floor.",
            new CardEffect { EnemyStepScale = 1.12 }),
        Relic(
            "relic-key-eater",
            "Key Eater",
            "Every bloom you take gives back a blink.",
            new CardEffect { BlinkRefundPerKey = 1 }),
        Relic(
            "relic-hollow-idol",
            "Hollow Idol",
            "Every catch costs one heart less, never below one.",
            new CardEffect { CatchCost = -1 }),
        Relic(
            "relic-lantern-of-greed",
            "Lantern of Greed",
            "One more lantern, thrown one step further.",
            new CardEffect { LanternCharges = 1, LanternRange = 1 }),
        Relic(
            "relic-vow-of-silence",
            "Vow of Silence",
            "Every drag holds the hunt half again as long.",
            new CardEffect { DragSeconds = 1.5 }),
        Relic(
            "relic-braided-vine",
            "Braided Vine",
            "Straight runs top out at a higher speed.",
            new CardEffect { MomentumMaxBonus = 0.08 }),
        Relic(
            "relic-tide-glass",
            "Tide Glass",
            "Your veil lasts a second longer.",
            new CardEffect { VeilDurationSeconds = 1.0 }),
        Relic(
            "relic-veilwalker",
            "Veilwalker",
            "One more veil on every floor from now on.",
            new CardEffect { VeilCharges = 1 }),
        Relic(
            "relic-bulwark",
            "Bulwark",
            "One more shove on every floor from now on.",
            new CardEffect { ShoveCharges = 1 }),
        Relic(
            "relic-dragnet",
            "Dragnet",
            "One more drag on every floor from now on.",
            new CardEffect { DragCharges = 1 }),
        Relic(
            "relic-mothers-gift",
            "Mother's Gift",
            "Secret rooms appear half again as often.",
            new CardEffect { RoomChancePercent = 15 }),
        Relic(
            "relic-pilgrims-step",
            "Pilgrim's Step",
            "Turning costs you less of your speed.",
            new CardEffect { TurnPenaltyScale = -0.4 }),
        Relic(
            "relic-steady-breath",
            "Steady Breath",
            "Your sprint lasts a quarter again as long.",
            new CardEffect { SprintStaminaScale = 1.24 }),
        Relic(
            "relic-quiet-drag",
            "Quiet Drag",
            "The hunt does not speed up as much after a drag.",
            new CardEffect { DragPenaltySeconds = -1.5 }),
        Relic(
            "relic-second-harvest",
            "Second Harvest",
            "A heart when you take it, and a heart every time you clear a floor.",
            new CardEffect { HealNow = 1, HealOnFloorClear = 1 }),
        Relic(
            "relic-grave-lantern",
            "Grave Lantern",
            "Blooms within ten tiles glow on their own tile, through walls and dark.",
            new CardEffect { BloomSenseRadius = 10 }),
        Relic(
            "relic-hollow-lantern",
            "Hollow Lantern",
            "Every floor from now on asks for one bloom fewer.",
            new CardEffect { KeyCount = -1 }),
        Relic(
            "relic-last-candle",
            "Last Candle",
            "Your torch reaches two steps further on every floor.",
            new CardEffect { VisionRadius = 2 }),
        Relic(
            "relic-grave-nurse",
            "Grave Nurse",
            "Two hearts come back every time you clear a floor.",
            new CardEffect { HealOnFloorClear = 2 }),
        Relic(
            "relic-nightjar",
            "Nightjar",
            "You feel a hunter's glow three steps further away.",
            new CardEffect { EnemyAuraDistance = 3 }),
        Relic(
            "relic-nine-lanterns",
            "Nine Lanterns",
            "Two more lanterns on every floor from now on.",
            new CardEffect { LanternCharges = 2 }),
        Relic(
            "relic-spore-sieve",
            "Spore Sieve",
            "A catch lights every bloom on the floor on its own tile.",
            new CardEffect { BloomGlowReach = 1 }),
        Relic(
            "relic-marigold-crown",
            "Marigold Crown",
            "Two hearts the moment you take it.",
            new CardEffect { HealNow = 2 }),
        Relic(
            "relic-salt-candle",
            "Salt Candle",
            "The grace after a catch lasts a breath longer.",
            new CardEffect { InvulnerabilitySeconds = 0.4 }),
        Relic(
            "relic-ash-wedge",
            "Ash Wedge",
            "A turn slows you for a second less.",
            new CardEffect { TurnPenaltySeconds = -1.0 }),
        Relic(
            "relic-copper-coin",
            "Copper Coin",
            "Secret rooms appear a little more often.",
            new CardEffect { RoomChancePercent = 5 }),
    });

    /// <summary>Gets every relic, in <see cref="RelicKind"/> order.</summary>
    public static IReadOnlyList<Card> Cards => Catalog;

    /// <summary>Gets every relic kind, in catalog order.</summary>
    public static IReadOnlyList<RelicKind> Kinds { get; } =
        new ReadOnlyCollection<RelicKind>(new List<RelicKind>(Enum.GetValues<RelicKind>()));

    /// <summary>Returns the relic of a kind.</summary>
    /// <param name="kind">The relic's kind.</param>
    /// <returns>The card that carries the relic's effect.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not a relic.</exception>
    public static Card CardFor(RelicKind kind)
    {
        var index = Array.IndexOf(Enum.GetValues<RelicKind>(), kind);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a relic kind.");
        }

        return Catalog[index];
    }

    /// <summary>Returns how rare a relic is, which decides how often an altar offers it.</summary>
    /// <param name="kind">The relic's kind.</param>
    /// <returns>One of the three bands.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not a relic.</exception>
    public static RelicRarity RarityOf(RelicKind kind) => kind switch
    {
        RelicKind.MothsWing or RelicKind.GildedTooth or RelicKind.WanderersMark or
        RelicKind.BraidedVine or RelicKind.TideGlass or RelicKind.Veilwalker or
        RelicKind.Bulwark or RelicKind.Dragnet or RelicKind.PilgrimsStep or
        RelicKind.SteadyBreath or RelicKind.QuietDrag or RelicKind.SaltCandle or
        RelicKind.AshWedge or RelicKind.CopperCoin => RelicRarity.Common,

        RelicKind.EmberHeart or RelicKind.QuietSoles or RelicKind.SleepersDust or
        RelicKind.LanternShard or RelicKind.MagnetStone or RelicKind.KeyEater or
        RelicKind.LanternOfGreed or RelicKind.VowOfSilence or RelicKind.MothersGift or
        RelicKind.Nightjar or RelicKind.NineLanterns or RelicKind.SporeSieve or
        RelicKind.MarigoldCrown => RelicRarity.Rare,

        RelicKind.DeepPocket or RelicKind.IronSkin or RelicKind.SaltRing or
        RelicKind.SlowCoil or RelicKind.HollowIdol or RelicKind.SecondHarvest or
        RelicKind.GraveLantern or RelicKind.HollowLantern or RelicKind.LastCandle or
        RelicKind.GraveNurse => RelicRarity.Legendary,

        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a relic kind."),
    };

    /// <summary>Returns the draw weight of a relic: how often an altar offers it.</summary>
    /// <param name="kind">The relic's kind.</param>
    /// <returns>Six for a common, three for a rare and one for a legendary.</returns>
    public static int WeightOf(RelicKind kind) => RarityOf(kind) switch
    {
        RelicRarity.Common => CommonWeight,
        RelicRarity.Rare => RareWeight,
        _ => LegendaryWeight,
    };

    /// <summary>Returns the family a relic belongs to, which decides its colour.</summary>
    /// <param name="kind">The relic's kind.</param>
    /// <returns>One of the five families.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not a relic.</exception>
    public static RelicFamily FamilyOf(RelicKind kind) => kind switch
    {
        RelicKind.QuietSoles or RelicKind.IronSkin or RelicKind.MothsWing or
        RelicKind.WanderersMark or RelicKind.BraidedVine or RelicKind.Bulwark or
        RelicKind.PilgrimsStep or RelicKind.SteadyBreath or RelicKind.SaltCandle or
        RelicKind.AshWedge => RelicFamily.Body,

        RelicKind.LanternShard or RelicKind.LanternOfGreed or RelicKind.TideGlass or
        RelicKind.Veilwalker or RelicKind.NineLanterns or RelicKind.LastCandle => RelicFamily.Light,

        RelicKind.GildedTooth or RelicKind.SlowCoil or RelicKind.KeyEater or
        RelicKind.Dragnet or RelicKind.QuietDrag or RelicKind.Nightjar => RelicFamily.Hunt,

        RelicKind.SleepersDust or RelicKind.MagnetStone or RelicKind.MothersGift or
        RelicKind.SporeSieve or RelicKind.CopperCoin => RelicFamily.Garden,

        RelicKind.EmberHeart or RelicKind.SaltRing or RelicKind.DeepPocket or
        RelicKind.HollowIdol or RelicKind.VowOfSilence or RelicKind.SecondHarvest or
        RelicKind.GraveLantern or RelicKind.HollowLantern or RelicKind.GraveNurse or
        RelicKind.MarigoldCrown => RelicFamily.Rite,

        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a relic kind."),
    };

    /// <summary>Finds the kind a relic card id names, for colouring and grouping.</summary>
    /// <param name="id">The card id, for example <c>relic-ember-heart</c>.</param>
    /// <returns>The kind, or <see langword="null"/> when the id names no relic.</returns>
    public static RelicKind? KindOf(string id)
    {
        var card = ById(id);

        return card is null ? null : Kinds[Catalog.IndexOf(card)];
    }

    /// <summary>Finds a relic by its card id.</summary>
    /// <param name="id">The card id, for example <c>relic-ember-heart</c>.</param>
    /// <returns>The relic, or <see langword="null"/> when the id names no relic.</returns>
    public static Card? ById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (var card in Catalog)
        {
            if (string.Equals(card.Id, id, StringComparison.Ordinal))
            {
                return card;
            }
        }

        return null;
    }

    /// <summary>
    /// Chooses the relic an altar stocks on a floor, walking past the first
    /// <paramref name="skip"/> relics in the rolled order.
    /// </summary>
    /// <param name="runSeed">The run's seed.</param>
    /// <param name="floorNumber">The floor the altar stands on.</param>
    /// <param name="skip">How many kinds to step past; a run uses this for relics it already holds.</param>
    /// <returns>A kind that is guaranteed distinct for each <paramref name="skip"/> below the pool size.</returns>
    /// <remarks>
    /// The order is a weighted shuffle of the whole pool, not a raw roll: walking it means an altar
    /// is most likely to hold a common, and a legendary needs the shuffle to put it first. It is
    /// still a permutation, so the skip walk cannot land twice on one relic and a run holding half
    /// the pool always has somewhere left to step.
    /// </remarks>
    public static RelicKind Pick(int runSeed, int floorNumber, int skip = 0)
    {
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), skip, "Skip cannot be negative.");
        }

        var order = Shuffle(runSeed, floorNumber);

        return order[skip % order.Count];
    }

    /// <summary>
    /// Builds the floor's weighted order of every relic, commonest first by weight rather than by
    /// position.
    /// </summary>
    private static List<RelicKind> Shuffle(int runSeed, int floorNumber)
    {
        var random = new SeededRandom(unchecked((int)Mix(runSeed, floorNumber)));
        var pool = new List<RelicKind>(Kinds);
        var order = new List<RelicKind>(pool.Count);

        while (pool.Count > 0)
        {
            var total = 0;

            foreach (var kind in pool)
            {
                total += WeightOf(kind);
            }

            var roll = random.NextInt(total);
            var chosen = pool.Count - 1;

            for (var index = 0; index < pool.Count; index++)
            {
                var weight = WeightOf(pool[index]);

                if (roll < weight)
                {
                    chosen = index;
                    break;
                }

                roll -= weight;
            }

            order.Add(pool[chosen]);
            pool.RemoveAt(chosen);
        }

        return order;
    }

    /// <summary>
    /// Mixes a seed and a floor number into a stable, well-spread non-negative integer, so
    /// neighbouring seeds do not stock the same altar on the same floor.
    /// </summary>
    private static uint Mix(int seed, int number)
    {
        unchecked
        {
            var mixed = (uint)seed * 2246822519u;
            mixed ^= (uint)number * 3266489917u;
            mixed ^= mixed >> 13;
            mixed *= 2654435761u;
            mixed ^= mixed >> 15;
            return mixed;
        }
    }

    /// <summary>Builds one relic as a card.</summary>
    private static Card Relic(string id, string name, string face, CardEffect effect) => new()
    {
        Id = id,
        Name = name,
        Tier = CardTier.Rare,
        FaceText = face,
        Effect = effect,
    };
}
