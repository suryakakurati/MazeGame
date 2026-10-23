using System;
using System.Collections.Generic;

namespace MazeGame.Core.Game;

/// <summary>
/// A run's progress in a form that can be written to disk and rebuilt later.
/// </summary>
/// <remarks>
/// <para>
/// The snapshot is deliberately <b>not</b> a floor: it stores the run — its seed, which floor it is
/// on, the hearts it carries, the cards it took and the garden it has grown — and a restore
/// rebuilds that floor from the seed. Resuming therefore drops the player at the start of the floor
/// they were on rather than in the middle of a step, which is the honest thing a run built from
/// seeds can promise. A half-step saved mid-tick would be a second, hand-maintained copy of
/// <see cref="GameState"/>'s rules.
/// </para>
/// <para>
/// Every field is a plain value or a list of ids, so the UI can serialize this without knowing a
/// single rule. The ids are validated on restore: a save naming a card that no longer ships is
/// refused rather than silently dropped.
/// </para>
/// </remarks>
public sealed record RunSnapshot
{
    /// <summary>The version this build writes and the newest version it can read.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets the format version the save was written with.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Gets the seed the run was built from, which rebuilds every floor and draft.</summary>
    public int RunSeed { get; init; }

    /// <summary>Gets the zero-based index of the floor the run is on.</summary>
    public int FloorIndex { get; init; }

    /// <summary>Gets the hearts the run was carrying on that floor.</summary>
    public int Hearts { get; init; }

    /// <summary>Gets the hearts banked for the floor after this one.</summary>
    public int CarryHearts { get; init; }

    /// <summary>
    /// Gets the blooms banked before this floor. The blooms picked on the floor being saved are
    /// folded in here, because a rebuilt floor starts its own count at zero.
    /// </summary>
    public int BloomsBanked { get; init; }

    /// <summary>Gets how many rerolls this floor's draft has already had.</summary>
    public int DraftRerolls { get; init; }

    /// <summary>Gets the deepest floor the run has ever reached.</summary>
    public int DeepestFloor { get; init; }

    /// <summary>Gets a value indicating whether the Turn has already been spent.</summary>
    public bool Reincarnated { get; init; }

    /// <summary>Gets the size bonus queued for the next draft.</summary>
    public int NextDraftBonus { get; init; }

    /// <summary>Gets the size penalty queued for the draft after that.</summary>
    public int FollowingDraftBonus { get; init; }

    /// <summary>Gets the hunters the Toll keeps at home on the next floor.</summary>
    public int HuntersOffNextFloor { get; init; }

    /// <summary>Gets the floor the Seed Vault's bloom pays on, if it is still waiting.</summary>
    public int? SeedBloomsOnFloor { get; init; }

    /// <summary>Gets the floor a House Favour promised a casino on.</summary>
    public int? CasinoFloor { get; init; }

    /// <summary>Gets a value indicating whether the promised table has been played.</summary>
    public bool CasinoGambleUsed { get; init; }

    /// <summary>Gets the ids of every card the run holds, in the order they were taken.</summary>
    public IReadOnlyList<string> CardIds { get; init; } = Array.Empty<string>();

    /// <summary>Gets the descent conditions the run has taken.</summary>
    public IReadOnlyList<DescentCondition> Conditions { get; init; } = Array.Empty<DescentCondition>();

    /// <summary>Gets the floor numbers whose condition gate has been answered.</summary>
    public IReadOnlyList<int> ChosenGates { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Checks that the snapshot could describe a real run and that this build can read it.
    /// </summary>
    /// <param name="floorCount">How many floors the run's catalog holds.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the version is from another build, the floor is outside the catalog, the run
    /// holds no hearts, or a card id does not name a shipped card.
    /// </exception>
    public void Validate(int floorCount)
    {
        if (Version != CurrentVersion)
        {
            throw new ArgumentException(
                $"This save was written by a build with format version {Version}; this one reads {CurrentVersion}.");
        }

        if (floorCount < 1 || FloorIndex < 0 || FloorIndex >= floorCount)
        {
            throw new ArgumentException("The saved floor is not one of the run's floors.");
        }

        if (Hearts < 1)
        {
            throw new ArgumentException("A run is only saved while it still has a heart.");
        }

        if (CarryHearts < 0 || BloomsBanked < 0 || DraftRerolls < 0 || NextDraftBonus < 0 ||
            FollowingDraftBonus < 0 || HuntersOffNextFloor < 0 || DeepestFloor < 0)
        {
            throw new ArgumentException("A save cannot bank a negative number.");
        }

        foreach (var id in CardIds)
        {
            // Relics are stored in the same list as drafted cards, so both catalogs are asked.
            if (CardCatalog.ById(id) is null && RelicCatalog.ById(id) is null)
            {
                throw new ArgumentException($"The save names a card this build does not ship: '{id}'.");
            }
        }
    }
}
